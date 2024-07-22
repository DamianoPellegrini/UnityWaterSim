Shader "Universal Render Pipeline/Nature/Water/SumOfSines"
{
	Properties
	{
		_WavesCount("Waves count", Integer) = 20
		_HeightScale("Height displacement scale", Float) = 1
		_InitialWave("Initial wave", Float) = 1

		_FrequencyGrowth("Frequency growth", Range(1.00001, 3.0)) = 1.14
		_AmplitudeFallOff("Amplitude fall-off", Range(0.00001, 1.0)) = 0.86
		_SpeedRamp("Speed ramp", Range(0.00001, 1)) = 1.1

		// Scatter
		_WavePeakScatterStrength("Wave Peak Scatter strength", Range(0, 1)) = 1
		_ScatterStrength("Scatter strength", Range(0, 1)) = 0.1
		[MainColor] _ScatterColor("Scatter color", Color) = (0, 0.2, 1, 1)

		// Diffuse
		_ScatterShadowStrength("Scatter Shadow strength", Range(0, 1)) = 0.3
		_BubbleDensity("Bubble density (ambient strength)", Range(0, 1)) = 0.75
		_BubbleColor("Bubble color (ambient color)", Color) = (0, 0, 0.25, 1)

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
			// Vertex
			int _WavesCount;
			half _FrequencyGrowth;
			half _AmplitudeFallOff;
			half _SpeedRamp;
			half _HeightScale;

			// Fragment
			half _EnvironmentLightStrength;
			half _WavePeakScatterStrength;
			half _ScatterStrength;
			half4 _ScatterColor;
			half _ScatterShadowStrength;
			half _BubbleDensity;
			half4 _BubbleColor;
			half _Roughness;
			CBUFFER_END

			struct Attributes {
				float4 positionOS   : POSITION;
				float3 normalOS : NORMAL;
			};

			struct Varyings {
				float4 positionHCS  : SV_POSITION;
				float3 positionWS : TEXCOORD0;
				float3 normalWS : TEXCOORD1;
			};

			struct Wave {
				half frequency;
				half amplitude;
				half speed;
				half2 direction;
				half steepness;
			};

			float WavePhase(Wave w) {
				return w.speed * w.frequency;
			}

			float GerstnerVariable(Wave w, float2 positionWS, float time) {
				return dot(positionWS, w.direction) * w.frequency + time * WavePhase(w);
			}

			float3 Gerstner(Wave w, float2 positionWS, float time) {
				float x = GerstnerVariable(w, positionWS, time);

				float3 displ = float3(0.0f, 0.0f, 0.0f);

				displ.x = w.steepness * w.amplitude * w.direction.x * cos(x);
				displ.y = w.steepness * w.amplitude * w.direction.y * cos(x);
				displ.z = w.amplitude * sin(x);

				return displ;
			}

			float3 GerstnerNormal(Wave w, float2 positionWS, float time) {
				float x = GerstnerVariable(w, positionWS, time);
				float s = sin(x);
				float c = cos(x);
				float omegaa = w.frequency * w.amplitude;

				float3 n = float3(0.0f, 0.0f, 0.0f);

				n.x = w.direction.x * omegaa * c;
				n.y = w.direction.y * omegaa * c;
				n.z = w.steepness * omegaa * s;

				return n;
			}

			Varyings vert(Attributes IN)
			{
				Varyings OUT;
				OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
				
				Wave w = (Wave)0;
				w.frequency = 1;
				w.amplitude = 1;
				w.speed = 1;
				w.direction = normalize(half2(1,1));
				w.steepness = 0.75;
				
				// Calculate displacement in WS
				float3 displacementOS = Gerstner(w, OUT.positionWS.xz, _Time.y).xzy;
				
				// Fractional Brownian Motion
				float2 position = OUT.positionWS.xz;
				int seed=0;
				float amplSum = 0.0;
				for (int i = 0; i < _WavesCount; i++) {
					displacementOS += Gerstner(w, position, _Time.y).xzy;
					float3 dx = GerstnerNormal(w, position, _Time.y).xzy;
					// displacementNormalOS += dx;
					amplSum += w.amplitude;

					position += -dx.xz * 0.0001;

					// Calc for next itered wave;
					w.amplitude *= _AmplitudeFallOff;
					w.frequency *= _FrequencyGrowth;
					w.speed *= _SpeedRamp;
					w.direction = normalize(float2(cos(seed), sin(seed)));
					seed += 20;
				}
				displacementOS /= amplSum;

				// Apply displacement in OS
				displacementOS.y *= _HeightScale;
				OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz + displacementOS);
				OUT.positionHCS = TransformWorldToHClip(OUT.positionWS);
				OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);

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

				half3 macroNormal = half3(0, 1, 0);
				half3 mesoNormal = IN.normalWS;

				// Calculate exact normal
				// Fractional Brownian Motion

				Wave w = (Wave)0;
				w.frequency = 1;
				w.amplitude = 1;
				w.speed = 1;
				w.direction = normalize(half2(1,1));
				w.steepness = 0.75;
				
				float2 position = IN.positionWS.xz;
				float3 displacementNormalOS = GerstnerNormal(w, position, _Time.y).xzy;
				int seed=0;
				float amplSum = 0.0;
				for (int i = 0; i < _WavesCount; i++) {
					float3 dx = GerstnerNormal(w, position, _Time.y).xzy;
					displacementNormalOS += dx;
					amplSum += w.amplitude;

					position += -dx.xz * 0.0001;
				
					// Calc for next itered wave;
					w.amplitude *= _AmplitudeFallOff;
					w.frequency *= _FrequencyGrowth;
					w.speed *= _SpeedRamp;
					w.direction = normalize(float2(cos(seed), sin(seed)));
					seed += 20;
				}
				displacementNormalOS /= amplSum;
				mesoNormal = normalize(IN.normalWS - displacementNormalOS);


				// PBR Scatter model
				half NdotL = DotClamped(mesoNormal, lightDir);

				half a = _Roughness;// + foam * _FoamRoughness;
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
				// output = lerp(output, _FoamColor, saturate(foam));

				// return half4(mesoNormal * 0.5 + 0.5, 1);

				return half4(output, 1);
			}
			ENDHLSL
		}

	}
	Fallback "Lit"
}
