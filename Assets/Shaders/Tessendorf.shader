Shader "Universal Render Pipeline/Nature/Water/Tessendorf"
{
	Properties
	{
		_LOD_scale("LOD_scale", Range(1,10)) = 0
        _FoamBiasLOD0("Foam Bias LOD0", Range(0,7)) = 1
        _FoamBiasLOD1("Foam Bias LOD1", Range(0,7)) = 1
        _FoamBiasLOD2("Foam Bias LOD2", Range(0,7)) = 1
        _FoamScale("Foam Scale", Range(0,20)) = 1
		_HeightScale("Height displacement scale", Float) = 1

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
		_WavePeakScatterStrength("Wave Peak Scatter strength", Range(0, 1)) = 1
		_ScatterStrength("Scatter strength", Range(0, 1)) = 0.1
		[MainColor] _ScatterColor("Scatter color", Color) = (0, 0.2, 1, 1)

		// Diffuse
		_ScatterShadowStrength("Scatter Shadow strength", Range(0, 1)) = 0.3
		_BubbleDensity("Bubble density (ambient strength)", Range(0, 1)) = 0.75
		_BubbleColor("Bubble color (ambient color)", Color) = (0, 0, 0.25, 1)

        _FoamColor("Foam Color", Color) = (1,1,1,1)

		_EnvironmentLightStrength("Environment Light strength", Range(0,1)) = 1
		_Roughness("Roughness", Range(0,1)) = 0.05
	}

	SubShader
	{        
		Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalRenderPipeline" }

		Pass
		{
			Tags { "LightMode"="UniversalForward" "UniversalMaterialType"="Lit" }
			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			// To get sunlight
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
			// To get envmap
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/GlobalIllumination.hlsl"

			// To make the Unity shader SRP Batcher compatible, declare all
			// properties related to a Material in a a single CBUFFER block with 
			// the name UnityPerMaterial.
			CBUFFER_START(UnityPerMaterial)

			half LengthScale0;
        	half LengthScale1;
        	half LengthScale2;

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

			half _FoamBiasLOD0;
			half _FoamBiasLOD1;
			half _FoamBiasLOD2;
			half _FoamScale;

			// Fragment
			half _EnvironmentLightStrength;
			half _WavePeakScatterStrength;
			half _ScatterStrength;
			half4 _ScatterColor;
			half _ScatterShadowStrength;
			half _BubbleDensity;
			half4 _BubbleColor;
			half _Roughness;
			half4 _FoamColor;
			CBUFFER_END

			struct Attributes {
				float4 positionOS   : POSITION;
				float3 normalOS : NORMAL;
			};

			struct Varyings {
				float4 positionHCS  : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
				float4 lodScales : TEXCOORD2;
				float2 uvWS : TEXCOORD3;
			};

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
				
				float4 worldUV = float4(OUT.positionWS.xz, 0, 0);
				OUT.uvWS = OUT.positionWS.xz;

				float3 viewVector = GetCameraPositionWS() - OUT.positionWS;
				float viewDist = length(viewVector);
				
				float lod_c0 = min(_LOD_scale * LengthScale0 / viewDist, 1);
				float lod_c1 = min(_LOD_scale * LengthScale1 / viewDist, 1);
				float lod_c2 = min(_LOD_scale * LengthScale2 / viewDist, 1);

				float3 displacementWS = 0;
				float largeWavesBias = 0;

				displacementWS += tex2Dlod(_Displacement_c0, worldUV / LengthScale0) * lod_c0;
				largeWavesBias = displacementWS.y;
				#if defined(MID) || defined(CLOSE)
				displacementWS += tex2Dlod(_Displacement_c1, worldUV / LengthScale1) * lod_c1;
				#endif
				#if defined(CLOSE)
				displacementWS += tex2Dlod(_Displacement_c2, worldUV / LengthScale2) * lod_c2;
				#endif

				
				// Apply displacement in OS
				displacementWS.y *= _HeightScale;
				OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz) + displacementWS;
				OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
				OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
				
				OUT.lodScales = float4(lod_c0, lod_c1, lod_c2, max(displacementWS.y - largeWavesBias * 0.8 - 0, 0) / 1);

				return OUT;
			}

			float DotClamped(float3 a, float3 b) {
				return clamp(dot(a,b), 0, 1);
			}

			float SchlickFresnel(float3 normal, float3 viewDir) {
				return 0.02f + (1 - 0.02f) * (pow(1 - DotClamped(normal, viewDir), 5.0f));
			}

			float SmithMaskingBeckmann(float3 H, float3 S, float roughness) {
				float hdots = max(0.001f, DotClamped(H, S));
				float a = hdots / (roughness * sqrt(1 - hdots * hdots));
				float a2 = a * a;

				return a < 1.6f ? (1.0f - 1.259f * a + 0.396f * a2) / (3.535f * a + 2.181 * a2) : 0.0f;
			}

			float Beckmann(float ndoth, float roughness) {
				float exp_arg = (ndoth * ndoth - 1) / (roughness * roughness * ndoth * ndoth);

				return exp(exp_arg) / (PI * roughness * roughness * ndoth * ndoth * ndoth * ndoth);
			}

			half4 frag(Varyings IN) : SV_Target
			{
				Light sun = GetMainLight();

				half3 lightDir = sun.direction;
				half3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
				half3 halfwayDir = normalize(lightDir + viewDir);

				half LdotH = DotClamped(lightDir, halfwayDir);
				half VdotH = DotClamped(viewDir, halfwayDir);

				half3 macroNormal = IN.normalWS;

				float4 derivatives = tex2D(_Derivatives_c0, IN.uvWS / LengthScale0);
				#if defined(MID) || defined(CLOSE)
				derivatives += tex2D(_Derivatives_c1, IN.uvWS / LengthScale1) * IN.lodScales.y;
				#endif
				#if defined(CLOSE)
				derivatives += tex2D(_Derivatives_c2, IN.uvWS / LengthScale2) * IN.lodScales.z;
				#endif

				float2 slope = float2(derivatives.x / (1 + derivatives.z), derivatives.y / (1 + derivatives.w));
				float3 displacementNormalWS = normalize(float3(-slope.x, 1, -slope.y));
				half3 mesoNormal = displacementNormalWS;

				#if defined(CLOSE)
				float jacobian = tex2D(_Turbulence_c0, IN.uvWS / LengthScale0).x
					+ tex2D(_Turbulence_c1, IN.uvWS / LengthScale1).x
					+ tex2D(_Turbulence_c2, IN.uvWS / LengthScale2).x;
				jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD2) * _FoamScale));
				#elif defined(MID)
				float jacobian = tex2D(_Turbulence_c0, IN.uvWS / LengthScale0).x
					+ tex2D(_Turbulence_c1, IN.uvWS / LengthScale1).x;
				jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD1) * _FoamScale));
				#else
				float jacobian = tex2D(_Turbulence_c0, IN.uvWS / LengthScale0).x;
				jacobian = min(1, max(0, (-jacobian + _FoamBiasLOD0) * _FoamScale));
				#endif

				float foam = jacobian;

				// PBR Scatter model
				half NdotL = DotClamped(mesoNormal, lightDir);

				half a = _Roughness + foam;
				half ndoth = max(0.0001f, dot(mesoNormal, halfwayDir));

				// half viewMask = G_MaskingSmithGGX(DotClamped(mesoNormal, viewDir), a);
				// half lightMask = G_MaskingSmithGGX(NdotL, a);

				half viewMask = SmithMaskingBeckmann(halfwayDir, viewDir, a);
				half lightMask = SmithMaskingBeckmann(halfwayDir, lightDir, a);
				
				half G = rcp(1 + viewMask + lightMask);

				half eta = 1.33f;
				half R = ((eta - 1) * (eta - 1)) / ((eta + 1) * (eta + 1));
				half thetaV = acos(viewDir.y);

				half numerator = pow(1 - dot(mesoNormal, viewDir), 5 * exp(-2.69 * a));
				half F = R + (1 - R) * numerator / (1.0f + 22.7f * pow(a, 1.5f));
				F = saturate(F);
				
				half3 specular = sun.color * F * G * Beckmann(ndoth, a);
				specular /= 4.0f * max(0.001f, DotClamped(macroNormal, lightDir));
				specular *= DotClamped(mesoNormal, lightDir);

				half3 irradiance = _EnvironmentLightStrength * CalculateIrradianceFromReflectionProbes(reflect(-viewDir, mesoNormal), IN.positionWS, a);

				half waveHeight = max(0.0f, IN.positionWS.y) * _HeightScale;
				half3 scatterColor = _ScatterColor.xyz;
				half3 bubbleColor = _BubbleColor.xyz;
				half bubbleDensity = _BubbleDensity;

				
				half k1 = _WavePeakScatterStrength * waveHeight * pow(DotClamped(lightDir, -viewDir), 4.0f) * pow(0.5f - 0.5f * dot(lightDir, mesoNormal), 3.0f);
				half k2 = _ScatterStrength * pow(DotClamped(viewDir, mesoNormal), 2.0f);
				half k3 = _ScatterShadowStrength * NdotL;
				half k4 = bubbleDensity;

				half3 scatter = (k1 + k2) * scatterColor * sun.color * rcp(1 + lightMask);
				scatter += k3 * scatterColor * sun.color + k4 * bubbleColor * sun.color;

				half3 output = (1 - F) * scatter + specular + F * irradiance;
				output = max(0.0f, output);
				output = lerp(output, _FoamColor, saturate(foam));


				// DEBUGS
				// return half4(mesoNormal * 0.5 + 0.5, 1);
				// return half4(foam,foam,foam, 1);

				return half4(output, 1);
			}
			ENDHLSL
		}

	}
	Fallback "Lit"
}
