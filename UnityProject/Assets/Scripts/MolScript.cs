using UnityEngine;

using System;
using System.Collections;
using System.Collections.Generic;

//[ExecuteInEditMode]
public class MolScript : MonoBehaviour
{
	private int molCount = 0;
	private float previousScreenWidth = 0;
	private float previousScreenHeight = 0;
	
//	public Material molMaterial;
	public Mesh cellMesh;
	public Material cellMaterial;
	public Material aoMaterial;

	public Shader molShader;	
	private Material molMaterial;

	public Mesh clipingPlane;
	public Material planeMaterial;

	public Shader dofShader;		
	private Material dofMaterial = null;

	private RenderTexture[] mrtTex;
	private RenderBuffer[] mrtRB;

	private RenderTexture color;
	private RenderTexture depth; 
	
	private ComputeBuffer drawArgsBuffer;
	private ComputeBuffer atomDataBuffer;
	private ComputeBuffer atomDataPDBBuffer;
	private ComputeBuffer molAtomCountBuffer;
	private ComputeBuffer molAtomStartBuffer;
	private ComputeBuffer molPositionsBuffer;
	private ComputeBuffer molRotationsBuffer;	
	private ComputeBuffer molStatesBuffer;	
	private ComputeBuffer molTypesBuffer;

	public Color Color1;
	public Color Color2;
	public Color Color3;
	public Color Color4;
	public Color Color5;

	public void Start()
	{
		this.mrtTex = new RenderTexture[2];
		this.mrtRB = new RenderBuffer[2];
		
		if (drawArgsBuffer == null)
		{
			drawArgsBuffer = new ComputeBuffer (1, 16, ComputeBufferType.DrawIndirect);
			var args = new int[4];
			args[0] = 0;
			args[1] = 1;
			args[2] = 0;
			args[3] = 0;
			drawArgsBuffer.SetData (args);
		}
		
		if (atomDataPDBBuffer == null)
		{
			string[] molNames = new string[] { "ATP", "ATP", "2K4T" };//"1OKC" };
			
			List<int> atomCount = new List<int>();
			List<int> atomStart = new List<int>();
			List<Vector4> atomDataPDB = new List<Vector4>();
			
			foreach(var name in molNames)
			{
				Debug.Log ("Loading molecule: " + name);

				var atoms = PdbReader.ReadPdbFile(name);
				
				atomCount.Add(atoms.Count);
				atomStart.Add(atomDataPDB.Count);

				Debug.Log ("Atom count: " + atoms.Count);
				Debug.Log ("Atom start: " + atomDataPDB.Count);

				atomDataPDB.AddRange(atoms);
			}
			
			molAtomCountBuffer = new ComputeBuffer(atomCount.Count, 4);
			molAtomCountBuffer.SetData(atomCount.ToArray());
			
			molAtomStartBuffer = new ComputeBuffer(atomStart.Count, 4);
			molAtomStartBuffer.SetData(atomStart.ToArray());
			
			atomDataPDBBuffer = new ComputeBuffer (atomDataPDB.Count, 16);
			atomDataPDBBuffer.SetData (atomDataPDB.ToArray());
		}
	}
	
	public void UpdateMols (Vector4[] positions, Vector4[] rotations, int[] types, int[] states)
	{
		System.Diagnostics.Debug.Assert(positions.Length - rotations.Length + types.Length - states.Length == 0, "The array of values sent to the renderer do not have the same length");
		System.Diagnostics.Debug.Assert(positions.Length != 0, "The array of values sent to the renderer are empty");		
		
		if(molCount != positions.Length)
		{
			molCount = positions.Length;
			
			if(molPositionsBuffer != null) molPositionsBuffer.Release();			
			molPositionsBuffer = new ComputeBuffer (molCount, 16); 
			
			if(molRotationsBuffer != null) molRotationsBuffer.Release();
			molRotationsBuffer = new ComputeBuffer (molCount, 16); 
			
			if(molTypesBuffer != null) molTypesBuffer.Release();
			molTypesBuffer = new ComputeBuffer (molCount, 4);
			
			if(molStatesBuffer != null) molStatesBuffer.Release();
			molStatesBuffer = new ComputeBuffer (molCount, 4); 
		}
		
		molPositionsBuffer.SetData(positions);
		molRotationsBuffer.SetData(rotations);	
		molTypesBuffer.SetData(types);
		molStatesBuffer.SetData(states);
	}
	
