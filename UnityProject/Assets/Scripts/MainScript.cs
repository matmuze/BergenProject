using UnityEngine;

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

using Winterdom.IO.FileMap;

public struct TrajectoryData 
{	
	public int id;
	public int reactionId;
	public int startFrame;
	public int endFrame;
		
	public Mesh mesh;	
	public int[] indices;
	public Vector3[] vertices;
}

public struct ParticleFrameData 
{
	public int id;
	public int type;
	public Vector4 position;
	public Vector4 rotation;
}

public struct ReactionData 
{
	public int frame;
	public float time;
	public Vector3 position;
	public string type;
	public int[] reactants;
	public int[] products;
	
	public override string ToString ()
	{
		return string.Format ("{0} : {1} : {2} : {3} : {4} : {5}", frame, time, position.x, position.y, position.z, type);
	}
}

public class MainScript : MonoBehaviour 
{
	private const int FRAME_PARTICLE_SIZE = 32;
	private const int SYSTEM_GRANULARITY = 65536;
	private const int SCALING_FACTOR = 25;
	private const int FRAME_OFFSET = 2500;
	private const double TIME_STEP = 0.00000005;
	private const float DEFAULT_FRAME_DURATION = 0.04166666666f;

	private const string viz_data_path = @"MCell\viz_data\data.bin";
	private const string viz_data_index_path = @"MCell\viz_data\index.bin";
	private const string rxn_data_path = @"MCell\rxn_data\reactions.txt";
	
	/*****/

	private int frameCount;
	
	[HideInInspector]
	public int maxParticles;	
	
	/*****/
	
	private ulong[] frameIndices;
	private byte[] tempBuffer;
	
	private MemoryMappedFile map;
	
	private ReactionData[] reactions;
	private int[] reactionsPerFrame;
	
	private Vector4[] drawPositions;
	private Vector4[] drawRotations;
	private int[] drawHighlights;

	public Material trajectoryMaterial;
	public GameObject trajectoryHelper;
	private TrajectoryData drawTrajectory;

	private bool pause = true;

	private int currentFrame = 0;
	private int previousFrame = -1;	

	private float progress = 0;
	private float elapsedTimeSinceLastFrame = 0.0f;

	private int currentReaction = 0;
	private int previousReaction = -1;
	
	[RangeAttribute(1, 100)]
	public int stepIncrease = 1;

	[RangeAttribute(0, 1)]
	public float slowDown = 0;

	[RangeAttribute(0, 1)]
	public float trajectoryLOD = 0;
	private float previousTrajectoryLOD = 0;
			
	public bool showTrajectory = true;

	//	[RangeAttribute(1, 100)]
	//	public int trajectoryLOD = 1;
	//	private int previousTrajectoryLOD = 1;

	private Vector3[] cachedVertices;
	private Vector3[] linearCachedVertices;

	void OnEnable () 
	{
		
	}
	
	void OnApplicationQuit() 
	{
		
	}
	
