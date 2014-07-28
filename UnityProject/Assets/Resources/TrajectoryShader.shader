Shader "Custom/TrajectoryShader" 
{
	Properties 
	{
		_Color ("Color", COLOR) = (1,0,0,1)
	}
	SubShader
	{
		Pass 
	    {		
	    	ZWrite On
	    
			CGPROGRAM			
			
			#include "UnityCG.cginc"
			
			#pragma vertex vert
			#pragma fragment frag
			
			float4 _Color;
			
			float4 vert (appdata_base v) : SV_POSITION
			{
				return mul (UNITY_MATRIX_MVP, v.vertex);
			}			

			void frag (out float4 fragColor : COLOR, out float fragDepth : DEPTH)
			{
				fragColor = _Color;
				fragDepth = 0;
			}			
			
			ENDCG	
		}
	} 
	FallBack "Diffuse"
}