	private void CheckResources ()
	{		
		if(Screen.width != previousScreenWidth || Screen.height != previousScreenHeight)
		{
			if (atomDataBuffer != null) atomDataBuffer.Release(); 
			atomDataBuffer = new ComputeBuffer (Screen.width * Screen.height, 24, ComputeBufferType.Append);
			
			for( int i = 0; i < this.mrtTex.Length; i++ ) 
				if (mrtTex[i] != null) mrtTex[i].Release();
			
			this.mrtTex[0] = new RenderTexture (Screen.width, Screen.height, 24, RenderTextureFormat.ARGBFloat);
			this.mrtTex[1] = new RenderTexture (Screen.width, Screen.height, 24, RenderTextureFormat.ARGBFloat);
			
			for( int i = 0; i < this.mrtTex.Length; i++ )
				this.mrtRB[i] = this.mrtTex[i].colorBuffer;		

			if(color != null) color.Release();
				color = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);

			if(depth != null) depth.Release();
				depth = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.Depth);

			previousScreenWidth = Screen.width;
			previousScreenHeight = Screen.height;
		}
		
		if(molMaterial == null)
		{
			molMaterial = new Material(molShader);
			molMaterial.hideFlags = HideFlags.HideAndDontSave;
		}
		
		if(dofMaterial == null)
		{
			dofMaterial = new Material(dofShader);
			dofMaterial.hideFlags = HideFlags.HideAndDontSave;
		}
	}
	
	private void ReleaseResources ()
	{
		if (drawArgsBuffer != null) drawArgsBuffer.Release (); drawArgsBuffer = null;
		if (atomDataBuffer != null) atomDataBuffer.Release(); atomDataBuffer = null;
		if (atomDataPDBBuffer != null) atomDataPDBBuffer.Release(); atomDataPDBBuffer = null;
		if (molAtomCountBuffer != null) molAtomCountBuffer.Release(); molAtomCountBuffer = null;
		if (molAtomStartBuffer != null) molAtomStartBuffer.Release(); molAtomStartBuffer = null;
		if (molPositionsBuffer != null) molPositionsBuffer.Release(); molPositionsBuffer = null;
		if (molRotationsBuffer != null) molRotationsBuffer.Release(); molRotationsBuffer = null;
		if (molTypesBuffer != null) molTypesBuffer.Release(); molTypesBuffer = null;
		if (molStatesBuffer != null) molStatesBuffer.Release(); molStatesBuffer = null;

		if (color != null) color.Release(); color = null;
		if (depth != null) depth.Release(); depth = null;

		for( int i = 0; i < this.mrtTex.Length; i++ ) 
		{
			if (mrtTex[i] != null) {mrtTex[i].Release(); mrtTex[i]=null;}
		}
		
		DestroyImmediate (dofMaterial);
		dofMaterial = null;
	}
	
	public enum BlurSampleCount
	{
		Low = 0,
		Medium = 1,
		High = 2,
	}	
	
	//	public bool enableDOF = true;	
	public bool visualizeFocus = false;
	public float focalLength = 10.0f;
	public float focalSize = 0.05f; 
	public float aperture = 11.5f;
	public Transform focalTransform = null;
	public float maxBlurSize = 2.0f;
	public bool highResolution = false;	
	
	public BlurSampleCount blurSampleCount = BlurSampleCount.High;	
	public bool nearBlur = false;	
	public float foregroundOverlap = 1.0f;
		
	
	[RangeAttribute(0,50)]
	public float innerSphere = 10;
	
	[RangeAttribute(0,50)]
	public float outerSphere = 20;
	
	private float focalDistance01 = 10.0f;	
	private float internalBlurWidth = 1.0f;
	
	float FocalDistance01 (float worldDist)
	{
		return camera.WorldToViewportPoint((worldDist-camera.nearClipPlane) * camera.transform.forward + camera.transform.position).z / (camera.farClipPlane-camera.nearClipPlane);	
	}
	
	void WriteCoc (RenderTexture fromTo, bool fgDilate, RenderTexture depthTexture)
	{
		dofMaterial.SetTexture("_FgOverlap", null); 
		
		if (nearBlur && fgDilate) 
		{
			int rtW  = fromTo.width/2;
			int rtH  = fromTo.height/2;
			
			// capture fg coc
			
			dofMaterial.SetTexture("_DepthTexture", depthTexture);
			
			RenderTexture temp2 = RenderTexture.GetTemporary (rtW, rtH, 0, fromTo.format);
			Graphics.Blit (fromTo, temp2, dofMaterial, 4); 
			
			// special blur
			float fgAdjustment = internalBlurWidth * foregroundOverlap;
			
			dofMaterial.SetTexture("_DepthTexture", depthTexture);
			
			dofMaterial.SetVector ("_Offsets", new Vector4 (0.0f, fgAdjustment , 0.0f, fgAdjustment));
			RenderTexture temp1 = RenderTexture.GetTemporary (rtW, rtH, 0, fromTo.format);
			Graphics.Blit (temp2, temp1, dofMaterial, 2);
			RenderTexture.ReleaseTemporary(temp2);
			
			dofMaterial.SetTexture("_DepthTexture", depthTexture);
			
			dofMaterial.SetVector ("_Offsets", new Vector4 (fgAdjustment, 0.0f, 0.0f, fgAdjustment));		
			temp2 = RenderTexture.GetTemporary (rtW, rtH, 0, fromTo.format);
			Graphics.Blit (temp1, temp2, dofMaterial, 2);
			RenderTexture.ReleaseTemporary(temp1);
			
			dofMaterial.SetTexture("_DepthTexture", depthTexture);
			
			// "merge up" with background COC
			dofMaterial.SetTexture("_FgOverlap", temp2);
			fromTo.MarkRestoreExpected(); // only touching alpha channel, RT restore expected
			Graphics.Blit (fromTo, fromTo, dofMaterial,  13);
			RenderTexture.ReleaseTemporary(temp2);
		}
		else
		{
			// capture full coc in alpha channel (fromTo is not read, but bound to detect screen flip)
			
			bool d3d = SystemInfo.graphicsDeviceVersion.IndexOf("Direct3D") > -1;
			
			//			Matrix4x4 M = GameObject.Find("Cube1").transform.localToWorldMatrix;
			Matrix4x4 V = Camera.main.worldToCameraMatrix;
			Matrix4x4 P = Camera.main.projectionMatrix;
			
			if (d3d)
			{
				// Invert Y for rendering to a render texture
				for ( int i = 0; i < 4; i++) { P[1,i] = -P[1,i]; }
				// Scale and bias from OpenGL -> D3D depth range
				for ( int i = 0; i < 4; i++) { P[2,i] = P[2,i]*0.5f + P[3,i]*0.5f;}
			}
			
			Matrix4x4 MVP = P*V;	
			
			dofMaterial.SetMatrix("_ViewProjectionInverseMatrix", MVP.inverse);
			dofMaterial.SetVector("_Target", (Vector4)GameObject.Find("Target").transform.position);
//			dofMaterial.SetFloat("_InnerSphere", GameObject.Find("Main Script").GetComponent<MainScript>().innerSphere);
//			dofMaterial.SetFloat("_OuterSphere", GameObject.Find("Main Script").GetComponent<MainScript>().outerSphere);
			dofMaterial.SetInt("_SplitScreen", System.Convert.ToInt32(GameObject.Find("Main Script").GetComponent<MainScript>().splitScreen));
			Graphics.Blit (fromTo, fromTo, dofMaterial,  0);	
		}
	}

	[RangeAttribute(0, 3)]
	public float intensity = 0.5f;

	[RangeAttribute(0.1f,3)]
	public float radius = 0.2f;

	[RangeAttribute(0,2)]
	public float sharpness = 1.0f;

	[RangeAttribute(0,3)]
	public int blurIterations = 1;

	[RangeAttribute(0,5)]
	public float blurFilterDistance = 1.25f;
	
	void OnRenderImage(RenderTexture src, RenderTexture dst)
	{
		if(molCount == 0) 
		{
			Graphics.Blit (src, dst);
			return;
		}

		CheckResources ();


//		var t = GameObject.Find("Clip Plane").transform;
//		float dotProduct = Vector3.Dot(t.transform.up, Camera.main.transform.position - t.position);
//		int planeOrientation = dotProduct < 0 ? 1: 0;
//
//		RenderTexture clipingPlaneColor = RenderTexture.GetTemporary (src.width, src.height, 24, src.format);
//		RenderTexture clipingPlaneDepth = RenderTexture.GetTemporary (src.width, src.height, 24, RenderTextureFormat.Depth);
//
//		Graphics.SetRenderTarget (clipingPlaneColor.colorBuffer, clipingPlaneDepth.depthBuffer);
//		GL.Clear (true, true, Color.black);
//
//		Matrix4x4 M = new Matrix4x4();
//		M.SetTRS(t.position, t.rotation, t.localScale);
//
//		planeMaterial.SetPass(0);
//
//		Graphics.DrawMeshNow(clipingPlane, M);

//		Graphics.Blit (clipingPlaneColor, dst);
//
//		RenderTexture.ReleaseTemporary (clipingPlaneColor);
//		RenderTexture.ReleaseTemporary (clipingPlaneDepth);
//
//		return;

		molMaterial.SetFloat ("molScale", GameObject.Find("Main Script").GetComponent<MainScript>().molScale);
				
		molMaterial.SetColor ("_Color1", Color1);
		molMaterial.SetColor ("_Color2", Color2);
		molMaterial.SetColor ("_Color3", Color3);
		molMaterial.SetColor ("_Color4", Color4);
		molMaterial.SetColor ("_Color5", Color5);

		molMaterial.SetBuffer ("atomDataBuffer", atomDataBuffer);
		molMaterial.SetBuffer ("atomDataPDBBuffer", atomDataPDBBuffer);
		molMaterial.SetBuffer ("molAtomCountBuffer", molAtomCountBuffer);		
		molMaterial.SetBuffer ("molAtomStartBuffer", molAtomStartBuffer);
		molMaterial.SetBuffer ("molPositions", molPositionsBuffer);
		molMaterial.SetBuffer ("molRotations", molRotationsBuffer);
		molMaterial.SetBuffer ("molHighlights", molStatesBuffer);
		molMaterial.SetBuffer ("molTypes", molTypesBuffer);
		
		Graphics.SetRenderTarget (this.mrtTex[0]);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		Graphics.SetRenderTarget (this.mrtTex[1]);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		
		Graphics.SetRenderTarget (this.mrtRB, this.mrtTex[0].depthBuffer);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));		

		molMaterial.SetPass(0);
		Graphics.DrawProcedural(MeshTopology.Points, molCount);
		
		molMaterial.SetTexture ("posTex", mrtTex[0]);
		molMaterial.SetTexture ("infoTex", mrtTex[1]);
		Graphics.SetRandomWriteTarget (1, atomDataBuffer);
		Graphics.Blit (src, dst, molMaterial, 1);
		Graphics.ClearRandomWriteTargets ();		
		ComputeBuffer.CopyCount (atomDataBuffer, drawArgsBuffer, 0);
				
		Graphics.Blit (src, color);

		Graphics.SetRenderTarget (color.colorBuffer, depth.depthBuffer);
		GL.Clear (true, false, Color.white);
		
		molMaterial.SetPass(2);
		Graphics.DrawProceduralIndirect(MeshTopology.Points, drawArgsBuffer);
		
		molMaterial.SetPass(3);
		Graphics.DrawProceduralIndirect(MeshTopology.Points, drawArgsBuffer);
		
		molMaterial.SetTexture ("_InputTex", color);
		molMaterial.SetTexture ("_DepthTex", depth);
		Graphics.Blit (color, src, molMaterial, 5);
		Graphics.Blit (src, color);