	void Start ()
	{
		if (!File.Exists (viz_data_path)) 
		{
			Debug.LogError ("No data file found at: " + viz_data_path);
			return;
		}
		
		if (!File.Exists (viz_data_index_path)) 
		{
			Debug.LogError ("No data file found at: " + viz_data_index_path);
			return;
		}
		
		if (!File.Exists (rxn_data_path)) 
		{
			Debug.LogError ("No data file found at: " + rxn_data_path);
			return;
		}
		
		// Read frame indices
		byte[] indexBytes = File.ReadAllBytes(viz_data_index_path);
		frameCount = indexBytes.Length / sizeof(ulong);
		frameIndices = new ulong[frameCount];
		Buffer.BlockCopy(indexBytes, 0, frameIndices, 0, frameCount * sizeof(ulong));
		
		// Create the map to the binary file
		var length = new System.IO.FileInfo(viz_data_path).Length;
		map = MemoryMappedFile.Create(viz_data_path, MapProtection.PageReadOnly, length);
		
		// Read reaction data
		List<ReactionData> reactions = new List<ReactionData>();
		reactionsPerFrame = new int[frameCount];
		
		StreamReader reader = File.OpenText(rxn_data_path);
		string line;
		while ((line = reader.ReadLine()) != null)
		{
			string[] fields = line.Split(' ');
			
			ReactionData reactionData = new ReactionData();
			reactionData.frame = int.Parse(fields[0])-1;

			if(reactionData.frame >= frameCount) break;

			reactionData.time = float.Parse(fields[1]);
			reactionData.position = new Vector3(float.Parse(fields[2]), float.Parse(fields[3]), float.Parse(fields[4])) * SCALING_FACTOR;
			reactionData.type = fields[5];
			reactionData.reactants = new int[2];
			
			reactions.Add(reactionData);
			reactionsPerFrame[reactionData.frame]++;
		}		
		this.reactions = reactions.ToArray();

		int count = 0;

		Debug.Log (">>>>>>>>");
		foreach(ReactionData reactionData in this.reactions)
		{
			count ++;
			Debug.Log ("Reaction: " + count + " frame: " + reactionData.frame);

			ParticleFrameData[] reactionFrameData = LoadFrameData(reactionData.frame);
			ParticleFrameData[] nextFrameData = LoadFrameData(reactionData.frame+1);
			
			if(reactionsPerFrame[reactionData.frame] > 1)
				throw new Exception("More that two reactions for the given frame");
			
			int[] reactionSortedIDs = reactionFrameData.ToList().Select(e => e.id).OrderBy(e => e).ToArray();
			int[] nextSortedIDs = nextFrameData.ToList().Select(e => e.id).OrderBy(e => e).ToArray();
			
			int reactant = -1;
			
			for(int i = 0; i < reactionSortedIDs.Length; i++)
			{
				if(reactionSortedIDs[i] != nextSortedIDs[i])
				{
					reactant = reactionSortedIDs[i];
					Debug.Log ("Reactant found: " + reactant);
					reactionData.reactants[0] = reactant;
					break;
				}
			}
			
			if(reactant == -1)
				throw new Exception("No reactant found for the given frame");
			
			// Search for reaction partner
			int reactantIndex = reactionFrameData.ToList().FindIndex( e => e.id == reactant);
			
			float distance = float.MaxValue;
			int partner = -1;
			int partnerIndex = -1;

			for(int i = 0; i < reactionFrameData.Length; i++)
			{
				float dist = Vector3.Distance(reactionFrameData[reactantIndex].position, reactionFrameData[i].position);
				
				if(reactionFrameData[i].type == 3 && dist < distance)
				{
					partner = reactionFrameData[i].id;
					distance = dist;
				}
			}

			Debug.Log("Distance: " + distance);
			
			if(partner == -1)
				throw new Exception("No partner found for the given reaction");
			
			Debug.Log ("Reaction partner found: " + partner);
			reactionData.reactants[1] = partner;

			Debug.Log (">>>>>>>>");
		}

		drawTrajectory.reactionId = -1;

		drawPositions = new Vector4[maxParticles];
		drawRotations = new Vector4[maxParticles];
		drawHighlights = new int[maxParticles];
		
		for (var i = 0; i < maxParticles; i++)
		{
			Quaternion q = UnityEngine.Random.rotation;			
			
			drawRotations[i].x = q.x;
			drawRotations[i].y = q.y;
			drawRotations[i].z = q.z;
			drawRotations[i].w = q.w;
			
			drawHighlights[i] = 0;
		}
	}

	void OnGUI () 
	{
		GUILayout.Label("Current frame: " + currentFrame);
		GUILayout.Label("Current time: " + (double)currentFrame * TIME_STEP);

		float previousProgress = GUI.HorizontalSlider(new Rect(25, Screen.height - 25, Screen.width - 50, 30), progress, 0.0F, 1.0F);

		if(progress != previousProgress)
		{
			Debug.Log(previousProgress);
			currentFrame = (int)(((float)frameCount - 1.0f) * previousProgress);
		}
	}

	bool trajectoryLoaded = false;
	bool forceRender = false;
	bool centerCamera = true;


