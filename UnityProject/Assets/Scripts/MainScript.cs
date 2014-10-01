using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEngine;
using Winterdom.IO.FileMap;

public struct TrajectoryData 
{	
	public int id;
	public int reactionId;
	public int startFrame;
	public int endFrame;
	public int frameCount;
	
	public Mesh mesh;	
	public int[] indices;
	public Vector3[] vertices;
}

public struct ParticleFrameData 
{
	public int id;
	public int type;
	public Vector3 position;
	public Vector3 orientation;
	public bool surface;
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
	const int FRAME_PARTICLE_SIZE = 32;
	const int SYSTEM_GRANULARITY = 65536;
	const int SCALING_FACTOR = 25;
	const double TIME_STEP = 0.00000005;
	
	const string viz_data_path = @"MCell\viz_data\data.bin";
	const string viz_data_index_path = @"MCell\viz_data\index.bin";
	const string rxn_data_path = @"MCell\rxn_data\reactions.txt";
	
	/*****/	
	
	int frameCount = 0;	
	const int particeCount = 4000;
	const int maxParticleId = 6000;
	
	int currentFrame = 0;
	int previousFrame = -1;	
	
	float progress = 0;
	float elapsedTimeSinceLastFrame = 0.0f;
	
	ulong[] frameIndices;
	byte[] tempBuffer;
	
	MemoryMappedFile map;
	
	/*****/
	
	ReactionData[] reactions;
	int[] reactionFrameStart;
	int[] reactionFrameEnd;
	
	/*****/
	
	int[] frameTypes = new int[maxParticleId]; 
	int[] frameAngles = new int[maxParticleId]; 
	int[] frameStates = new int[maxParticleId]; 
	bool[] frameSurfaceTypes = new bool[maxParticleId];
	bool[] frameEnabledStates = new bool[maxParticleId]; 
	Vector3[] framePositions = new Vector3[maxParticleId];
	Vector3[] frameOrientations = new Vector3[maxParticleId];
	
	bool[] reactingFlags = new bool[maxParticleId];
	float[] currentAngles = new float[maxParticleId];
	int[] slowDownCount = new int[maxParticleId];
	
	/*****/
	
	Vector4[] tempPos = new Vector4[4000];		
	Vector4[] tempRot = new Vector4[4000];		
	int[] tempType = new int[4000];
	int[] tempState = new int[4000];
	
	/*****/
	
	int[] trajectoryIndices;
	Mesh trajectoryMesh;
	Vector3[] trajectoryVertices;
	int trajectoryObjectId = 1;
	
	/*****/
	
	public Texture blackStripe;
	public GameObject trajectoryHelper;
	public Material trajectoryMaterial;
	
	public float abstractionMin = 0.0f;
	public float innerSphere = 10.0f;
	public float outerSphere = 25.0f;
	
	[RangeAttribute(0, 1)]
	public float abstractionLevel = 1.0f;
	
	[RangeAttribute(0, 100)]
	public float angularDrag = 1.0f;
	
	[RangeAttribute(0, 100)]
	public float colliderRadius = 1.0f;
	
	[RangeAttribute(0, 30)]
	public float drag = 10.0f;
	
	[RangeAttribute(0, 0.1f)]
	public float jitterForce = 0;
	
	[RangeAttribute(0, 1)]
	public float molScale = 0.015f;
	
	[RangeAttribute(0, 100)]
	public float scaleTorque = 10.0f;
	
	[RangeAttribute(1, 1000)]
	public int stepIncrease = 1;
	
	[RangeAttribute(0.0f, 10.0f)]
	public float lensCutoff = 2;
	
	
	public bool enableDOF = false;
	public bool enableLens = false;
	public bool showTrajectory = false;
	public bool splitScreen = false;
	public bool usePhysics = true;
	
	/*****/
	
	bool init = false;
	bool pause = true;
	bool resetCurrentPositions = true;
	
	int previousReactionIndex = 0;
	
	GameObject camera;
	GameObject target;
	
