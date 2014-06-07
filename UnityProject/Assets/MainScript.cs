using UnityEngine;

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Winterdom.IO.FileMap;

public class MainScript : MonoBehaviour 
{


	private const int FRAME_PARTICLE_SIZE = 32;
	private const int SYSTEM_GRANULARITY = 65536;
	
	/*****/

	[HideInInspector]
	public int maxFrames;

	[HideInInspector]
	public int maxParticles;	

	[HideInInspector]
	public string dataPath;

	[HideInInspector]
	public string indexPath;

	/*****/

	private ulong[] frameIndices;
	private byte[] tempBuffer;

	private MemoryMappedFile map;

	private int currentFrame = 0;
	private int animationTick = 0;

	/*****/

	public void Init (int maxFrames, int maxParticles, string dataPath, string indexPath) 
	{
		this.maxFrames = maxFrames;
		this.maxParticles = maxParticles;
		this.dataPath = dataPath;
		this.indexPath = indexPath;

		gameObject.AddComponent<MeshRenderer>().material = Resources.Load("MolMaterial") as Material;
		gameObject.AddComponent<MeshFilter>();
	}

	void OnEnable () 
	{

	}
	
	void OnApplicationQuit() 
	{

	}
	
	void Start ()
	{
		//UnityEditor.SceneView.FocusWindowIfItsOpen(typeof(UnityEditor.SceneView));

		if (!File.Exists (dataPath)) 
		{
			Debug.LogError ("No data file found at: " + dataPath);
			return;
		}

		if (!File.Exists (indexPath)) 
		{
			Debug.LogError ("No index file found.");
			return;
		}

		// Read frame indices
		frameIndices = new ulong[maxFrames];
		byte[] indexBytes = File.ReadAllBytes(indexPath);
		Buffer.BlockCopy(indexBytes, 0, frameIndices, 0, indexBytes.Length);

		// Create the map to the binary file
		var length = new System.IO.FileInfo(dataPath).Length;
		map = MemoryMappedFile.Create(dataPath, MapProtection.PageReadOnly, length);

		// Init vertices
		Vector3[] vertices = new Vector3[maxParticles];
		int[] indices = new int[maxParticles];
		
		for (var i = 0; i < maxParticles; i++)
		{
			indices[i] = i;
			vertices[i].Set(0, 0, 0);
		}

		Mesh mesh = GetComponent<MeshFilter>().mesh;	
		mesh.vertices = vertices;
		mesh.SetIndices(indices, MeshTopology.Points, 0);
	}

	void OnGUI () 
	{
		GUILayout.Label("Current frame: " + currentFrame);
	}
	
	void Update () 
	{
		LoadNextFrame ();
	}
	
	void LoadNextFrame ()
	{	
		// Find offset values for the memory mapped files
		int offset1 = (currentFrame==0) ? 0 : (int)frameIndices[currentFrame-1];
		int offset2 = (int)Math.Floor((float)offset1 / (float)SYSTEM_GRANULARITY) * SYSTEM_GRANULARITY;
		int offset3 = offset1 - offset2;
		int compressedFrameSize = (int)frameIndices [currentFrame] - offset1;
		int size = offset3 + compressedFrameSize;

		// Instanciate temp buffer if null
		if(tempBuffer == null) tempBuffer = new byte[0];

		// Resize temp buffer if too small to host the data
		if(tempBuffer.Length < size) Array.Resize(ref tempBuffer, size);
		
		// Create view and fetch bytes into temp buffer
		using ( Stream view = map.MapView(MapAccess.FileMapRead, offset2, size) )
		{
			view.Read(tempBuffer, 0, size);
		}

		// Fetch compressed frame from the temp buffer
		byte[] compressedFrame = new byte[compressedFrameSize];
		Buffer.BlockCopy(tempBuffer, offset3, compressedFrame, 0, compressedFrameSize);

		byte[] uncompressedFrame = new byte[maxParticles * FRAME_PARTICLE_SIZE];

		int res = ZLibWrapper.UncompressBuffer(uncompressedFrame, (uint)uncompressedFrame.Length, compressedFrame, (uint)compressedFrameSize);

		// Update mesh vertices
		Mesh mesh = GetComponent<MeshFilter>().mesh;
		Vector3[] vertices = mesh.vertices;

		for (var i = 0; i < maxParticles; i++)
		{
//			vertices[i].x = (float)Half.ToHalf(uncompressedFrame, i * FRAME_PARTICLE_SIZE + 2 * sizeof(UInt16));
//			vertices[i].y = (float)Half.ToHalf(uncompressedFrame, i * FRAME_PARTICLE_SIZE + 3 * sizeof(UInt16));
//			vertices[i].z = (float)Half.ToHalf(uncompressedFrame, i * FRAME_PARTICLE_SIZE + 4 * sizeof(UInt16));

			vertices[i].x = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (2 * sizeof(float)));
			vertices[i].y = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (3 * sizeof(float)));
			vertices[i].z = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (4 * sizeof(float)));
		}		

		mesh.vertices = vertices;
		
		currentFrame ++;
		if (currentFrame > maxFrames-1) currentFrame = 0; 
	}
}