//		cellMaterial.SetTexture ("_DepthTex", clipingPlaneDepth);
		cellMaterial.SetInt("_PlaneOrientation", -1);

		Graphics.SetRenderTarget (color.colorBuffer, depth.depthBuffer);
		cellMaterial.SetPass(0);
		Graphics.DrawMeshNow(cellMesh, Matrix4x4.Scale( new Vector3(25,25,25)));

		cellMaterial.SetPass(1);
		Graphics.DrawMeshNow(cellMesh, Matrix4x4.Scale( new Vector3(25,25,25)));

		//*** Do AO here ***//
			
		if(GameObject.Find("Main Script").GetComponent<MainScript>().enableAO) 
		{
			Matrix4x4 P = GameObject.Find("Main Camera").GetComponent<Camera>().projectionMatrix;
			Matrix4x4 invP = P.inverse;
			Vector4 projInfo = new Vector4 ((-2.0f / (Screen.width * P[0])), (-2.0f / (Screen.height * P[5])), ((1.0f - P[2]) / P[0]), ((1.0f + P[6]) / P[5]));
			
			aoMaterial.SetVector ("_ProjInfo", projInfo); // used for unprojection
			aoMaterial.SetMatrix ("_ProjectionInv", invP); // only used for reference
			aoMaterial.SetFloat ("_Radius", radius);
			aoMaterial.SetFloat ("_Radius2", radius*radius);
			aoMaterial.SetFloat ("_Intensity", intensity);
			aoMaterial.SetFloat ("_BlurFilterDistance", blurFilterDistance);
			aoMaterial.SetTexture ("_DepthTexture", depth);
			aoMaterial.SetFloat ("_Sharpness", sharpness);

			int rtW = src.width;
			int rtH = src.height;
			
			RenderTexture tmpRt  = RenderTexture.GetTemporary (rtW, rtH);
			RenderTexture tmpRt2;// = RenderTexture.GetTemporary (rtW, rtH);
			
			Graphics.Blit (color, tmpRt, aoMaterial, 0);
			
			for (int i  = 0; i < blurIterations; i++) 
			{
				aoMaterial.SetVector("_Axis", new Vector2(1.0f,0.0f));
				tmpRt2 = RenderTexture.GetTemporary (rtW, rtH);
				Graphics.Blit (tmpRt, tmpRt2, aoMaterial, 1);
				RenderTexture.ReleaseTemporary (tmpRt);
				
				aoMaterial.SetVector("_Axis", new Vector2(0.0f,1.0f));
				tmpRt = RenderTexture.GetTemporary (rtW, rtH);
				Graphics.Blit (tmpRt2, tmpRt, aoMaterial, 1);
				RenderTexture.ReleaseTemporary (tmpRt2);
			}

			tmpRt2 = RenderTexture.GetTemporary (rtW, rtH);

			aoMaterial.SetTexture ("_AOTex", tmpRt);
			Graphics.Blit (color, tmpRt2, aoMaterial, 2);
			Graphics.Blit (tmpRt2, color);

			RenderTexture.ReleaseTemporary (tmpRt);
			RenderTexture.ReleaseTemporary (tmpRt2);
		}

		//*** Do depth of field here ***//

		if(GameObject.Find("Main Script").GetComponent<MainScript>().enableDOF) 
		{
			if(innerSphere > outerSphere) outerSphere = innerSphere;

			dofMaterial.SetFloat ("_InnerSphere", innerSphere);
			dofMaterial.SetFloat ("_OuterSphere", outerSphere);


			if (aperture < 0.0f) aperture = 0.0f;
			if (maxBlurSize < 0.1f) maxBlurSize = 0.1f;
			focalSize = Mathf.Clamp(focalSize, 0.0f, 2.0f);
			internalBlurWidth = Mathf.Max(maxBlurSize, 0.0f); 
			
			// focal & coc calculations
			
			focalDistance01 = (focalTransform) ? (camera.WorldToViewportPoint (focalTransform.position)).z / (camera.farClipPlane) : FocalDistance01 (focalLength);
			dofMaterial.SetVector ("_CurveParams", new Vector4 (1.0f, focalSize, aperture/10.0f, focalDistance01));
			
			// possible render texture helpers
			
			RenderTexture rtLow  = null;		
			RenderTexture rtLow2  = null;
//			float fgBlurDist = internalBlurWidth * foregroundOverlap;
			
			dofMaterial.SetTexture("_DepthTexture", depth);

			if(visualizeFocus) 
			{
				WriteCoc (color, true, depth);
				Graphics.Blit (color, dst, dofMaterial, 16);
			}
			else 
			{ 		
				color.filterMode = FilterMode.Bilinear;
				
				if(highResolution) internalBlurWidth *= 2.0f;
				
				WriteCoc (color, true, depth);
				
				rtLow = RenderTexture.GetTemporary (color.width >> 1, color.height >> 1, 0, color.format);
				rtLow2 = RenderTexture.GetTemporary (color.width >> 1, color.height >> 1, 0, color.format);
				
				int blurPass = (blurSampleCount == BlurSampleCount.High || blurSampleCount == BlurSampleCount.Medium) ? 17 : 11;
				
				if(highResolution) 
				{
					dofMaterial.SetVector ("_Offsets", new Vector4 (0.0f, internalBlurWidth, 0.025f, internalBlurWidth));
					Graphics.Blit (color, dst, dofMaterial, blurPass);
				}
				else
				{
					dofMaterial.SetVector ("_Offsets", new Vector4 (0.0f, internalBlurWidth, 0.1f, internalBlurWidth));
					
					// blur
					Graphics.Blit (color, rtLow, dofMaterial, 6);
					Graphics.Blit (rtLow, rtLow2, dofMaterial, blurPass);
					
					// cheaper blur in high resolution, upsample and combine
					dofMaterial.SetTexture("_LowRez", rtLow2);
					dofMaterial.SetTexture("_FgOverlap", null);
					dofMaterial.SetVector ("_Offsets",  Vector4.one * ((1.0f*color.width)/(1.0f*rtLow2.width)) * internalBlurWidth);
					Graphics.Blit (color, dst, dofMaterial, blurSampleCount == BlurSampleCount.High ? 18 : 12);
				}
			}

			if(rtLow) RenderTexture.ReleaseTemporary(rtLow);
			if(rtLow2) RenderTexture.ReleaseTemporary(rtLow2);	
		}
		else
		{
			Graphics.Blit (color, dst);
		}					
	}
	
	void OnDisable ()
	{
		ReleaseResources ();
	}
}