	void Update () 
	{
		// Reaction oriented functions
		if (Input.GetKeyDown(KeyCode.LeftArrow))
		{
			if(currentReaction == 0) currentReaction = reactions.Length - 1;
			else currentReaction --; 
		}
		else if (Input.GetKeyDown(KeyCode.RightArrow))
		{
			currentReaction = (currentReaction + 1) % reactions.Length;
		}
		
		if(trajectoryLOD != previousTrajectoryLOD && trajectoryLoaded)
		{
			previousTrajectoryLOD = trajectoryLOD;
			drawTrajectory = LoadTrajectory(reactions[currentReaction].reactants[0], currentReaction, Math.Max(0, reactions[currentReaction].frame - FRAME_OFFSET), reactions[currentReaction].frame, trajectoryLOD);
			forceRender = true;
		}

		if(Input.GetKeyDown(KeyCode.L) && !trajectoryLoaded)
		{
			drawTrajectory = LoadTrajectory(reactions[currentReaction].reactants[0], currentReaction, Math.Max(0, reactions[currentReaction].frame - FRAME_OFFSET), reactions[currentReaction].frame, trajectoryLOD, true);
			trajectoryLoaded = true;			
			forceRender = true;
		}
		
		if(Input.GetKeyDown(KeyCode.R))
		{
			currentFrame = Math.Max(0, reactions[currentReaction].frame - FRAME_OFFSET);
			elapsedTimeSinceLastFrame = 0;
		}
		
		if(Input.GetKeyDown(KeyCode.F))
		{
			centerCamera = true;
			forceRender = true;
		}

		if(showTrajectory & trajectoryLoaded)
		{
			Graphics.DrawMesh(drawTrajectory.mesh, Vector3.zero, Quaternion.identity, trajectoryMaterial, 0);
			trajectoryHelper.GetComponent<Renderer>().enabled = true;
		}
		else
		{
			trajectoryHelper.GetComponent<Renderer>().enabled = false;
		}

		if(currentReaction != previousReaction)
		{
			Debug.Log("Current reaction: " + currentReaction);	

			currentFrame = Math.Max(0, reactions[currentReaction].frame - FRAME_OFFSET);
			trajectoryLoaded = currentReaction == drawTrajectory.reactionId;

			centerCamera = true;
			forceRender = true;
			previousReaction = currentReaction;
		}

		progress = (float)currentFrame / (float)frameCount;

		// If there is a new frame to display
		if(currentFrame != previousFrame || forceRender)
		{
			ParticleFrameData[] frameData = LoadFrameData(currentFrame);
			drawPositions = frameData.ToList().Select(e => e.position).ToArray();

			for (int i = 0; i < maxParticles; i++)
			{
				drawHighlights[i] = 0;
			}
			
			if(currentReaction != -1 && currentFrame < reactions[currentReaction].frame && currentFrame >= reactions[currentReaction].frame - FRAME_OFFSET)
			{
				int reactant1 = frameData.ToList().FindIndex( e=> e.id == reactions[currentReaction].reactants[0]);
				int reactant2 = frameData.ToList().FindIndex( e=> e.id == reactions[currentReaction].reactants[1]);
								
				if(trajectoryLoaded)
				{
					if(currentFrame >= drawTrajectory.startFrame && currentFrame <= drawTrajectory.endFrame)
					{
						if(currentFrame - drawTrajectory.startFrame < drawTrajectory.vertices.Length)
							drawPositions[reactant1] = drawTrajectory.vertices[currentFrame - drawTrajectory.startFrame];
						else
							drawPositions[reactant1] = drawTrajectory.vertices.Last();
					}
				}

				if(showTrajectory)
				{
					trajectoryHelper.transform.position = drawPositions[reactant1];
				}

				if(centerCamera)
				{
					GameObject.Find("Main Camera").GetComponent<MouseOrbit>().target = drawPositions[reactant1];
					centerCamera = false;

				}

				if(true)
				{
					drawHighlights[reactant1] = 1;
					drawHighlights[reactant2] = 1;
				}
			}

			GameObject.Find("Main Camera").GetComponent<MolScript>().UpdateMols(drawPositions, drawRotations, drawHighlights); 

			forceRender = false;
			previousFrame = currentFrame;
		}

		if (Input.GetKeyDown(KeyCode.Space)) pause = !pause;

		if(!pause)
		{
			elapsedTimeSinceLastFrame += Time.deltaTime;

			float frameDuration = DEFAULT_FRAME_DURATION * Mathf.Pow(1.0f + slowDown, 6.0f);

			if(elapsedTimeSinceLastFrame > frameDuration)
			{
				elapsedTimeSinceLastFrame = 0;
				currentFrame += stepIncrease;
			}						
		}
		else if(Input.GetKeyDown(KeyCode.N))
		{
			currentFrame += stepIncrease;
			elapsedTimeSinceLastFrame = 0;
		}

//		else if(Input.GetKey(KeyCode.N))
//		{
//			elapsedTimeSinceLastFrame += Time.deltaTime;
//			if(elapsedTimeSinceLastFrame > 0.1f)
//			{
//				elapsedTimeSinceLastFrame = 0;
//				currentFrame += stepIncrease;
//			}
//		}
//		else if(Input.GetKeyDown(KeyCode.B))
//		{
//			currentFrame --;
//			elapsedTimeSinceLastFrame = 0;
//		}
//		else if(Input.GetKey(KeyCode.B))
//		{
//			elapsedTimeSinceLastFrame += Time.deltaTime;
//			if(elapsedTimeSinceLastFrame > 0.1f)
//			{
//				elapsedTimeSinceLastFrame = 0;
//				currentFrame --;
//			}
//		}
		
		if (currentFrame > frameCount-1) currentFrame = 0; 
		if (currentFrame < 0) currentFrame = frameCount - 1; 
	}
	
