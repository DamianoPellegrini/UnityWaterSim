Shader "Universal Render Pipeline/Nature/Water/Tessendorf"
{
	Properties
	{
		_LOD_scale("LOD_scale", Range(1,10)) = 3
        _FoamBiasLOD0("Foam Bias LOD0", Range(0,7)) = 0.5
        _FoamBiasLOD1("Foam Bias LOD1", Range(0,7)) = 1.7
        _FoamBiasLOD2("Foam Bias LOD2", Range(0,7)) = 2.8
        _FoamScale("Foam Scale", Range(0,20)) = 2.4
		_HeightScale("Height displacement scale", Float) = 1
		_RefractionStrength("Refraction Strength", Float) = 1

		[Header(Cascade 0)]
		_Displacement_c0("Displacement C0", 2D) = "black" {}
		_Derivatives_c0("Derivatives C0", 2D) = "black" {}
		_Turbulence_c0("Turbulence C0", 2D) = "white" {}
		[Header(Cascade 1)]
		_Displacement_c1("Displacement C1", 2D) = "black" {}
		_Derivatives_c1("Derivatives C1", 2D) = "black" {}
		_Turbulence_c1("Turbulence C1", 2D) = "white" {}
		[Header(Cascade 2)]
		_Displacement_c2("Displacement C2", 2D) = "black" {}
		_Derivatives_c2("Derivatives C2", 2D) = "black" {}
		_Turbulence_c2("Turbulence C2", 2D) = "white" {}

		// Scatter
		_WavePeakScatterStrength("Wave Peak Scatter strength", Range(0, 1)) = 0.1
		_ScatterStrength("Scatter strength", Range(0, 1)) = 0.1
		[MainColor] _ScatterColor("Scatter color", Color) = (0, 0.2, 1, 1)

		// Diffuse
		_ScatterShadowStrength("Scatter Shadow strength", Range(0, 1)) = 0.15
		_BubbleDensity("Bubble density (ambient strength)", Range(0, 1)) = 0.7
		_BubbleColor("Bubble color (ambient color)", Color) = (0, 0, 0.25, 1)

        _FoamColor("Foam Color", Color) = (1,1,1,1)

		// Fog
		_FogColor ("Water Fog Color", Color) = (0, 0, 0.25, 1)
		_FogDensity ("Water Fog Density", Range(0, 2)) = 0.25

		_EnvironmentLightStrength("Environment Light strength", Range(0,1)) = 1
		_Roughness("Roughness", Range(0.001,1)) = 0.05
	}

	SubShader
	{        
		Tags {
			"RenderType" = "Transparent"
			"Queue" = "Transparent"
			"RenderPipeline" = "UniversalRenderPipeline"
		}
		// LOD 200
		
		HLSLINCLUDE

		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // To get sunlight
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl" // To get envmap
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl" // To sample URP Opaque texture
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" // To sample URP Depth texture

		// To make the Unity shader SRP Batcher compatible, declare all
		// properties related to a Material in a a single CBUFFER block with 
		// the name UnityPerMaterial.
		CBUFFER_START(UnityPerMaterial)


		// Vertex
		sampler2D _Displacement_c0;
		sampler2D _Derivatives_c0;
		sampler2D _Turbulence_c0;
		
		sampler2D _Displacement_c1;
		sampler2D _Derivatives_c1;
		sampler2D _Turbulence_c1;
		
		sampler2D _Displacement_c2;
		sampler2D _Derivatives_c2;
		sampler2D _Turbulence_c2;

		half _LOD_scale;
		half _HeightScale;
		
		half _LengthScale0;
		half _LengthScale1;
		half _LengthScale2;

		half _FoamBiasLOD0;
		half _FoamBiasLOD1;
		half _FoamBiasLOD2;
		half _FoamScale;
		
		// Fragment
		half _RefractionStrength;

		half _EnvironmentLightStrength;

		half _WavePeakScatterStrength;
		half _ScatterStrength;
		half4 _ScatterColor;
		half _ScatterShadowStrength;

		half _BubbleDensity;
		half4 _BubbleColor;

		half _Roughness;

		half4 _FoamColor;

		half4 _FogColor;
		half _FogDensity;
		CBUFFER_END

		ENDHLSL

		Pass
		{
			Name "ForwardLit"
			Tags {
				"LightMode"="UniversalForward"
			}
			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite On
			Cull Back

			HLSLPROGRAM
			#pragma multi_compile _ MID CLOSE
			#pragma vertex SSSVertex
			#pragma fragment SSSFragment

			struct Attributes {
				float4 positionOS   : POSITION;
				float3 normalOS : NORMAL;
			};

			struct Varyings {
				float4 positionHCS  : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float2 uvWS : TEXCOORD2;
				float3 displacementWS : TEXCOORD3;
				float4 lodScales : TEXCOORD4;
				float4 positionNDC : TEXCOORD5;
			};

			Varyings SSSVertex(Attributes IN)
			{
				Varyings OUT;

				float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
				VertexNormalInputs normals = GetVertexNormalInputs(IN.normalOS);
				
				float4 worldUV = float4(positionWS.xz, 0, 0);
				OUT.uvWS = worldUV.xy;

				float3 viewVector = GetCameraPositionWS() - positionWS;
				float viewDist = length(viewVector);
				
				float lod_c0 = min(_LOD_scale * _LengthScale0 / viewDist, 1);
				float lod_c1 = min(_LOD_scale * _LengthScale1 / viewDist, 1);
				float lod_c2 = min(_LOD_scale * _LengthScale2 / viewDist, 1);

				float3 displacementWS = 0;
				float largeWavesBias = 0;

				displacementWS += tex2Dlod(_Displacement_c0, worldUV / _LengthScale0).rgb * lod_c0;
				largeWavesBias = displacementWS.y;
				#if defined(MID) || defined(CLOSE)
				displacementWS += tex2Dlod(_Displacement_c1, worldUV / _LengthScale1).rgb * lod_c1;
				#endif
				#if defined(CLOSE)
				displacementWS += tex2Dlod(_Displacement_c2, worldUV / _LengthScale2).rgb * lod_c2;
				#endif

				OUT.lodScales = float4(lod_c0, lod_c1, lod_c2, max(displacementWS.y - largeWavesBias * 0.8 - _ScatterStrength, 0) / _WavePeakScatterStrength);

				// Apply displacement in WS
				displacementWS.y *= _HeightScale;
				
				// World space
				OUT.positionWS = positionWS + displacementWS;
				OUT.displacementWS = displacementWS;
				OUT.normalWS = normals.normalWS;

				// Clip space
				OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);

				// Screen space
				float4 ndc = OUT.positionHCS * 0.5f;
				OUT.positionNDC.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
				OUT.positionNDC.zw = OUT.positionHCS.zw;

				return OUT;
			}

			float SchlickFresnel(float3 normal, float3 viewDir) {
				return 0.02f + (1 - 0.02f) * (pow(1 - saturate(dot(normal, viewDir)), 5.0f));
			}

			float SmithMaskingBeckmann(float3 H, float3 S, float roughness) {
				float hdots = max(0.001f, saturate(dot(H, S)));
				float a = hdots / (roughness * sqrt(1 - hdots * hdots));

				float a2 = a * a;

				return a < 1.6f ? (1.0f - 1.259f * a + 0.396f * a2) / (3.535f * a + 2.181 * a2) : 0.0f;
			}

			float Beckmann(float ndoth, float roughness) {
				float exp_arg = (ndoth * ndoth - 1) / (roughness * roughness * ndoth * ndoth);

				return exp(exp_arg) / (PI * roughness * roughness * ndoth * ndoth * ndoth * ndoth);
			}

			half4 SSSFragment(Varyings IN) : SV_Target
			{
				Light sun = GetMainLight();

				half3 lightDir = sun.direction;
				half3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
				half3 halfwayDir = normalize(lightDir + viewDir);

				half LdotH = saturate(dot(lightDir, halfwayDir));
				half VdotH = saturate(dot(viewDir, halfwayDir));

				// Normals
				float4 derivatives = tex2D(_Derivatives_c0, IN.positionWS.xz / _LengthScale0) * IN.lodScales.x;
				#if defined(MID) || defined(CLOSE)
				derivatives += tex2D(_Derivatives_c1, IN.positionWS.xz / _LengthScale1) * IN.lodScales.y;
				#endif
				#if defined(CLOSE)
				derivatives += tex2D(_Derivatives_c2, IN.positionWS.xz / _LengthScale2) * IN.lodScales.z;
				#endif
				float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));


				// Depth & refraction calculation
				float2 refractionOffset = slope * 10 * _RefractionStrength;
                float2 uvSS = GetNormalizedScreenSpaceUV(IN.positionHCS.xy + refractionOffset);

                // Sample the depth from the Camera depth texture.
                #if UNITY_REVERSED_Z
                    real sceneDepth = SampleSceneDepth(uvSS);
                #else
                    // Adjust Z to match NDC for OpenGL ([-1, 1])
                    real sceneDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uvSS));
                #endif
				real surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(IN.positionHCS.z);
				real eyeDepth = LinearEyeDepth(sceneDepth, _ZBufferParams);
				real depthDifference = saturate((eyeDepth - surfaceDepth - 0.1));

				half3 sceneColor = SampleSceneColor(uvSS);
				float fogFactor = exp2(-_BubbleDensity * depthDifference);
				half3 underwaterColor = lerp(_FogColor, sceneColor, fogFactor);

				float3 macroNormal = float3(0, 1, 0);
				float3 mesoNormal = normalize(float3(-slope.x, 1, -slope.y));

				// Foam
				#if defined(CLOSE)
				float jacobian = tex2D(_Turbulence_c0, IN.positionWS.xz / _LengthScale0).r
					+ tex2D(_Turbulence_c1, IN.positionWS.xz / _LengthScale1).r
					+ tex2D(_Turbulence_c2, IN.positionWS.xz / _LengthScale2).r;
				jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD2) * _FoamScale));
				#elif defined(MID)
				float jacobian = tex2D(_Turbulence_c0, IN.positionWS.xz / _LengthScale0).r
					+ tex2D(_Turbulence_c1, IN.positionWS.xz / _LengthScale1).r;
				jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD1) * _FoamScale));
				#else
				float jacobian = tex2D(_Turbulence_c0, IN.positionWS.xz / _LengthScale0).r;
				jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD0) * _FoamScale));
				#endif
				float foam = lerp(0.0f, saturate(jacobian), pow(1 - surfaceDepth, 2));

				// PBR Scatter model
				half NdotL = saturate(dot(mesoNormal, lightDir));

				half a = _Roughness + foam;
				half ndoth = max(0.0001f, dot(mesoNormal, halfwayDir));

				// Specular term
				half viewMask = SmithMaskingBeckmann(halfwayDir, viewDir, a);
				half lightMask = SmithMaskingBeckmann(halfwayDir, lightDir, a);

				half G = rcp(1 + viewMask + lightMask);

				half eta = 1.33f;
				half R = ((eta - 1) * (eta - 1)) / ((eta + 1) * (eta + 1));
				half thetaV = FastACos(viewDir.y);

				half numerator = pow(1 - dot(mesoNormal, viewDir), abs(5 * exp(-2.69 * a)));
				half F = R + (1 - R) * numerator / (1.0f + 22.7f * pow(a, 1.5f));
				F = saturate(F);
				
				half3 specular = sun.color * F * G * Beckmann(ndoth, a);
				specular *= rcp(4.0f * max(0.001f, saturate(dot(macroNormal, lightDir))));
				specular *= saturate(dot(mesoNormal, lightDir));

				// Scatter term
				half3 irradiance = _EnvironmentLightStrength * CalculateIrradianceFromReflectionProbes(reflect(-viewDir, mesoNormal), IN.positionWS, a);

				half waveHeight = max(0.0f, IN.displacementWS.y);
				half3 scatterColor = _ScatterColor.xyz;
				half3 bubbleColor = _BubbleColor.xyz;
				half bubbleDensity = _BubbleDensity;

				half k1 = _WavePeakScatterStrength * waveHeight * pow(saturate(dot(lightDir, -viewDir)), 4.0f) * pow(0.5f - 0.5f * dot(lightDir, mesoNormal), 3.0f);
				half k2 = _ScatterStrength * pow(saturate(dot(viewDir, mesoNormal)), 2.0f);
				half k3 = _ScatterShadowStrength * NdotL;
				half k4 = bubbleDensity;

				half3 scatter = (k1 + k2) * scatterColor * sun.color * rcp(1 + lightMask);
				scatter += k3 * scatterColor * sun.color + k4 * bubbleColor * sun.color;

				// Final color
				half3 output = (1 - F) * scatter + specular + F * irradiance;

				// Final foam
				output = lerp(output, _FoamColor, saturate(foam));

				// Final depth
				output = lerp(output, underwaterColor, 0.5f);

				output = max(0.0f, output);

				// DEBUGS
				// return half4(mesoNormal * 0.5 + 0.5, 1);
				// return half4(foam,foam,foam, 1);
				// return half4((surfaceDepth),(surfaceDepth),(surfaceDepth),1);
				// return half4(IN.positionHCS.z,IN.positionHCS.z,IN.positionHCS.z,1);
				// return half4(eyeDepth,eyeDepth,eyeDepth,1);
				// return half4(depthDifference,depthDifference,depthDifference,1);
				// return half4(IN.positionHCS.z - sceneDepth,IN.positionHCS.z - sceneDepth,IN.positionHCS.z - sceneDepth,1);

				return half4(output, 1);
			}
			ENDHLSL
		}

		// TODO: maybe move shadow like refraction?
		// Pass{
		// 	Name "ShadowPass"
		// 	Tags {
		// 		"LightMode"="ShadowCaster"
		// 	}
		// }

		Pass{
			Name "DepthNormalsPass"
			Tags { "LightMode"="DepthNormals" }

			ZWrite On
			ZTest LEqual

			HLSLPROGRAM
			// #pragma vertex DepthNormalsVertex
			#pragma vertex DisplacedDepthNormalsVertex
			#pragma fragment DepthNormalsFragment

			// Material Keywords
			#pragma shader_feature_local _NORMALMAP
			#pragma shader_feature_local _PARALLAXMAP
    		#pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED
			#pragma shader_feature_local_fragment _ALPHATEST_ON
			#pragma shader_feature_local_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

			// GPU Instancing
			#pragma multi_compile_instancing
			//#pragma multi_compile _ DOTS_INSTANCING_ON

			#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"

			Varyings DisplacedDepthNormalsVertex(Attributes input) {
				Varyings output = (Varyings)0;
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);

				float4 worldUV = float4(positions.positionWS.xz, 0, 0);
				float3 viewVector = GetCameraPositionWS() - positions.positionWS;
				float viewDist = length(viewVector);
				
				float lod_c0 = min(_LOD_scale * _LengthScale0 / viewDist, 1);
				float lod_c1 = min(_LOD_scale * _LengthScale1 / viewDist, 1);
				float lod_c2 = min(_LOD_scale * _LengthScale2 / viewDist, 1);

				float3 displacementWS = 0;
				float largeWavesBias = 0;

				displacementWS += tex2Dlod(_Displacement_c0, worldUV / _LengthScale0).xyz * lod_c0;
				largeWavesBias = displacementWS.y;
				#if defined(MID) || defined(CLOSE)
				displacementWS += tex2Dlod(_Displacement_c1, worldUV / _LengthScale1).xyz * lod_c1;
				#endif
				#if defined(CLOSE)
				displacementWS += tex2Dlod(_Displacement_c2, worldUV / _LengthScale2).xyz * lod_c2;
				#endif

				#if defined(_ALPHATEST_ON)
				output.uv = worldUV.xy;
				#endif
				output.positionCS = TransformWorldToHClip(positions.positionWS + displacementWS);

				// Normals
				float4 derivatives = tex2Dlod(_Derivatives_c0, worldUV / _LengthScale0) * lod_c0;
				#if defined(MID) || defined(CLOSE)
				derivatives += tex2Dlod(_Derivatives_c1, worldUV / _LengthScale1) * lod_c1;
				#endif
				#if defined(CLOSE)
				derivatives += tex2Dlod(_Derivatives_c2, worldUV / _LengthScale2) * lod_c2;
				#endif
				float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));

				output.normalWS = NormalizeNormalPerVertex(float3(-slope.x, 1, -slope.y));
				return output;
			}
			ENDHLSL
		}

		// TODO: same as shadows
		// Pass{
		// 	Name "MetaPass"
		// 	Tags {
		// 		"LightMode"="Meta"
		// 	}
		// }

	}
}
