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

		[KeywordEnum(Off, Jacobian, SSS, Refraction, Reflection, Normal, Fresnel, Foam, WaterDepth)] _Debug ("Debug mode", Float) = 0
	}

	SubShader
	{		
		Tags {
			"RenderType" = "Transparent"
			"Queue" = "Transparent-100"
			"RenderPipeline" = "UniversalRenderPipeline"
		}
		ZWrite Off
		Cull Off
		LOD 300
		
		HLSLINCLUDE

		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
		#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Refraction.hlsl" // To refraction model box
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl" // To get sunlight
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl" // To get envmap
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl" // To sample URP Opaque texture
		#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl" // To sample URP Depth texture

		// To make the Unity shader SRP Batcher compatible, declare all
		// properties related to a Material in a a single CBUFFER block with 
		// the name UnityPerMaterial.
		CBUFFER_START(UnityPerMaterial)

		sampler2D _WaterDepthMap;
		float4 _WaterZBufferParams;

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

		float3 SampleDisplacement(float2 worldXZ, float3 lodWeights) {
			float4 uvst = float4(worldXZ, 0,0);

			float3 displacement = tex2Dlod(_Displacement_c0, uvst / _LengthScale0).rgb * lodWeights[0];
			#if defined(MID) || defined(CLOSE)
			displacement += tex2Dlod(_Displacement_c1, uvst / _LengthScale1).rgb * lodWeights[1];
			#endif
			#if defined(CLOSE)
			displacement += tex2Dlod(_Displacement_c2, uvst / _LengthScale2).rgb * lodWeights[2];
			#endif

			return displacement;
		}

		float4 SampleDerivatives(float2 worldXZ, float3 lodWeights) {
			float4 uvst = float4(worldXZ, 0,0);

			float4 derivatives = tex2Dlod(_Derivatives_c0, uvst / _LengthScale0) * lodWeights[0];
			#if defined(MID) || defined(CLOSE)
			derivatives += tex2Dlod(_Derivatives_c1, uvst / _LengthScale1) * lodWeights[1];
			#endif
			#if defined(CLOSE)
			derivatives += tex2Dlod(_Derivatives_c2, uvst / _LengthScale2) * lodWeights[2];
			#endif

			return derivatives;
		}

		float3 NormalFromDerivatives(float4 derivatives)
		{
			float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));
			return normalize(float3(-slope.x, 1, -slope.y));
		}
		float3 TangentFromDerivatives(float4 derivatives)
		{
			float tangentMagnitude = 1 / (1 + derivatives.z);
			return normalize(float3(tangentMagnitude, -derivatives.x * tangentMagnitude, 0.0));
		}

		float3 BitangentFromDerivatives(float4 derivatives)
		{
			float bitangentMagnitude = 1 / (1 + derivatives.w);
			return normalize(float3(0.0, -derivatives.y * bitangentMagnitude, bitangentMagnitude));
		}

		float SampleTurbulence(float2 worldXZ, float3 lodWeights) {
			float4 uvst = float4(worldXZ, 0,0);

			float jacobian = tex2Dlod(_Turbulence_c0, uvst / _LengthScale0).r * lodWeights[0];
			jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD0) * _FoamScale));
			#if defined(MID) || defined(CLOSE)
			jacobian += tex2Dlod(_Turbulence_c1, uvst / _LengthScale1).r * lodWeights[1];
			jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD1) * _FoamScale));
			#endif
			#if defined(CLOSE)
			jacobian += tex2Dlod(_Turbulence_c2, uvst / _LengthScale1).r * lodWeights[2];
			jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD2) * _FoamScale));
			#endif

			return jacobian;
		}

		// Depth of the sea floor
		float SampleWaterDepth(float2 worldXZ) {
			return (1 - tex2Dlod(_WaterDepthMap, float4(worldXZ, 0,0) * 0.002 + 0.5).r) * (24/*_MaxDepth*/ + _WaterZBufferParams.x) - _WaterZBufferParams.x;
		}

		real SampleSceneZBuffer(float2 uv) {
			// Sample the depth from the Camera depth texture.
			#if UNITY_REVERSED_Z
			return SampleSceneDepth(uv);
			#else
			// Adjust Z to match NDC for OpenGL ([-1, 1])
			return lerp(UNITY_NEAR_CLIP_VALUE, 1, SampleSceneDepth(uv));
			#endif
		}

		float2 AdjustedDepth(half2 uvs, half4 additionalData)
		{
			float rawD = SampleSceneZBuffer(uvs);
			float d = LinearEyeDepth(rawD, _ZBufferParams);

		 	return float2(d * additionalData.x - additionalData.y, (rawD * -_ProjectionParams.x));
		}

		float3 WaterDepth(float3 posWS, half4 additionalData, half2 screenUVs)// x = seafloor depth, y = water depth
		{
			float3 outDepth = 0;
			outDepth.xz = AdjustedDepth(screenUVs, additionalData);
			float wd = SampleWaterDepth(posWS.xz);
			outDepth.y = wd + posWS.y;
			return outDepth;
		}

		half4 AdditionalData(float3 positionWS, float3 displacementWS)
		{
			half4 data = half4(0.0, 0.0, 0.0, 0.0);
			float3 viewPos = TransformWorldToView(positionWS);
			data.x = length(viewPos / viewPos.z);// distance to surface
			data.y = length(GetCameraPositionWS().xyz - positionWS); // local position in camera space
			data.z = displacementWS.y * 0.5 + 0.5; // encode the normalized wave height into additional data
			data.w = displacementWS.x + displacementWS.z;
			return data;
		}

		ENDHLSL

		Pass
		{
			Name "ForwardLit"
			Tags {
				"LightMode"="UniversalForward"
			}
			Blend SrcAlpha OneMinusSrcAlpha

			HLSLPROGRAM
			#pragma shader_feature _DEBUG_OFF _DEBUG_JACOBIAN _DEBUG_SSS _DEBUG_REFRACTION _DEBUG_REFLECTION _DEBUG_NORMAL _DEBUG_FRESNEL _DEBUG_FOAM _DEBUG_WATERDEPTH
			#pragma multi_compile _ MID CLOSE

			#pragma vertex SSSVertex
			#pragma fragment SSSFragment

			// Point lights, etc.
			#pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS

			// Shadows
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
			#pragma multi_compile_fragment _ _SHADOWS_SOFT

			// Baked GI
			#pragma multi_compile _ LIGHTMAP_ON
			#pragma multi_compile _ DYNAMICLIGHTMAP_ON
			#pragma multi_compile _ DIRLIGHTMAP_COMBINED
			#pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
			#pragma multi_compile _ SHADOWS_SHADOWMASK

			#pragma multi_compile_fog
			#pragma multi_compile_instancing
			#pragma multi_compile _ DOTS_INSTANCING_ON
			#pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

			#define REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR 1

			struct Attributes {
				float4 positionOS			: POSITION;
				float3 normalOS				: NORMAL;
				float4 tangentOS			: TANGENT;
				float2 staticLightmapUV		: TEXCOORD1;
				float2 dynamicLightmapUV	: TEXCOORD2;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct Varyings {
				float3 positionWS				: TEXCOORD0;
				float2 uvWS						: TEXCOORD2;
				float3 displacementWS			: TEXCOORD3;
				float3 lodWeights				: TEXCOORD4;
				float4 positionNDC				: TEXCOORD5;

#ifdef _ADDITIONAL_LIGHTS_VERTEX
				half4 fogFactorAndVertexLight	: TEXCOORD6; // x: fogFactor, yzw: vertex light
#else
				half  fogFactor					: TEXCOORD6;
#endif

				DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 7);
#ifdef DYNAMICLIGHTMAP_ON
				float2  dynamicLightmapUV		: TEXCOORD8; // Dynamic lightmap UVs
#endif
			
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
				float4 shadowCoord				: TEXCOORD9;
#endif

				float4 positionCS				: SV_POSITION;

				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};

			void InitializeInputData(Varyings input, out InputData inputData)
			{
				inputData = (InputData)0;
				inputData.positionWS = input.positionWS;

				inputData.positionCS = input.positionCS;

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
	inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
	inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
	inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
#ifdef _ADDITIONAL_LIGHTS_VERTEX
	inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactorAndVertexLight.x);
	inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
	inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
#endif
			}


			Varyings SSSVertex(Attributes input)
			{
				Varyings output = (Varyings)0;

				// Unity Instancing
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_TRANSFER_INSTANCE_ID(input, output);
				// Unity Stereo
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

				VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);

				// LOD weights
				float3 viewVector = GetCameraPositionWS() - vertexInput.positionWS;
				float viewDist = length(viewVector);

				float lod_c0 = min(_LOD_scale * _LengthScale0 / viewDist, 1);
				float lod_c1 = min(_LOD_scale * _LengthScale1 / viewDist, 1);
				float lod_c2 = min(_LOD_scale * _LengthScale2 / viewDist, 1);
				output.lodWeights = float3(lod_c0, lod_c1, lod_c2);

				// Displacement calculations
				float3 displacementWS = SampleDisplacement(vertexInput.positionWS.xz, output.lodWeights);
				displacementWS.y *= _HeightScale;
				
				// TODO: Transform the displacement to be oriented towards the surface normal
				// float3 tangentWS = TangentFromDerivatives(derivatives);
				// VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);
				// real3x3 tangentToWorld = CreateTangentToWorld(normalInput.normalWS, normalInput.tangentWS, input.tangentOS.w);
				// displacementWS = TransformTangentToWorld(displacementWS, tangentToWorld);

				// Recalculate transformation after displacement
				vertexInput.positionWS += displacementWS;
				vertexInput = GetVertexPositionInputs(TransformWorldToObject(vertexInput.positionWS));

				// Fog
				half fogFactor = 0;