	List<int> ongoingReactions = new List<int>();
	
	GameObject[] molObjects;
	bool[] molObjectsActiveState = new bool[maxParticleId]; 
	
	/*****/    
	
	void readReactionData()
	{
		// Read reaction data
		List<ReactionData> reactionList = new List<ReactionData>();
		
		StreamReader reader = File.OpenText(rxn_data_path);
		string line;
		
		while ((line = reader.ReadLine()) != null)
		{
			string[] fields = line.Split(' ');
			
			ReactionData reactionData = new ReactionData();
			reactionData.frame = int.Parse(fields[0]);
			
			if(reactionData.frame >= frameCount) break;
			
			reactionData.time = float.Parse(fields[1]);
			reactionData.position = Quaternion.Euler(-90,0,0) * new Vector3(-float.Parse(fields[2]), float.Parse(fields[3]), -float.Parse(fields[4])) * SCALING_FACTOR;
			reactionData.position.y *= -1;
			reactionData.type = fields[5];
			reactionData.reactants = new int[2];
			reactionData.products = new int[1];
			
			reactionList.Add(reactionData);
		}		
		reactions = reactionList.ToArray();
		
		buildReactionMaps();
		
		for(int i = 1; i < reactionFrameStart.Length; i++)
		{
			if(reactionFrameStart[i] == -1) continue;
			
			int NUM_REACTIONS = (reactionFrameEnd[i] - reactionFrameStart[i]) + 1;			
			Debug.Log ("Frame: " + i + " Reaction count: " + NUM_REACTIONS);
			
			var previousFrameData = LoadFrameData(i-1).ToList();
			var reactionFrameData = LoadFrameData(i).ToList();	
			
			int[] previousSortedIDs = previousFrameData.Select(e => e.id).OrderBy(e => e).ToArray();
			int[] reactionSortedIDs = reactionFrameData.Select(e => e.id).OrderBy(e => e).ToArray();
			
			List<int> reactants = new List<int>();
			for(int j = 0; j < NUM_REACTIONS; j++) 
			{
				reactants.Add(-1);
			}
			
			List<int> products = new List<int>();
			for(int j = 0; j < NUM_REACTIONS; j++) 
			{
				products.Add(-1);
			}
			
			List<int> partners = new List<int>();
			for(int j = 0; j < NUM_REACTIONS; j++) 
			{
				partners.Add(-1);
			}
			
			// Find reactants
			
			int a = 0;
			int b = 0;
			int c = 0;
			
			while(a < previousSortedIDs.Length)
			{
				while(b < reactionSortedIDs.Length)
				{
					if(previousSortedIDs[a] == reactionSortedIDs[b]) 
					{
						b++;
						break;
					}
					
					if(previousSortedIDs[a] < reactionSortedIDs[b])
					{
						int ind = -1;
						
						int reactant = previousSortedIDs[a];
						int reactantIndex = previousFrameData.FindIndex( e => e.id == reactant);
						
						float reactionDistance = float.MaxValue;
						int reaction = -1;
						
						for(int j = 0; j < NUM_REACTIONS; j++)
						{
							if(reactants[j] != -1) continue;
							
							ind = reactionFrameStart[i]+j;
							
							float d = Vector3.Distance(previousFrameData[reactantIndex].position, reactions[ind].position);
							if(d < reactionDistance)
							{
								reactionDistance = d;
								reaction = j;
							}
						}
						
						reactants[reaction] = reactant;
						//						Debug.Log("Reactant reaction distance: " + reactionDistance);
						
						c++;
						break;
					}
					b++;
				}
				a++;
			}
			
			if(c != NUM_REACTIONS)
			{
				Debug.LogError("Some fucked up thing just happened");
			}
			
			if(reactants.Contains(-1))
			{
				Debug.LogError("Found empty entries in reactants");
			}
			
			if(reactants.Distinct().Count() != reactants.Count())
			{
				Debug.LogError("Found duplicates in reactants");
			}
			
			// Find products
			
			a = reactionSortedIDs.Length-1;
			b = previousSortedIDs.Length-1;
			c = 0;
			
			while(a > 0)
			{
				while(b > 0)
				{
					if(reactionSortedIDs[a] == previousSortedIDs[b]) 
					{
						b--;
						break;
					}
					
					if(reactionSortedIDs[a] > previousSortedIDs[b])
					{
						int ind = -1;
						
						int product = reactionSortedIDs[a];
						int productIndex = reactionFrameData.FindIndex( e => e.id == product);
						
						float reactionDistance = float.MaxValue;
						int reaction = -1;
						
						for(int j = 0; j < NUM_REACTIONS; j++)
						{
							if(products[j] != -1) continue;
							
							ind = reactionFrameStart[i]+j;
							
							float d = Vector3.Distance(reactionFrameData[productIndex].position, reactions[ind].position);
							if(d < reactionDistance)
							{
								reactionDistance = d;
								reaction = j;
							}
						}
						
						products[reaction] = product;
						//						Debug.Log("Product reaction distance: " + reactionDistance);
						
						c++;
						break;
					}					
					b--;
				}				
				a--;
			}	
			
			if(c != NUM_REACTIONS)
			{
				Debug.LogError("Some fucked up thing just happened");
			}
			
			if(products.Contains(-1))
			{
				Debug.LogError("Found empty entries in products");
			}
			
			if(products.Distinct().Count() != products.Count())
			{
				Debug.LogError("Found duplicates in products");
			}
			
			// Find partners
			
			for(int j = 0; j < NUM_REACTIONS; j++)
			{
				int partner = -1;
				float partnerDistance = float.MaxValue;
				
				int ind = reactionFrameStart[i]+j;
				
				for(int kk = 0; kk < previousFrameData.Count; ++kk)
				{
					if(previousFrameData[kk].type != 2) continue;
					if(partners.Contains(previousFrameData[kk].id)) continue;
					
					float d = Vector3.Distance(previousFrameData[kk].position, reactions[ind].position);
					
					if(d < partnerDistance)
					{
						partner = previousFrameData[kk].id;
						partnerDistance = d;
					}
				}	
				partners[j] = partner;
				//				Debug.Log("Partner reaction distance: " + partnerDistance);
			}
			
			if(partners.Contains(-1))
			{
				Debug.LogError("Found empty entries in partners");
			}
			
			if(partners.Distinct().Count() != partners.Count())
			{
				Debug.LogError("Found duplicates in partners");
			}
			
			for(int j = 0; j < NUM_REACTIONS; j++)
			{
				int ind = reactionFrameStart[i]+j;
				
				reactions[ind].reactants[0] = reactants[j];
				reactions[ind].reactants[1] = partners[j];
				reactions[ind].products[0] = products[j];
				
				Debug.Log("Reaction: " + ind + " reactant: " + reactants[j] + " partner: " + partners[j] + " product: " + products[j]);
			}
		}
		
		var serializer = new XmlSerializer(typeof(ReactionData[]));
		var stream = new FileStream(Path.Combine(Application.persistentDataPath, "reactions.xml"), FileMode.Create);
		serializer.Serialize(stream, reactions);
		stream.Close();
	}
	