	ParticleFrameData[] LoadFrameData (int frame)
	{	
		ParticleFrameData[] frameData = new ParticleFrameData[maxParticles];
		
		// Find offset values for the memory mapped files
		ulong offset1 = (frame==0) ? 0 : frameIndices[frame-1];
		ulong temp = offset1 / (ulong)SYSTEM_GRANULARITY;
		ulong offset2 = temp * (ulong)SYSTEM_GRANULARITY;

		int offset3 = (int)(offset1 - offset2);
		int compressedFrameSize = (int)(frameIndices [frame] - offset1);
		int size = offset3 + compressedFrameSize;
		
		// Instanciate temp buffer if null
		if(tempBuffer == null) tempBuffer = new byte[0];
		
		// Resize temp buffer if too small to host the data
		if(tempBuffer.Length < size) Array.Resize(ref tempBuffer, size);
		
		// Create view and fetch bytes into temp buffer
		using ( Stream view = map.MapView(MapAccess.FileMapRead, (long)offset2, size) )
		{
			view.Read(tempBuffer, 0, size);
		}
		
		// Fetch compressed frame from the temp buffer
		byte[] compressedFrame = new byte[compressedFrameSize];
		Buffer.BlockCopy(tempBuffer, offset3, compressedFrame, 0, compressedFrameSize);
		
		byte[] uncompressedFrame = new byte[maxParticles * FRAME_PARTICLE_SIZE];
		
		ZLibWrapper.UncompressBuffer(uncompressedFrame, (uint)uncompressedFrame.Length, compressedFrame, (uint)compressedFrameSize);
		
		for (var i = 0; i < maxParticles; i++)
		{
			frameData[i].type = (int)BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (0 * sizeof(float)));
			frameData[i].id = (int)BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (1 * sizeof(float)));
			frameData[i].position.x = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (2 * sizeof(float))) * SCALING_FACTOR;
			frameData[i].position.y = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (3 * sizeof(float))) * SCALING_FACTOR;
			frameData[i].position.z = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (4 * sizeof(float))) * SCALING_FACTOR;
			frameData[i].position.w = 1;
		}	
		
		return frameData;
	}

	TrajectoryData LoadTrajectory (int id, int reactionId, int startFrame, int endFrame, float trajectoryLOD, bool resetCachedVertices = false)
	{	
		if(startFrame >= endFrame) throw new Exception("End frame cannot be smaller than start frame");
		if(endFrame > frameCount) throw new Exception("Sampling interval exceeds max frames");
				
		int numFrames = (endFrame - startFrame) + 1;
		
		if(resetCachedVertices)
		{
			cachedVertices = new Vector3[numFrames];
			linearCachedVertices = new Vector3[numFrames];

			Vector3 first = LoadFrameData(startFrame).ToList().Find( e => e.id == id).position;
			Vector3 last = LoadFrameData(endFrame).ToList().Find( e => e.id == id).position;

			for(int i = 0; i < numFrames; i++)
			{
				float t = ((float)i / (float)(numFrames - 1));

				cachedVertices[i] = LoadFrameData(i + startFrame).ToList().Find( e => e.id == id).position;
				linearCachedVertices[i] = Vector3.Lerp(first, last, t);
			}
		}

		TrajectoryData trajectory = new TrajectoryData();

		trajectory.id = id;
		trajectory.reactionId = reactionId;
		trajectory.startFrame = startFrame;
		trajectory.endFrame = endFrame;

		trajectory.mesh = new Mesh();
		trajectory.indices = new int[numFrames];
		trajectory.vertices = new Vector3[numFrames];

		for(int i = 0; i < numFrames; i++)
		{
			trajectory.indices[i] = i;
			trajectory.vertices[i] = Vector3.Lerp(cachedVertices[i], linearCachedVertices[i], trajectoryLOD);
		}
		
		trajectory.mesh.vertices = trajectory.vertices;
		trajectory.mesh.SetIndices(trajectory.indices, MeshTopology.LineStrip, 0);
		
		return trajectory;
	}
	
