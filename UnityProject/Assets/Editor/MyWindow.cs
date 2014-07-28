//using UnityEditor;
//using UnityEngine;
//
//using System;
//using System.IO;
//using System.Text;
//using System.Linq;
//using System.Collections.Generic;
//using System.Runtime.Serialization;
//using System.Runtime.Serialization.Formatters.Binary;
//
//using Winterdom.IO.FileMap;
//
//public class MyWindow : EditorWindow
//{
//	const int FRAME_PARTICLE_SIZE = 32;
//	
//	/*****/
//	
//	int maxFrames = 1000;
//	int maxParticles = 4000;		
//	
//	string dataPath = @"MCell\viz_data\data.bin";
//	string indexPath = @"MCell\viz_data\index.bin";
//	
//	// Add menu item named "My Window" to the Window menu
//	[MenuItem("CellUnity/Show Window")]
//	public static void ShowWindow()
//	{
//		//Show existing window instance. If one doesn't exist, make one.
//		EditorWindow.GetWindow(typeof(MyWindow));
//	}
//	
//	void OnGUI()
//	{
//		GUILayout.Label ("Base Settings", EditorStyles.boldLabel);
//		
//		maxFrames = EditorGUILayout.IntField ("Max Frames", maxFrames);
//		maxParticles = EditorGUILayout.IntField ("Max Particles", maxParticles);
//		//		dataPath = EditorGUILayout.TextField ("Output Data File", dataPath);
//		
//		if(GUILayout.Button ("Convert Frame Data")) 
//		{
//			ConvertFrameData();
//		}
//		
//		if(GUILayout.Button ("Init Scene")) 
//		{
//			InitScene();
//		}
//	}
//	
//	public void InitScene()
//	{
//		if (!File.Exists (dataPath)) 
//		{
//			Debug.LogError ("No data file found at: " + dataPath);
//			return;
//		}
//		
//		GameObject gameObject = GameObject.Find("Main Object");
//		
//		if (gameObject != null)
//			GameObject.DestroyImmediate (gameObject);
//		
//		gameObject = new GameObject ("Main Object");
//		gameObject.AddComponent<MainScript>().Init(maxFrames, maxParticles, dataPath, indexPath);
//	}
//	
//	public void ConvertFrameData()
//	{
//		Debug.Log ("Loading ASCII frame data");
//		
//		DirectoryInfo di = new DirectoryInfo(@"MCell\viz_data");
//		FileSystemInfo[] files = di.GetFileSystemInfos();
//		var frameFileNames = files.Where(x => x.Extension == ".dat").OrderBy (f => f.Name).Select (f => f.FullName).ToArray ();
//		
//		//		for (int i = 0; i < 50; i++) 
//		//		{
//		//			Debug.Log(frameFileNames[i]);
//		//		}
//		
//		if (frameFileNames.Length == 0) 
//		{
//			Debug.LogError ("No frame data found.");
//			return;
//		}
//		
//		ulong[] frameIndices = new ulong[maxFrames];
//		
//		byte[] compressedFrame = new byte[maxParticles * FRAME_PARTICLE_SIZE];
//		byte[] uncompressedFrame = new byte[maxParticles * FRAME_PARTICLE_SIZE];
//		
//		Array.Clear(compressedFrame, 0, compressedFrame.Length);
//		Array.Clear(uncompressedFrame, 0, uncompressedFrame.Length);
//		
//		List<string> molNames = new List<string> ();
//		
//		// Delete previously existing data file
//		if (File.Exists (dataPath))	File.Delete (dataPath);
//		
//		//using (var fileStream = File.Open(dataPath, FileMode.Append))
//		using (BinaryWriter writer = new BinaryWriter(File.Open(dataPath, FileMode.Append)))
//		{		
//			for (int i = 0; i < maxFrames; i++)
//			{
//				using (var reader = new StreamReader(frameFileNames[i]))
//				{
//					for (int j = 0; j < maxParticles; j++) 
//					{
//						string line = reader.ReadLine ();
//						
//						// If no more particles in the frame we fill up the data file with empty particles
//						if (line == null)
//						{					
//							//for(int k = 0; k < 8; k++) writer.Write((Int16)(-1));
//						} 
//						else 
//						{
//							string[] split = line.Split (new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);
//							
//							string name = Convert.ToString (split [0]);					
//							if (!molNames.Contains (name)) molNames.Add (name);
//							
//							//							BitConverter.GetBytes(Convert.ToUInt16(molNames.IndexOf (name))).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 0 * sizeof(UInt16));
//							//							BitConverter.GetBytes(Convert.ToUInt16(split [1])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 1 * sizeof(UInt16));
//							//							Half.GetBytes((Half)Convert.ToSingle(split [2])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 2 * sizeof(UInt16));
//							//							Half.GetBytes((Half)Convert.ToSingle(split [3])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 3 * sizeof(UInt16));
//							//							Half.GetBytes((Half)Convert.ToSingle(split [4])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 4 * sizeof(UInt16));
//							//							Half.GetBytes((Half)Convert.ToSingle(split [5])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 5 * sizeof(UInt16));
//							//							Half.GetBytes((Half)Convert.ToSingle(split [6])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 6 * sizeof(UInt16));
//							//							Half.GetBytes((Half)Convert.ToSingle(split [7])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 7 * sizeof(UInt16));
//							
//							//							BitConverter.GetBytes(Convert.ToUInt32(molNames.IndexOf (name))).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 0 * sizeof(float));
//							//							BitConverter.GetBytes(Convert.ToUInt32(split [1])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 1 * sizeof(float));
//							//BitConverter.GetBytes(Convert.ToSingle(split [2])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 2 * sizeof(float));
//							//BitConverter.GetBytes(Convert.ToSingle(split [3])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 3 * sizeof(float));					
//							//BitConverter.GetBytes(Convert.ToSingle(split [4])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 4 * sizeof(float));							
//							//							BitConverter.GetBytes(Convert.ToSingle(split [5])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 5 * sizeof(float));				
//							//							BitConverter.GetBytes(Convert.ToSingle(split [6])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 6 * sizeof(float));		
//							//							BitConverter.GetBytes(Convert.ToSingle(split [7])).CopyTo(uncompressedFrame, j * FRAME_PARTICLE_SIZE + 7 * sizeof(float));
//						}
//					}
//					
//					int res = ZLibWrapper.CompressBuffer(compressedFrame, uncompressedFrame, (uint)(maxParticles * FRAME_PARTICLE_SIZE));
//					
//					Debug.Log(res);
//					
//					//byte[] compressedFrame = DeflateStream.CompressBuffer(uncompressedFrame);
//					
//					//					// Write frame index
//					//					if(i == 0) frameIndices[i] = (ulong)compressedFrame.Length;
//					//					else frameIndices[i] = frameIndices[i-1] + (ulong)compressedFrame.Length;
//					//
//					////					Debug.Log(frameIndices[i]);
//					//
//					//					writer.Write(compressedFrame);
//				}			
//			}
//		}
//		
//		//		using (BinaryWriter writer = new BinaryWriter(File.Open(indexPath, FileMode.Create))) 
//		//		{			
//		//			byte[] frameIndexBytes = new byte[maxFrames * sizeof(ulong)];
//		//			Buffer.BlockCopy(frameIndices, 0, frameIndexBytes, 0, frameIndexBytes.Length);
//		//			writer.Write(frameIndexBytes);
//		//		}
//	}
//}