	void buildReactionMaps()
	{
		for(int i = 0; i < reactions.Length; i++)
		{
			if(reactionFrameStart[reactions[i].frame] == -1)
			{
				reactionFrameStart[reactions[i].frame] = i;
				reactionFrameEnd[reactions[i].frame] = i;
			}
			else
			{
				reactionFrameEnd[reactions[i].frame] ++;
			}
		}
	}
	
	void Start ()
	{
		molObjects = new GameObject[maxParticleId];
		
		for(int i = 0; i < maxParticleId; i++)
		{
			molObjects[i] = new GameObject();
			molObjects[i].hideFlags = HideFlags.HideAndDontSave;
			//			molObjects[i].transform.rotation = UnityEngine.Random.rotation;
			
			var r = molObjects[i].AddComponent<Rigidbody>();
			r.useGravity = false;
			r.isKinematic = false;
			
			var c = molObjects[i].AddComponent<SphereCollider>();
			c.enabled = false;
			c.radius = colliderRadius;
			
			currentAngles[i] = UnityEngine.Random.Range(-180, 180);
		}
		
		camera = GameObject.Find("Main Camera");
		target = GameObject.Find("Target");
		
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
		
		// Declare and init reactionPerFrame array
		reactionFrameStart = new int[frameCount];
		reactionFrameEnd = new int[frameCount];
		for(int i = 0; i < frameCount; i++) 
		{
			reactionFrameStart[i] = -1;
			reactionFrameEnd[i] = -1;
		}
		
		//		if(false)
		if(File.Exists(Path.Combine(Application.persistentDataPath, "reactions.xml")))
		{
			var serializer = new XmlSerializer(typeof(ReactionData[]));
			var stream = new FileStream(Path.Combine(Application.persistentDataPath, "reactions.xml"), FileMode.Open);
			reactions = serializer.Deserialize(stream) as ReactionData[];
			stream.Close();
			
			buildReactionMaps();
		}
		else
		{
			readReactionData();
		}
		
		trajectoryMesh = new Mesh();
		trajectoryIndices = new int[1000];
		trajectoryVertices = new Vector3[1000];
		
		for(int i = 0; i < 1000; i++)
		{
			trajectoryIndices[i] = i;
			
			ParticleFrameData[] reactionFrameData = LoadFrameData(i);
			int index = reactionFrameData.ToList().FindIndex( e => e.id == trajectoryObjectId);
			trajectoryVertices[i] = reactionFrameData[index].position;
		}
		
		trajectoryMesh.vertices = trajectoryVertices;
		trajectoryMesh.SetIndices(trajectoryIndices, MeshTopology.LineStrip, 0);
	}
	