//	TrajectoryData LoadTrajectory (int id, int reactionId, int startFrame, int endFrame, int samplingFrequency, bool resetCachedVertices = false)
//	{	
//		if(startFrame >= endFrame) throw new Exception("End frame cannot be smaller than start frame");
//		if(endFrame > maxFrames) throw new Exception("Sampling interval exceeds max frames");
//
//		TrajectoryData trajectory = new TrajectoryData();
//
//		int numFrames = (endFrame - startFrame) + 1;
//
//		int numSampledFrames = 1 + (numFrames - 1) / samplingFrequency;
//		int numIntervals = numSampledFrames - 1;
//		int numInterpolations = numFrames - numSampledFrames;
//		int interpolationSteps = (int)Math.Round((float)numInterpolations / (float)numIntervals);
//		numInterpolations = numIntervals * interpolationSteps; 	
//		int numSteps = numSampledFrames + numInterpolations;
//
//		Debug.Log("numSampledFrames: " + numSampledFrames + " interpolationSteps: " + interpolationSteps + " numInterpolations: " + numInterpolations);		
//		Debug.Log("Frames: " + numFrames + " Vertices: " + numSteps);
//
//		if(numFrames < numSteps)
//			Debug.LogWarning("Trajectory too long"); 
//
//		if(numFrames > numSteps)
//			Debug.LogWarning("Trajectory too short");
//		
//		trajectory.vertices = new Vector3[numSteps];
//
//		if(resetCachedVertices)
//		{
//			cachedVertices = new Vector3[numFrames];
//
//			//Fill in with trajectory vertices
//			for(int i = 0; i < numFrames; i++)
//			{
//				cachedVertices[i] = LoadFrameData(i + startFrame).ToList().Find( e => e.id == id).position;
//			}
//		}
//
//		int stepCount = 0;
//		int lastSampledFrame = 0;
//
//		for(int samplingCount = 0; samplingCount < numIntervals; samplingCount++)
//		{
////			Debug.Log("--------------");
////			Debug.Log("Sampling count: " + samplingCount);
//			
//			int currentFrame = samplingCount * samplingFrequency;
//			int nextFrame = currentFrame + samplingFrequency;
//
//			// If this is the last sampling interval use the end frame as the next frame
//			if(samplingCount == numSampledFrames - 2) nextFrame = numFrames - 1;
//
////			Debug.Log("Current frame: " + currentFrame);
////			Debug.Log("Next frame: " + nextFrame);
//			
//			Vector3 current = cachedVertices[currentFrame];
//			Vector3 next = cachedVertices[nextFrame];
//			
////			Debug.Log("Step count: " + stepCount);
//			trajectory.vertices[stepCount] = current;
//			stepCount ++;
//			
////			Debug.Log(">>>>>>>>>>");
//			for(int interpolationCount = 0; interpolationCount < interpolationSteps; interpolationCount++)
//			{
////				Debug.Log("Interpolation count: " + interpolationCount);
//				
//				float t = (float)(interpolationCount + 1) / (float)(samplingFrequency + 1);
//				
////				Debug.Log("Step count: " + stepCount);
//				trajectory.vertices[stepCount] = Vector3.Lerp(current, next,t);
//				stepCount ++;
//			}
////			Debug.Log(">>>>>>>>>>");
//			
////			Debug.Log("Step count: " + stepCount);
//			trajectory.vertices[stepCount] = next;
//
//			lastSampledFrame = nextFrame;
//		}
//
//		Debug.Log("Last sampled frame: " + lastSampledFrame);
//
//		trajectory.mesh = new Mesh();
//		trajectory.indices = new int[numSteps];
//		
//		Vector3 min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
//		Vector3 max = new Vector3(-float.MaxValue, -float.MaxValue, -float.MaxValue);
//		
//		for (int i = 0; i < numSteps; i++)
//		{
//			min = Vector3.Min(trajectory.vertices[i], min);
//			max = Vector3.Max(trajectory.vertices[i], max);
//		
//			trajectory.indices[i] = i;
//		}
//		
//		trajectory.mesh.vertices = trajectory.vertices;
//		trajectory.mesh.SetIndices(trajectory.indices, MeshTopology.LineStrip, 0);
//	
//		trajectory.reactionId = reactionId;
//		trajectory.target = min + (max - min) * 0.5f;
//		trajectory.startFrame = startFrame;
//		trajectory.endFrame = endFrame;
//
//		return trajectory;
//	}
}
