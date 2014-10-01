Shader "Custom/MolShader" 
{
//	Properties
//	{
//		_Color1 ("Color1", Color) = (1,1,1,1)
//		_Color2 ("Color2", Color) = (1,1,1,1)
//		_Color3 ("Color3", Color) = (1,1,1,1)
//		_Color4 ("Color4", Color) = (1,1,1,1)		
//		_Color5 ("Color5", Color) = (1,1,1,1)
//	}
	
	CGINCLUDE

	#include "UnityCG.cginc"

	uniform float4 _Color1;
	uniform float4 _Color2;
	uniform float4 _Color3;
	uniform float4 _Color4;	
	uniform float4 _Color5;
	
	float Epsilon = 1e-10;
		
	
	
	float3 RGBtoHCV(in float3 RGB)
	{
		// Based on work by Sam Hocevar and Emil Persson
		float4 P = (RGB.g < RGB.b) ? float4(RGB.bg, -1.0, 2.0/3.0) : float4(RGB.gb, 0.0, -1.0/3.0);
		float4 Q = (RGB.r < P.x) ? float4(P.xyw, RGB.r) : float4(RGB.r, P.yzx);
		float C = Q.x - min(Q.w, Q.y);
		float H = abs((Q.w - Q.y) / (6 * C + Epsilon) + Q.z);
		return float3(H, C, Q.x);
	}
	
	float3 HUEtoRGB(in float H)
	{
		float R = abs(H * 6 - 3) - 1;
		float G = 2 - abs(H * 6 - 2);
		float B = 2 - abs(H * 6 - 4);
		return saturate(float3(R,G,B));
	}
	
	float3 RGBtoHSV(in float3 RGB)
	{
	    float3 HCV = RGBtoHCV(RGB);
	    float S = HCV.y / (HCV.z + Epsilon);
	    return float3(HCV.x, S, HCV.z);
	}
	
	float3 HSVtoRGB(in float3 HSV)
	{
		float3 RGB = HUEtoRGB(HSV.x);
		return ((RGB - 1) * HSV.y + 1) * HSV.z;
	}
	
	float3 HSLtoRGB(in float3 HSL)
  {
    float3 RGB = HUEtoRGB(HSL.x);
    float C = (1 - abs(2 * HSL.z - 1)) * HSL.y;
    return (RGB - 0.5) * C + HSL.z;
  }

	
	float3 RGBtoHSL(in float3 RGB)
	{
		float3 HCV = RGBtoHCV(RGB);
		float L = HCV.z - HCV.y * 0.5;
		float S = HCV.y / (1 - abs(L * 2 - 1) + Epsilon);
		return float3(HCV.x, S, L);
	}
	
	float4 GetColor (in int type, in int state)
	{				
		float4 c = (type == 0) ? _Color1 : (type == 1) ? _Color2 : (type == 2) ? _Color3 : _Color5;
		
		if(state == 1 && type == 2)
		{
			c = _Color4;
//			float3 v = RGBtoHSL(c.xyz);
//			v.z += 0.15;
//			
//			c.xyz = HSLtoRGB(v);
		}
		
		if(state == 1 && type != 2)
		{
			c = _Color5;
//			float3 v = RGBtoHSL(c.xyz);
//			v.z += 0.15;
//			
//			c.xyz = HSLtoRGB(v);
		}
		
		return c;
	}
	
	struct AtomData
	{
	     float x;
	     float y;
	     float z;
	     float s;
	     float h;
	     float t;
	};
																																																																																																																					
	uniform StructuredBuffer<AtomData> atomDataBuffer;
	uniform StructuredBuffer<float4> atomDataPDBBuffer;	
	uniform StructuredBuffer<int> molAtomCountBuffer;										
	uniform StructuredBuffer<int> molAtomStartBuffer;											
	uniform	StructuredBuffer<float4> molPositions;
	uniform	StructuredBuffer<float4> molRotations;			
	uniform	StructuredBuffer<int> molHighlights;
	uniform	StructuredBuffer<int> molTypes;
	
	uniform float molScale;	
	
	ENDCG
	
	SubShader 
	{	
		// First pass
	    Pass 
	    {
	    	CGPROGRAM			
	    		
			#include "UnityCG.cginc"
			
			#pragma only_renderers d3d11
			#pragma target 5.0				
			
			#pragma vertex VS				
			#pragma fragment FS
			#pragma hull HS
			#pragma domain DS	
			#pragma geometry GS			
		
			struct vs2hs
			{
	            float3 pos : CPOINT;
	            float4 rot : COLOR0;
	            float4 info : COLOR1;
        	};
        	
        	struct hsConst
			{
			    float tessFactor[2] : SV_TessFactor;
			};

			struct hs2ds
			{
			    float3 pos : CPOINT;
			    float4 rot : COLOR0;
			    float4 info : COLOR1;
			};
			
			struct ds2gs
			{
			    float3 pos : CPOINT;
			    float4 rot : COLOR0;
			    float4 info : COLOR1;
			    float4 atomData : COLOR2;
			};
			
			struct gs2fs
			{
			    float4 pos : SV_Position;
			    float4 worldPos : COLOR0;
			    float4 info : COLOR1;
			};
			
			float3 qtransform( float4 q, float3 v )
			{ 
				return v + 2.0 * cross(cross(v, q.xyz ) + q.w * v, q.xyz);
			}
				
			vs2hs VS(uint id : SV_VertexID)
			{
			    vs2hs output;
			    
			    output.pos = molPositions[id].xyz;
			    output.rot = molRotations[id];
			    output.info = float4( molHighlights[id], molTypes[id], 0, 0);
			    
			    return output;
			}										
			
			hsConst HSConst(InputPatch<vs2hs, 1> input, uint patchID : SV_PrimitiveID)
			{
				hsConst output;					
				
				float4 transformPos = mul (UNITY_MATRIX_MVP, float4(input[0].pos, 1.0));
				transformPos /= transformPos.w;
				
				float tessFactor = 64;		
					
				if(input[0].info.x == -1 || transformPos.x < -1 || transformPos.y < -1 || transformPos.x > 1 || transformPos.y > 1 || transformPos.z > 1 || transformPos.z < -1 ) 
				{
					output.tessFactor[0] = 0.0f;
					output.tessFactor[1] = 0.0f;
				}		
				else
				{
					output.tessFactor[0] = tessFactor;
					output.tessFactor[1] = tessFactor;					
				}		
				
				return output;
			}
			
			[domain("isoline")]
			[partitioning("integer")]
			[outputtopology("point")]
			[outputcontrolpoints(1)]				
			[patchconstantfunc("HSConst")]
			hs2ds HS (InputPatch<vs2hs, 1> input, uint ID : SV_OutputControlPointID)
			{
			    hs2ds output;
			    
			    output.pos = input[0].pos;
			    output.rot = input[0].rot;
			    output.info = input[0].info;
			    
			    return output;
			} 
			
			[domain("isoline")]
			ds2gs DS(hsConst input, const OutputPatch<hs2ds, 1> op, float2 uv : SV_DomainLocation)
			{
				ds2gs output;	

				int id = (uv.x * input.tessFactor[0] + uv.y * input.tessFactor[0] * input.tessFactor[1]);	
				
				output.pos = op[0].pos;
			    output.rot = op[0].rot;
			    output.info = op[0].info;	
			    output.info.z = id;
																
				return output;			
			}
			
			[maxvertexcount(1)]
			void GS(point ds2gs input[1], inout PointStream<gs2fs> pointStream)
			{
				if(input[0].info.z < molAtomCountBuffer[input[0].info.y])
				{
					float4 atomDataPDB = atomDataPDBBuffer[input[0].info.z + molAtomStartBuffer[input[0].info.y]];	
				
					gs2fs output;
					
					output.worldPos = float4(input[0].pos + qtransform(input[0].rot, atomDataPDB.xyz) * molScale, atomDataPDB.w);
					output.pos = mul(UNITY_MATRIX_MVP, float4(output.worldPos.xyz, 1));
					output.info = input[0].info.xyww;
					
					pointStream.Append(output);
				} 					  					
			}
			
			void FS (gs2fs input, out float4 color1 : COLOR0, out float4 color2 : COLOR1)
			{			
				
					
				color1 = input.worldPos;
				color2 = input.info;
			}
						
			ENDCG
		}
		
		// Second pass
		Pass
		{
			ZWrite Off ZTest Always Cull Off Fog { Mode Off }

			CGPROGRAM
			
			#include "UnityCG.cginc"
				
			#pragma only_renderers d3d11		
			#pragma target 5.0
			
			#pragma vertex vert
			#pragma fragment frag
		
			sampler2D posTex;
			sampler2D infoTex;
			
			AppendStructuredBuffer<AtomData> pointBufferOutput : register(u1);

			struct v2f
			{
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
			};			

			v2f vert (appdata_base v)
			{
				v2f o;
				o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
				o.uv = v.texcoord;
				return o;
			}			

			float4 frag (v2f i) : COLOR0
			{
				float4 pos = tex2D (posTex, i.uv);
				float4 info = tex2D (infoTex, i.uv);
				
				AtomData o;
				o.x = pos.x;
				o.y = pos.y;
				o.z = pos.z;
				o.s = pos.w;
				o.h = info.x;
				o.t = info.y;
				
				[branch]
				if (any(pos > 0))
				{
					pointBufferOutput.Append (o);
				}
				
				discard;
				return pos;
			}			
			
			ENDCG
		}			
		

		// Third pass
		Pass
		{	
			CGPROGRAM	
					
			#include "UnityCG.cginc"			
			
			#pragma vertex VS			
			#pragma fragment FS							
			#pragma geometry GS	
				
			#pragma only_renderers d3d11		
			#pragma target 5.0									
											
			struct vs2gs
			{
				float4 pos : SV_POSITION;
				float4 info: COLOR0;	
			};
			
			struct gs2fs
			{
				float4 pos : SV_POSITION;							
				float4 info: COLOR0;					
			};

			vs2gs VS(uint id : SV_VertexID)
			{
			    AtomData atomData = atomDataBuffer[id];	
			    
			    vs2gs output;	
			    output.pos = mul (UNITY_MATRIX_MV, float4(atomData.x, atomData.y, atomData.z, 1));
			    output.info = float4(atomData.s, atomData.h, atomData.t, 0); 
			    					        
			    return output;
			}
			
			[maxvertexcount(1)]
			void GS(point vs2gs input[1], inout PointStream<gs2fs> pointStream)
			{
				gs2fs output;	
				  	
			  	output.pos = mul (UNITY_MATRIX_P, input[0].pos);							
				output.info = float4(input[0].info.y, input[0].info.z, 0, 0);
					
				pointStream.Append(output);				
			}
			
			float4 FS (gs2fs input) : COLOR
			{			
				return GetColor(round(input.info.y), round(input.info.x));
			}
			
			ENDCG					
		}	
		
		// Fourth pass
		Pass
		{		
			ZWrite On
						
			CGPROGRAM	
					
			#include "UnityCG.cginc"			
			
			#pragma vertex VS			
			#pragma fragment FS							
			#pragma geometry GS	
				
			#pragma only_renderers d3d11		
			#pragma target 5.0									
											
			struct vs2gs
			{
				float4 pos : SV_POSITION;
				float4 info: FLOAT4;	
			};
			
			struct gs2fs
			{
				float4 pos : SV_POSITION;							
				float4 info: FLOAT4;
				float2 uv: TEXCOORD0;		
			};

			vs2gs VS(uint id : SV_VertexID)
			{
			    AtomData atomData = atomDataBuffer[id];	
			    
			    vs2gs output;	
			    output.pos = mul (UNITY_MATRIX_MV, float4(atomData.x, atomData.y, atomData.z, 1));
			    output.info = float4(atomData.s, atomData.h, atomData.t, 0); 					        
			    return output;
			}
			
			[maxvertexcount(4)]
			void GS(point vs2gs input[1], inout TriangleStream<gs2fs> triangleStream)
			{
				float spriteSize = (molScale * input[0].info.x);
								
				gs2fs output;	
			  	output.info = float4(spriteSize, input[0].info.y, input[0].info.z, 0);			
			
				output.pos = mul (UNITY_MATRIX_P, input[0].pos + float4(spriteSize, spriteSize, 0, 0));
				output.uv = float2(1.0f, 1.0f);
				triangleStream.Append(output);

				output.pos = mul (UNITY_MATRIX_P, input[0].pos + float4(spriteSize, -spriteSize, 0, 0));
				output.uv = float2(1.0f, -1.0f);
				triangleStream.Append(output);					

				output.pos = mul (UNITY_MATRIX_P, input[0].pos + float4(-spriteSize, spriteSize, 0, 0));
				output.uv = float2(-1.0f, 1.0f);
				triangleStream.Append(output);

				output.pos = mul (UNITY_MATRIX_P, input[0].pos + float4(-spriteSize, -spriteSize, 0, 0));
				output.uv = float2(-1.0f, -1.0f);
				triangleStream.Append(output);	
				
				triangleStream.RestartStrip();	
			}
			
			void FS (gs2fs input, out float4 fragColor : COLOR, out float fragDepth : DEPTH) 
			{	
				float lensqr = dot(input.uv, input.uv);
    			
    			if(lensqr > 1.0)
        			discard;

			    float3 normal = float3(input.uv, sqrt(1.0 - lensqr));				
				float3 light = float3(0, 0, 1);							
									
				float ndotl = max( 0.0, dot(light, normal));	
				float atom01Depth = LinearEyeDepth(input.pos.z);				
				float atomEyeDepth = LinearEyeDepth(input.pos.z);				
				float edgeFactor = clamp((ndotl- 0.5) * 50, 0, 1);
						
				float4 atomColor = GetColor(round(input.info.z), round(input.info.y));
				
				fragColor =  (ndotl * 0.05)  + atomColor * 0.95 ;
				fragDepth = 1 / ((atomEyeDepth + input.info.x * -normal.z) * _ZBufferParams.z) - _ZBufferParams.w / _ZBufferParams.z;	
			}			
			ENDCG	
		}
		
		Pass
		{
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

			sampler2D _MainTex;
			sampler2D _DepthTex;
			
            float4 frag(v2f_img i) : COLOR 
            {
            	float d = LinearEyeDepth(tex2D (_DepthTex, i.uv).r);
                return float4(d,d,d,1);
            }
            ENDCG
        }
		
		Pass
		{
			ZTest On
			ZWrite On
			
			CGPROGRAM
			
			#pragma vertex vert_img
            #pragma fragment frag
            
			#include "UnityCG.cginc"

			sampler2D _InputTex;
			sampler2D _DepthTex;		
			
			static float IaoCap = 1.0f;
			static float IaoMultiplier=10000.0f;
			static float IdepthTolerance=0.00001;
			static float IaoScale = 0.6;

			float readDepth( in float2 coord ) 
			{
				return LinearEyeDepth(tex2D (_DepthTex, coord).r) * 0.1;
			}

			float compareDepths( in float depth1, in float depth2 )
			{
				float ao=0.0;
				if (depth2>0.0 && depth1>0.0) 
				{
					float diff = sqrt( clamp( (depth1-depth2),0.0,1.0) );
									
					if (diff<0.15)
					ao = min(IaoCap,max(0.0,depth1-depth2-IdepthTolerance) * IaoMultiplier) * min(diff,0.1);
					
				}
				return ao;
			}				

			float4 frag (v2f_img i) : COLOR0
			{
				float3 color = tex2D(_InputTex, i.uv).rgb;
				float depth = readDepth(i.uv);				

				if(depth  == 1) discard;

//				return float4(depth, 0, 0, 1);
			
				float d;
				float pw = 5.0 / _ScreenParams.x;
				float ph = 5.0 / _ScreenParams.y;

				float aoCap = IaoCap;

				float ao = 0.0;			
				
				//float aoMultiplier=10000.0;
				float aoMultiplier= IaoMultiplier;
				float depthTolerance = IdepthTolerance;
				float aoscale= IaoScale;

				d=readDepth( float2(i.uv.x+pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x+pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;			    
			    
				d=readDepth( float2(i.uv.x+pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;    
				
				pw*=2.0;
				ph*=2.0;
				aoMultiplier/=2.0;
				aoscale*=1.2;
				
				d=readDepth( float2(i.uv.x+pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x+pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
			    
				d=readDepth( float2(i.uv.x+pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;    			    

				pw*=2.0;
				ph*=2.0;
				aoMultiplier/=2.0;
				aoscale*=1.2;
				
				d=readDepth( float2(i.uv.x+pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x+pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				
			  	d=readDepth( float2(i.uv.x+pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale; 
			    
				pw*=2.0;
				ph*=2.0;
				aoMultiplier/=2.0;
				aoscale*=1.2;
				
				d=readDepth( float2(i.uv.x+pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x+pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;
			    
			    d=readDepth( float2(i.uv.x+pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x-pw,i.uv.y));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y+ph));
				ao+=compareDepths(depth,d)/aoscale;
				d=readDepth( float2(i.uv.x,i.uv.y-ph));
				ao+=compareDepths(depth,d)/aoscale;

				// ao/=4.0;
			    ao/=8.0;
			    ao = 1.0- (ao * 1.0);
//			    ao = 1.5*ao;

			    ao = clamp(ao, 0.0, 1.0 ) ;			    
//				return float4(float3(ao,ao,ao), 1.0);
				return float4(color * ao, 1.0);
			}
			
			
			ENDCG
		}			
						
	}
	Fallback Off
}	