	void OnApplicationQuit()
	{
		foreach(var gameObject in molObjects)
		{
			DestroyImmediate (gameObject, true);
		}
		
		var objects = GameObject.FindObjectsOfType<GameObject>();
		foreach (var o in objects)
		{
			DestroyImmediate(o);
		}
		
		DestroyImmediate(trajectoryMesh);
		//		DestroyImmediate(trajectoryMaterial);
		
		GC.Collect();
		
		Resources.UnloadUnusedAssets();
		UnityEditor.EditorUtility.UnloadUnusedAssets();
		UnityEditor.EditorUtility.UnloadUnusedAssetsIgnoreManagedReferences();
		
	}
	
	void OnGUI () 
	{
		GUI.contentColor = Color.black;
		//		GUILayout.Label("Current frame: " + currentFrame);
		//		GUILayout.Label("Current time: " + (double)currentFrame * TIME_STEP);
		
		float newProgress = GUI.HorizontalSlider(new Rect(25, Screen.height - 10, Screen.width - 50, 30), progress, 0.0F, 1.0F);
		
		if(progress != newProgress)
		{
			currentFrame = (int)(((float)frameCount - 1.0f) * newProgress);
			resetCurrentPositions = true;
		}
		
		if(splitScreen)
		{
			GUI.DrawTexture(new Rect(Screen.width * 0.5f - 5.0f, 0.0f, 10.0f, Screen.height), blackStripe);
		}
		
		//		float newAbstrationLevel = GUI.VerticalSlider(new Rect(Screen.width - 25, 25, 30, Screen.height - 50), abstractionLevel, 1, 0);
		//		
		//		if(abstractionLevel != newAbstrationLevel)
		//		{
		//			abstractionLevel = newAbstrationLevel;
		//		}
	}
	
	
	void Update () 
	{		
		progress = (float)currentFrame / (float)frameCount;
		
		// If there is a new frame to display
		if(currentFrame != previousFrame )
		{					
			// Reset the frame positions array
			for(int i = 0; i< framePositions.Length; i++)
			{
				reactingFlags[i] = false;
				frameEnabledStates[i] = false;
//				frameAngles[i] = UnityEngine.Random.Range(-180, 180);
			}
			
			var frameData = LoadFrameData(currentFrame);
			
			// Fill up the frame positions array 
			for(int i = 0; i< frameData.Length; i++)
			{
				frameEnabledStates[frameData[i].id] = true;
				frameTypes[frameData[i].id] = frameData[i].type;
				frameSurfaceTypes[frameData[i].id] = frameData[i].surface;
				framePositions[frameData[i].id] = frameData[i].position;
				frameOrientations[frameData[i].id] = frameData[i].orientation;
				
				if(frameSurfaceTypes[frameData[i].id])
				{
					molObjects[frameData[i].id].rigidbody.isKinematic = true;
				}
			}
			
			// Reset the current mol positions to the frame positions
			if(resetCurrentPositions || progress == 0)
			{
				resetCurrentPositions = false;
				ongoingReactions.Clear();
				
				for(int i = 0; i< framePositions.Length; i++)
				{
					molObjectsActiveState[i] = frameEnabledStates[i];
					
					if(molObjectsActiveState[i])
					{
						molObjects[i].transform.position = framePositions[i];
						//						molObjects[i].transform.rotation = Quaternion.FromToRotation(Vector3.forward, frameOrientations[i]);
					}
					
					molObjects[i].collider.enabled = false;
					molObjects[i].tag = "Untagged";
					frameStates[i] = 0;
					slowDownCount[i] = 0;
				}
				
				
			}
			// Trigger reactions
			else
			{
				for(int f = previousFrame+1; f <= currentFrame; f++)
				{
					if(reactionFrameStart[f] == -1) continue;
					
					int numReactions = (reactionFrameEnd[f] - reactionFrameStart[f]) + 1;
					
					for(int r = 0; r < numReactions; r++)
					{
						int currentReactionIndex = reactionFrameStart[f] + r;
						
						if(ongoingReactions.Count != 0 && currentReactionIndex != previousReactionIndex +1)
							Debug.Log("Reaction index mismatch: " + currentReactionIndex + " / " + previousReactionIndex);
						
						previousReactionIndex = currentReactionIndex;
						ReactionData reaction = reactions[currentReactionIndex];
						
						int reactant1 = reaction.reactants[0];
						int reactant2 = reaction.reactants[1];
						
						frameStates[reactant1] = frameStates[reactant2] = 1;
						
						// Tag reacting elements
						molObjects[reactant1].tag = "R1"; 
						molObjects[reactant2].tag = "R2";
						
						ongoingReactions.Add(currentReactionIndex);
						
						Debug.Log("Start reaction: " + currentReactionIndex);
					}
				}
			}
			
			int reactingElements = 0;
			for(int i = 0; i< framePositions.Length; i++)
			{
				if (molObjectsActiveState[i] && !frameEnabledStates[i])
					reactingElements ++;
			}
			
			if(reactingElements != ongoingReactions.Count)
			{
				Debug.LogError("Mismatch between reacting elements and reactions");
			}
			
			previousFrame = currentFrame;
		}
		
		if(showTrajectory)
		{
			if(!trajectoryHelper.activeSelf)
			{
				trajectoryHelper.SetActive(true);
			}
			
			Graphics.DrawMesh(trajectoryMesh, Vector3.zero, Quaternion.identity, trajectoryMaterial, 0);
			trajectoryHelper.transform.position = framePositions[trajectoryObjectId];
		}
		else
		{
			if(trajectoryHelper.activeSelf)
			{
				trajectoryHelper.SetActive(false);
			}
		}
		
		
		if (Input.GetKeyDown(KeyCode.Space)) pause = !pause;
		
		//		if(!pause)
		//		{
		//			elapsedTimeSinceLastFrame += Time.deltaTime;
		//			
		//			if(elapsedTimeSinceLastFrame > frameDuration)
		//			{
		//				elapsedTimeSinceLastFrame = 0;
		//				currentFrame += stepIncrease;
		//			}						
		//		}
		
		if(Input.GetKeyDown(KeyCode.N))
		{
			currentFrame += stepIncrease;
			elapsedTimeSinceLastFrame = 0;
		}
		
		if (currentFrame > frameCount-1) currentFrame = 0; 
		if (currentFrame < 0) currentFrame = frameCount - 1; 
		
		init = true;
	}
	
