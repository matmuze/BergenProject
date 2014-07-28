using UnityEngine;

//[ExecuteInEditMode]
public class MolScript : MonoBehaviour
{
	private int molMax = 10000;
	private int molCount = 10000;

	[RangeAttribute(0.01f,0.5f)]
	public float molScale = 0.01f;

	public Shader shader;	
	private Material mat;
	
	private RenderTexture colorTexture;

	private RenderTexture[] mrtTex;
	private RenderBuffer[] mrtRB;

	private ComputeBuffer cbDrawArgs;
	private ComputeBuffer cbPoints;
	private ComputeBuffer cbAtoms;
	private ComputeBuffer cbPositions;
	private ComputeBuffer cbRotations;	
	private ComputeBuffer cbSpecies;
	private ComputeBuffer cbHighlights;

	public void UpdateMols (Vector4[] positions, Vector4[] rotations, int[] highlights)
	{
		CreateResources();

		if (cbPositions != null)
		{
			cbPositions.SetData(positions);
			cbRotations.SetData(rotations);	
			cbHighlights.SetData(highlights);	
			molCount = positions.Length;
		}
	}

	private void CreateResources ()
	{
		if (cbDrawArgs == null)
		{
			cbDrawArgs = new ComputeBuffer (1, 16, ComputeBufferType.DrawIndirect);
			var args = new int[4];
			args[0] = 0;
			args[1] = 1;
			args[2] = 0;
			args[3] = 0;
			cbDrawArgs.SetData (args);
		}

		if (cbAtoms == null)
		{
			var atoms = PdbReader.ReadPdbFileSimple();

			cbAtoms = new ComputeBuffer (atoms.Count, 16);
			cbAtoms.SetData (atoms.ToArray());
		}
		
		if (cbPositions == null)
		{
			cbPositions = new ComputeBuffer (molMax, 16); 
		}

		if (cbRotations == null)
		{
			cbRotations = new ComputeBuffer (molMax, 16); 
		}

		if (cbSpecies == null)
		{
			cbSpecies = new ComputeBuffer (molMax, 4); 
		}

		if (cbHighlights == null)
		{
			cbHighlights = new ComputeBuffer (molMax, 4); 
		}
		
		if (cbPoints == null)
		{
			cbPoints = new ComputeBuffer (Screen.width * Screen.height, 20, ComputeBufferType.Append);
		}
		
		if (colorTexture == null)
		{
			colorTexture = new RenderTexture (Screen.width, Screen.height, 24, RenderTextureFormat.ARGBFloat);
			colorTexture.filterMode = FilterMode.Point;
			colorTexture.anisoLevel = 1;
			colorTexture.antiAliasing = 1;
			colorTexture.Create();
		}

		if (this.mrtTex == null)
		{
			this.mrtTex = new RenderTexture[2];
			this.mrtRB = new RenderBuffer[2];
			
			this.mrtTex[0] = new RenderTexture (Screen.width, Screen.height, 24, RenderTextureFormat.ARGBFloat);
			this.mrtTex[1] = new RenderTexture (Screen.width, Screen.height, 24, RenderTextureFormat.ARGBFloat);
			
			for( int i = 0; i < this.mrtTex.Length; i++ )
				this.mrtRB[i] = this.mrtTex[i].colorBuffer;			
		}

		if(mat == null)
		{
			mat = new Material(shader);
			mat.hideFlags = HideFlags.HideAndDontSave;
		}
	}
	
	private void ReleaseResources ()
	{
		if (cbDrawArgs != null) cbDrawArgs.Release (); cbDrawArgs = null;
		if (cbPoints != null) cbPoints.Release(); cbPoints = null;
		if (cbAtoms != null) cbAtoms.Release(); cbAtoms = null;
		if (cbPositions != null) cbPositions.Release(); cbPositions = null;
		if (cbRotations != null) cbRotations.Release(); cbRotations = null;
		if (cbSpecies != null) cbSpecies.Release(); cbSpecies = null;
		if (cbHighlights != null) cbHighlights.Release(); cbHighlights = null;
		
		if (colorTexture != null) colorTexture.Release(); colorTexture = null;

		for( int i = 0; i < this.mrtTex.Length; i++ ) 
		{
			if (mrtTex[i] != null) {mrtTex[i].Release(); mrtTex[i]=null;}
		}


		DestroyImmediate (mat);
		mat = null;
	}

	void OnRenderImage(RenderTexture src, RenderTexture dst)
	{
		CreateResources ();

		Graphics.SetRenderTarget (this.mrtTex[0]);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		Graphics.SetRenderTarget (this.mrtTex[1]);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));

		Graphics.SetRenderTarget (this.mrtRB, this.mrtTex[0].depthBuffer);
		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));		
		mat.SetFloat ("molScale", molScale);
		mat.SetBuffer ("molPositions", cbPositions);
		mat.SetBuffer ("molRotations", cbRotations);
		mat.SetBuffer ("molHighlights", cbHighlights);
		mat.SetBuffer ("atomPositions", cbAtoms);
		mat.SetPass(0);
		Graphics.DrawProcedural(MeshTopology.Points, molCount);

		mat.SetTexture ("posTex", mrtTex[0]);
		mat.SetTexture ("infoTex", mrtTex[1]);
		Graphics.SetRandomWriteTarget (1, cbPoints);
		Graphics.Blit (src, dst, mat, 1);
		Graphics.ClearRandomWriteTargets ();		
		ComputeBuffer.CopyCount (cbPoints, cbDrawArgs, 0);
	
		Graphics.SetRenderTarget (src);
//		GL.Clear (true, true, new Color (0.0f, 0.0f, 0.0f, 0.0f));
		mat.SetFloat ("spriteSize", molScale * 1.0f);
		mat.SetColor ("spriteColor", Color.white);
		mat.SetBuffer ("atomPositions", cbPoints);

		mat.SetPass(2);
		Graphics.DrawProceduralIndirect(MeshTopology.Points, cbDrawArgs);

		mat.SetPass(3);
		Graphics.DrawProceduralIndirect(MeshTopology.Points, cbDrawArgs);

		Graphics.Blit (src, dst);
	}

	void OnDisable ()
	{
		ReleaseResources ();
	}
}