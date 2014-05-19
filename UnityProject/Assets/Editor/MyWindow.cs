using UnityEditor;
using UnityEngine;

using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;

struct ParticleFrameData
{
	public int type;
	public int id;

	public double posX;
	public double posY;
	public double posZ;

	public double orientX;
	public double orientY;
	public double orientZ;

	public override string ToString()
	{
		return "Type: " + type + " ID: " + id + " Pos X: " + posX + " Pos Y: " + posY + " Pos Z: " + posZ + " Orient X: " + orientX + " Orient Y: " + orientY + " Orient Z: " + orientZ;
	}
}

public class MyWindow : EditorWindow
{

	// Add menu item named "My Window" to the Window menu
	[MenuItem("CellUnity/Show Window")]
	public static void ShowWindow()
	{
		//Show existing window instance. If one doesn't exist, make one.
		EditorWindow.GetWindow(typeof(MyWindow));
	}
	
	void OnGUI()
	{
		if(GUILayout.Button ("Load MCell Scene")) 
		{
			//GameObject.Find("Main Script").GetComponent<MainScript>().LoadScene();

			LoadScene();
		}
	}

	public void LoadScene()
	{
		string[] frameFileNames = Directory.GetFiles (@"MCell\viz_data", "*.dat");

		if (frameFileNames.Length == 0)	return;

//		foreach(string file in frameFileNames)
//		{
//			Debug.Log (file);
//		}

		int counter = 0;
		string line;
		string[] split;


		ParticleFrameData[][] dataHolder = new ParticleFrameData[100][];
		dataHolder[0] = new ParticleFrameData[4000];

		StreamReader reader = new StreamReader(frameFileNames[0]);

		while((line = reader.ReadLine()) != null)
		{
			split = line.Split(new char[] {' '}, StringSplitOptions.RemoveEmptyEntries);

			//dataHolder[0][counter] = new ParticleFrameData();

			dataHolder[0][counter].type = Convert.ToInt32(split[0]);
			dataHolder[0][counter].id = Convert.ToInt32(split[1]);

			dataHolder[0][counter].posX = Convert.ToDouble(split[2]);
			dataHolder[0][counter].posY = Convert.ToDouble(split[3]);
			dataHolder[0][counter].posZ = Convert.ToDouble(split[4]);

			dataHolder[0][counter].orientX = Convert.ToDouble(split[5]);
			dataHolder[0][counter].orientY = Convert.ToDouble(split[6]);
			dataHolder[0][counter].orientZ = Convert.ToDouble(split[7]);

			counter++;
		}

		foreach(ParticleFrameData d in dataHolder[0])
		{
			Debug.Log(d);
		}
	}

	public void LoadScene_old()
	{
//		string text = System.IO.File.ReadAllText(@"MCellData\Scene.molecules.mdl");
//
//		string[] split = text.Split(new char[] {'{','}'}, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where<string>(s => !string.IsNullOrEmpty(s)).ToArray();
//
//		List<string> res = new List<string> ();
//
//		for(int i = 1; i < split.Length; i+=2 )
//		{
//			res.Add(split[i]);
//		}

//		int currentByteIndex = 0;
//
//		// Read bytes from the first frame
//		string[] files = Directory.GetFiles (Directory.GetDirectories (@"MCellData\viz_data").First (), "*.dat");
//		byte[] bytes = File.ReadAllBytes(files.First());
//
//		// Get bin flag
//		int binFlag = BitConverter.ToInt32(bytes, currentByteIndex);
//		currentByteIndex += 4;
//
//		Debug.Log ("Bin flag: " + binFlag);
//
//		if (binFlag == 1) 
//		{
//			while(currentByteIndex < bytes.Length)
//			{
//				// Get string length
//				int stringLength = (int)bytes[currentByteIndex];
//				currentByteIndex += 1;
//				
//				Debug.Log ("String length: " + stringLength);
//				
//				string name = Encoding.UTF8.GetString (bytes.ToList ().GetRange (currentByteIndex, stringLength).ToArray ());
//				currentByteIndex += stringLength;
//				
//				Debug.Log ("Mol name: " + name);
//				
//				// Get surface flag
//				int surfaceFlag = (int)bytes[currentByteIndex];
//				currentByteIndex += 1;
//				
//				Debug.Log ("Surface flag: " + surfaceFlag);
//				
//				// Get float count
//				int floatCount = BitConverter.ToInt32(bytes, currentByteIndex);
//				int particleCount = floatCount / 3;
//				currentByteIndex += 4;
//				
//				Debug.Log ("Particle count: " + particleCount);
//				
//				float[] pos = new float[floatCount];
//				
//				// Get pos values
//				for (int i = 0; i < floatCount; i++) 
//				{
//					//pos[i] = BitConverter.ToSingle(bytes, currentByteIndex + i * 4);
//				}
//				
//				currentByteIndex += floatCount * 4;
//				
//				if (surfaceFlag == 1)
//				{
//					float[] orient = new float[floatCount];
//					
//					// Get pos values
//					for (int i = 0; i < floatCount; i++) 
//					{
//						//orient[i] = BitConverter.ToSingle(bytes, currentByteIndex + i * 4);
//					}
//					
//					currentByteIndex += floatCount * 4;
//				}
//			}
//		}


//		for (int i = 0; i < 10; i++)
//		{
//			Debug.Log ("Debug float: " + floats[i]);
//		}

//		for (int i = 0; i < 50; i++)
//		{
//			Debug.Log("index: " + i);
//			Debug.Log ("byte: " + bytes[i]);
//			Debug.Log ("char: " + Convert.ToChar(bytes[i]));
//			Debug.Log ("-----");
//		}
	}
}