	float getLocalAbstraction (Vector3 position)
	{
		float localAbstraction = -1;
		
		if(enableLens)
		{
			float mag = Vector3.Distance(targetPosition, position);
			
			float f = (mag - innerSphere) / (outerSphere - innerSphere);
			float lensFactor = (mag <= innerSphere) ? 0: (mag < outerSphere) ? Mathf.Pow(f, lensCutoff) : 1; 
			
			localAbstraction = lensFactor * (1- abstractionMin) + abstractionMin;
		}
		else
		{
			localAbstraction = (1-abstractionLevel) * (1- abstractionMin) + abstractionMin;
		}
		
		if(splitScreen)
		{
			float d = Vector3.Dot(cameraRight, position - cameraPosition); 
			localAbstraction = (d > 0) ? localAbstraction : 1;
		}				
		
		return localAbstraction;
	}
	
	Vector3 targetPosition;
	Vector3 cameraRight;
	Vector3 cameraPosition;
	Vector3 forward;
	
	public bool enableAO = true;
	
	[RangeAttribute(0.1f, 10.0f)]
	public float mass = 1;
	
	//	[RangeAttribute(-180, 180)]
	//	public float x = 0;
	//
	//	[RangeAttribute(-180, 180)]
	//	public float y = 0;
	//
	//	[RangeAttribute(-180, 180)]
	//	public float z = 0;
	
//	[RangeAttribute(0, 1)]
//	public float boost1 = 0.75f;
//	
//	[RangeAttribute(0, 1)]
//	public float boost2 = 0.75f;
//	
//	[RangeAttribute(0, 1)]
//	public float boost3 = 0.4f;
//	
//	[RangeAttribute(0, 1)]
//	public float boost4 = 0.4f;
	