#if !defined(_FOG_FRAGMENT)
					fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

				// Vertex light and fog packing
#ifdef _ADDITIONAL_LIGHTS_VERTEX
				float4 derivatives = SampleDerivatives(vertexInput.positionWS.xz, output.lodWeights);
				float3 normalWS = NormalFromDerivatives(derivatives);
				half3 vertexLight = VertexLighting(vertexInput.positionWS, normalWS);

				output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
				output.fogFactor = fogFactor;
#endif

				// Lightmaps for bakedGI
				OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);

#ifdef DYNAMICLIGHTMAP_ON
				output.dynamicLightmapUV = input.dynamicLightmapUV.xy * unity_DynamicLightmapST.xy + unity_DynamicLightmapST.zw;
#endif

				// World space
				output.positionWS = vertexInput.positionWS;
				output.displacementWS = displacementWS;
				output.uvWS = vertexInput.positionWS.xz;

				// Clip space
				output.positionCS = vertexInput.positionCS;

				// Screen space
				output.positionNDC = vertexInput.positionNDC;

#ifdef REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR
				// Shadow space
				output.shadowCoord = GetShadowCoord(vertexInput);
#endif

				return output;
			}

			float SmithMaskingBeckmann(float3 H, float3 S, float roughness) {
				float HdotS = max(0.001f, saturate(dot(H, S)));
				float a = HdotS / (roughness * sqrt(1 - HdotS * HdotS));

				float a2 = a * a;

				return a < 1.6f ? (1.0f - 1.259f * a + 0.396f * a2) / (3.535f * a + 2.181 * a2) : 0.0f;
			}

			float Beckmann(float NdotH, float roughness) {
				float exp_arg = (NdotH * NdotH - 1) / (roughness * roughness * NdotH * NdotH);

				return exp(exp_arg) / (PI * roughness * roughness * NdotH * NdotH * NdotH * NdotH);
			}

			half4 SSSFragment(Varyings input) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(input);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				// SurfaceData surfaceData;
				// InitializeStandardLitSurfaceData(input.uv, surfaceData);

				InputData inputData;
				InitializeInputData(input, inputData);

				// half4 color = UniversalFragmentPBR(inputData, surfaceData);
				// color.rgb = MixFog(color.rgb, inputData.fogCoord);
				// color.a = OutputAlpha(color.a, IsSurfaceTypeTransparent(_Surface));
				// return color;

				Light sun = GetMainLight();

				half3 lightDir = sun.direction;
				half3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
				half3 halfwayDir = normalize(lightDir + viewDir);

				half LdotH = saturate(dot(lightDir, halfwayDir));
				half VdotH = saturate(dot(viewDir, halfwayDir));

				// Normals
				float4 derivatives = SampleDerivatives(input.positionWS.xz, input.lodWeights);

				// TODO: Use normalOS as macronormal and using tangent move mesonormal in tangent space
				float3 macroNormal = float3(0, 1, 0);
				float3 mesoNormal = NormalFromDerivatives(derivatives);

				// Depth & refraction calculation
				float2 distortion = TransformWorldToViewNormal(-mesoNormal).xz * 10 * _RefractionStrength;
				float2 uvSS = GetNormalizedScreenSpaceUV(input.positionCS.xy + distortion);

				real rawDepth = SampleSceneZBuffer(uvSS);
				// real surfaceDepth = UNITY_Z_0_FAR_FROM_CLIPSPACE(input.positionCS.z);
				real surfaceDepth = input.positionCS.z;
				real eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
				real depthDifference = saturate((rawDepth - surfaceDepth - 0.1));

				// Refraction & fog
				half3 sceneColor = SampleSceneColor(uvSS);
				float fogFactor = exp2(-_BubbleDensity * depthDifference);
				half3 underwaterColor = lerp(_FogColor, sceneColor, fogFactor);

				// Foam
				// TODO: Fix jacobian foam
				float jacobian = SampleTurbulence(input.positionWS.xz, input.lodWeights);
				// float foam = lerp(0.0f, saturate(jacobian), pow(1 - surfaceDepth, 2));
				float foam = 0;

				// half depthEdge = saturate(rawDepth * 20);
				// half depthAdd = saturate(1 - rawDepth * 4) * 0.5;
				// half edgeFoam = saturate((1 - min(rawDepth, surfaceDepth) * 0.5 - 0.25) + depthAdd) * depthEdge;
				// foam += edgeFoam;

				// PBR Scatter model
				half NdotL = saturate(dot(mesoNormal, lightDir));

				half roughness = _Roughness + foam;
				half ndoth = max(0.0001f, dot(mesoNormal, halfwayDir));

				// Specular term
				half viewMask = SmithMaskingBeckmann(halfwayDir, viewDir, roughness);
				half lightMask = SmithMaskingBeckmann(halfwayDir, lightDir, roughness);

				half G = rcp(1 + viewMask + lightMask);

				half thetaV = FastACos(viewDir.y);
				half numerator = pow(1 - dot(mesoNormal, viewDir), abs(5 * exp(-2.69 * roughness)));
				half fresnel = 0.02f + (1 - 0.02f) * numerator / (1.0f + 22.7f * pow(roughness, 1.5f));
				fresnel = saturate(fresnel);
				
				half3 specular = sun.color * fresnel * G * Beckmann(ndoth, roughness);
				specular *= rcp(4.0f * max(0.001f, saturate(dot(macroNormal, lightDir))));
				specular *= saturate(dot(mesoNormal, lightDir));

				// Reflection
				half3 irradiance = _EnvironmentLightStrength * GlossyEnvironmentReflection(reflect(-viewDir, mesoNormal), input.positionWS, roughness, 0.0, uvSS);
				
				// Scatter term
				half waveHeight = max(0.0f, input.displacementWS.y);
				half3 scatterColor = _ScatterColor.rgb;
				half3 bubbleColor = _BubbleColor.rgb;
				half bubbleDensity = _BubbleDensity;

				half k1 = _WavePeakScatterStrength * waveHeight * pow(saturate(dot(lightDir, -viewDir)), 4.0f) * pow(0.5f - 0.5f * dot(lightDir, mesoNormal), 3.0f);
				half k2 = _ScatterStrength * pow(saturate(dot(viewDir, mesoNormal)), 2.0f);
				half k3 = _ScatterShadowStrength * NdotL;
				half k4 = bubbleDensity;

				half3 scatter = (k1 + k2) * scatterColor * sun.color * rcp(1 + lightMask);
				scatter += k3 * scatterColor * sun.color + k4 * bubbleColor * sun.color;

				// Final color
				half3 output = (1 - fresnel) * scatter + specular + fresnel * irradiance;

				// Final foam
				output = lerp(output, _FoamColor, saturate(foam));

				// Final depth
				output = lerp(output, underwaterColor, 0.35f);
				output.rgb = MixFogColor(output.rgb, _FogColor.rgb, inputData.fogCoord);
				

				// //If the two are similar, then there is an object intersecting with our object
				// float diff = (abs(rawDepth - surfaceDepth)) / 0.005;
 
				// if(diff <= 1)
				// {
				//	 output.rgb = lerp(_FoamColor.rgb,
				//					   output.rgb,
				//					   float4(diff, diff, diff, diff));
				// }

				output = max(0.0f, output);

				half4 ad = AdditionalData(input.positionWS, input.displacementWS);

				half3 screenUV = input.shadowCoord.xyz / input.shadowCoord.w;//screen UVs
				float3 testDepth = WaterDepth(input.positionWS, ad, screenUV.xy);

				// DEBUGS
				#if defined(_DEBUG_FOAM)
					return half4(foam.xxx, 1);
				#elif defined(_DEBUG_JACOBIAN)
				return half4(jacobian.xxx, 1);
				#elif defined(_DEBUG_SSS)
					return half4(scatter, 1);
				#elif defined(_DEBUG_REFRACTION)
					return half4(underwaterColor, 1);
				#elif defined(_DEBUG_REFLECTION)
					return half4(irradiance, 1);
				#elif defined(_DEBUG_NORMAL)
					return half4(mesoNormal * 0.5 + 0.5, 1);
				#elif defined(_DEBUG_FRESNEL)
					return half4(fresnel.xxx, 1);
				#elif defined(_DEBUG_WATERDEPTH)
					return half4(frac(testDepth), 1);
				#else
					return half4(output, 1);
				#endif

			}
			ENDHLSL
		}

		// TODO: Transparent volumes cast a shadow with intensity depending on depth (volume depth and distance from bottom to fragments)
		// But that would render them in the depth buffer so ZWrite should be off
		// Pass{
		// 	Name "ShadowPass"
		// 	Tags { "LightMode"="ShadowCaster" }
		// 	ZWrite Off
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
				float3 lodWeights = float3(lod_c0, lod_c1, lod_c2);

				float3 displacementWS = SampleDisplacement(positions.positionWS.xz, lodWeights);

				#if defined(_ALPHATEST_ON)
				output.uv = worldUV.xy;
				#endif
				output.positionCS = TransformWorldToHClip(positions.positionWS + displacementWS);

				// Normals
				float4 derivatives = SampleDerivatives(positions.positionWS.xz, lodWeights);
				output.normalWS = NormalFromDerivatives(derivatives);
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