	[RangeAttribute(0, 10)]
	public int nextFrameCount = 2;
	
	[RangeAttribute(0, 100)]
	public float maximumScreenSpeed = 10.0f;

	[RangeAttribute(0, 0.1f)]
	public float extraWurst = 0.01f;

	[RangeAttribute(0, 0.1f)]
	public float attractionBoost1 = 0.025f;

	[RangeAttribute(0, 0.1f)]
	public float attractionBoost2 = 0.025f;

//	[RangeAttribute(0, 100)]
//	public float limitDistance = 10.0f;

	[RangeAttribute(0, 50)]
	public float minAngle = 10.0f;
	
	[RangeAttribute(0, 50)]
	public float maxAngle = 30.0f;



	public bool enableMVS = true;

	int fixedUpdateCount = 0;

	void FixedUpdate () 
	{		
		if(!init) return;
		
		if(!pause)
		{
			fixedUpdateCount ++;
			
			if(fixedUpdateCount >= nextFrameCount)
			{
				currentFrame += stepIncrease;
				fixedUpdateCount = 0;
				
				if (currentFrame > frameCount-1) currentFrame = 0; 
				if (currentFrame < 0) currentFrame = frameCount - 1; 
			}
			
			//			Debug.Log(fixedUpdateCount);
		}
		
		Vector3 position;
		Quaternion rotation;
		
		Rigidbody rb;
		SphereCollider sc;
		
		targetPosition = this.target.transform.position;
		cameraRight = this.camera.transform.right;
		cameraPosition = this.camera.transform.position;
		forward = Vector3.forward;
		
		Vector3 v = new Vector3();
		
		int counter = 0;
		float localAbstraction = 0;
		
		// Update reacting elements
		
		foreach (var reactionId in ongoingReactions.ToArray())
		{
			ReactionData reaction = reactions[reactionId];
			int reactant = reaction.reactants[0];
			int partner = reaction.reactants[1];
			int product = reaction.products[0];
			
			float distance = Vector3.Distance(molObjects[reactant].transform.position, molObjects[partner].transform.position);

//			if(true)
			if (distance < 0.1f)
			{
				molObjects[product].transform.position = molObjects[partner].transform.position;
				molObjects[product].transform.rotation = molObjects[reactant].transform.rotation;
				
				molObjectsActiveState[reactant] = false;
				molObjectsActiveState[product] =  true;
				
				//				slowDownCount[product] = 1;
				
				framePositions[product] = molObjects[partner].transform.position;
				
				// Untag reaction elements
				molObjects[reactant].tag = molObjects[partner].tag = "Untagged";
				
				// Restore state
				frameStates[reactant] = frameStates[partner] = 0;
				ongoingReactions.Remove(reactionId);
				
				//				Debug.Log("End reaction: " + reactionId);
			}
			else
			{							
//								reactingFlags[reactant] = true;
				//				v = (molObjects[reactant].transform.position - molObjects[partner].transform.position);
				//				molObjects[reactant].rigidbody.position -= v * getLocalAbstraction(molObjects[reactant].transform.position) * boost1 + v.normalized * boost2;
				
				framePositions[reactant] = molObjects[partner].transform.position;				
				molObjects[reactant].rigidbody.position += (molObjects[partner].transform.position - molObjects[reactant].rigidbody.position) * attractionBoost1;
				molObjects[partner].rigidbody.position += (reaction.position - molObjects[partner].rigidbody.position) * attractionBoost2;
			}
		}
		
		// Update all elements
		
		for(int i = 0; i < molObjects.Length; i++)
		{
			if(!molObjectsActiveState[i]) continue;
			
			position = molObjects[i].rigidbody.position;
			rotation = molObjects[i].rigidbody.rotation;
			
			rb = molObjects[i].rigidbody;
			rb.drag = drag;
			rb.angularDrag = angularDrag;	
			
//			float mag = Vector3.Distance(targetPosition, position);
			
//			sc = molObjects[i].collider as SphereCollider;
//			if(usePhysics && mag <= innerSphere && frameSurfaceTypes[i])
//			{				
//				sc.enabled = true;
//				sc.radius = colliderRadius;
//			}
//			else
//			{
//				sc.enabled = false;
//			}

			if(!frameSurfaceTypes[i] )
			{
				if(angularDrag != 0) rb.AddTorque(UnityEngine.Random.onUnitSphere * scaleTorque);
			}
			else
			{				
				rb.mass = mass;
				var q = Quaternion.FromToRotation(forward, frameOrientations[i]);

				float angleRange = Mathf.Lerp(minAngle, maxAngle, (float)stepIncrease * 0.01f);

				float deltaAngle = UnityEngine.Random.Range(-angleRange, angleRange);
				currentAngles[i] += deltaAngle; 

				rb.rotation = Quaternion.Euler(-30, 60, currentAngles[i]) * q;
			}
			//
			//			localAbstraction = getLocalAbstraction(position);
			
			//			if(slowDownCount[i] != 0)
			//			{
			//				frameStates[i] = 1;
			//
			//				localAbstraction *= boost3;
			//
			//				slowDownCount[i] ++;
			//
			//				if(slowDownCount[i] > 20) 
			//				{
			//					frameStates[i] = 0;
			//					slowDownCount[i] = 0;
			//				}
			//			}
			
			if(framePositions[i].x == float.MaxValue)
				Debug.LogError("WTF");
			
			if(true)
			{
				v = (framePositions[i] - position);

				localAbstraction = 1;




				if(enableMVS)
				{
					var p = Camera.main.WorldToScreenPoint(position);

					if(splitScreen)
					{
						if(p.x > (float)Screen.width * 0.5f && p.x < Screen.width && p.y > 0.0f && p.y < Screen.height && p.z > 0.0f)
						{
							var f = (Vector2)Camera.main.WorldToScreenPoint(framePositions[i]);
							float sm = Mathf.Round(Vector2.Distance(f,p));
							
							if(sm > maximumScreenSpeed)
							{
								localAbstraction = maximumScreenSpeed / sm;
								localAbstraction += extraWurst;
							}
							
							if(localAbstraction < 0.001f) localAbstraction = 0.0f;
						}
					}
					else
					{
						if(p.x > 0.0f && p.x < (float)Screen.width && p.y > 0.0f && p.y < (float)Screen.height && p.z > 0.0f)
						{
							var f = (Vector2)Camera.main.WorldToScreenPoint(framePositions[i]);
							float sm = Mathf.Round(Vector2.Distance(f,p));
							//						
							if(sm > maximumScreenSpeed)
							{
								localAbstraction = maximumScreenSpeed / sm;
								localAbstraction += extraWurst;
							}
							
							if(localAbstraction < 0.001f) localAbstraction = 0.0f;
						}
					}
				}



				rb.position +=  v * localAbstraction;				
			}
			
			//			rb.position += UnityEngine.Random.onUnitSphere * jitterForce;
			
			if(counter >= 4000)
				throw new Exception("Exceeding number of particles");
			
			//			position = rb.position;
			//			rotation = rb.rotation;
			
			tempPos[counter].x = position.x;
			tempPos[counter].y = position.y;
			tempPos[counter].z = position.z;
			tempPos[counter].w = 1;
			
			tempRot[counter].x = rotation.x;
			tempRot[counter].y = rotation.y;
			tempRot[counter].z = rotation.z;
			tempRot[counter].w = rotation.w;
			
			tempType[counter] = frameTypes[i];
			tempState[counter] = frameStates[i];
			
			if(showTrajectory)
			{
				if(i != trajectoryObjectId)
					tempState[counter] = -1;
			}
			
			counter ++;
		}
		
		if(counter != 4000)
		{
			Debug.Log(counter);
			Debug.LogWarning("Wrong number of displayed elements");
		}
		
		//		GameObject.Find("Target").transform.position = GameObject.Find("Target").transform.position + ( (Vector3)tempPos[0] - GameObject.Find("Target").transform.position) * smooth;
		this.camera.GetComponent<MolScript>().UpdateMols(tempPos, tempRot, tempType, tempState); 
	}
	
	ParticleFrameData[] LoadFrameData (int frame)
	{	
		ParticleFrameData[] frameData = new ParticleFrameData[particeCount];
		
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
		
		byte[] uncompressedFrame = new byte[particeCount * FRAME_PARTICLE_SIZE];
		
		ZLibWrapper.UncompressBuffer(uncompressedFrame, (uint)uncompressedFrame.Length, compressedFrame, (uint)compressedFrameSize);
		
		for (var i = 0; i < particeCount; i++)
		{
			frameData[i].type = (int)BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (0 * sizeof(float)));
			frameData[i].id = (int)BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (1 * sizeof(float)));
			frameData[i].position.x = -BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (2 * sizeof(float)));			
			frameData[i].position.y = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (3 * sizeof(float)));
			frameData[i].position.z = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (4 * sizeof(float)));
			frameData[i].orientation.x = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (5 * sizeof(float)));
			frameData[i].orientation.y = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (6 * sizeof(float)));
			frameData[i].orientation.z = BitConverter.ToSingle(uncompressedFrame, i * FRAME_PARTICLE_SIZE + (7 * sizeof(float)));
			frameData[i].surface = (frameData[i].orientation.x != 0 || frameData[i].orientation.y != 0 || frameData[i].orientation.z != 0);
			
			frameData[i].position = Quaternion.Euler(-90,0,0) * frameData[i].position * SCALING_FACTOR;
			frameData[i].orientation = Quaternion.Euler(-90,0,0) * frameData[i].orientation;
			frameData[i].orientation.y *= -1;
		}	
		
		return frameData;
	}	
}
