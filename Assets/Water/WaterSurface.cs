using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Rendering;
using Water.Spectrum;

namespace Water
{
    // [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public partial class WaterSurface : MonoBehaviour
    {
        // public WaterFrequencySpectrum spectrum;
        public WaterFrequencySpectrum[] spectrums;
        // TODO: make T in range using custom editor
        [Min(0)] public float SpectrumInterpolationParameter;

        public Texture2D gaussianNoise;

        public float lengthScale0 = 250;
        public float lengthScale1 = 17;
        public float lengthScale2 = 5;

        // [Header("Debug")]
        public SpectrumCascade cascade0;
        public SpectrumCascade cascade1;
        public SpectrumCascade cascade2;

        protected WaterSimulationSettings waterSimSettings;
        protected SpectrumCascade cascadePhys;
        protected FastFourierTransform renderingFFT;
        protected FastFourierTransform physicsFFT;

        private Texture2D displacementReadback;
        private Texture2D derivativesReadback;
        private float readbackRequestTime = float.NaN;
        private float deltaReadbackTime;

        protected void InitOrReset(WaterSimulationSettings settings, bool shouldDispose = false)
        {
            waterSimSettings = settings;

            if (shouldDispose)
            {
                Dispose();
            }

            float boundary1 = 2 * Mathf.PI / lengthScale1 * 6f;
            float boundary2 = 2 * Mathf.PI / lengthScale2 * 6f;
            cascade0 = new SpectrumCascade($"{name}_{nameof(cascade0)}", (int)settings.renderingPatchSize, 250, 0.0001f, boundary1);
            cascade1 = new SpectrumCascade($"{name}_{nameof(cascade1)}", (int)settings.renderingPatchSize, 250, boundary1, boundary2);
            cascade2 = new SpectrumCascade($"{name}_{nameof(cascade2)}", (int)settings.renderingPatchSize, 250, boundary2, 9999.0f);
            cascadePhys = new SpectrumCascade($"{name}_{nameof(cascadePhys)}", (int)settings.physicsPatchSize, 250, 0.0001f, boundary1);
            renderingFFT = new FastFourierTransform((int)settings.renderingPatchSize, settings.FFTShader);
            physicsFFT = new FastFourierTransform((int)settings.physicsPatchSize, settings.FFTShader);
            displacementReadback = new Texture2D((int)settings.physicsPatchSize, (int)settings.physicsPatchSize);
            derivativesReadback = new Texture2D((int)settings.physicsPatchSize, (int)settings.physicsPatchSize);
        }

        Action<WaterSimulationSettings, string> OnSettingsUpdate => (settings, name) =>
        {
            InitOrReset(settings, name == nameof(settings.renderingPatchSize) || name == nameof(settings.physicsPatchSize));
        };

        protected void OnEnable()
        {
            InitOrReset(GraphicsSettings.GetRenderPipelineSettings<WaterSimulationSettings>(), true);
            GraphicsSettings.Subscribe(OnSettingsUpdate);
        }

        protected void OnDisable()
        {
            GraphicsSettings.Unsubscribe(OnSettingsUpdate);

            Dispose();
        }

        private WaterFrequencySpectrum GetLerpedSpectrum()
        {
            if (spectrums.Length == 0) return null;
            if (spectrums.Length == 1) return spectrums[0];

            var range = Mathf.Min(SpectrumInterpolationParameter, spectrums.Length - 1);
            var lower = Mathf.FloorToInt(range);
            var upper = Mathf.CeilToInt(range);
            var lerp = range - lower;

            return spectrums[lower].Lerp(spectrums[upper], lerp);
        }

        protected void Update()
        {
            var spectrum = GetLerpedSpectrum();

            if (cascade0 == null || cascade2 == null || cascade1 == null) return;
            if (spectrum == null) return;
            if (gaussianNoise == null) return;

            Shader.SetGlobalFloat("_LengthScale0", lengthScale0);
            Shader.SetGlobalFloat("_LengthScale1", lengthScale1);
            Shader.SetGlobalFloat("_LengthScale2", lengthScale2);

            // Update spectrum slices
            float boundary1 = 2 * Mathf.PI / lengthScale1 * 6f;
            float boundary2 = 2 * Mathf.PI / lengthScale2 * 6f;
            cascade0.lengthScale = lengthScale0;
            cascade1.lengthScale = lengthScale1;
            cascade2.lengthScale = lengthScale2;
            cascade0.cutoffHigh = boundary1;
            cascade1.cutoffLow = boundary1;
            cascade1.cutoffHigh = boundary2;
            cascade2.cutoffLow = boundary2;

            // Calculation in fourier space (spectrums aka frequencies)
            spectrum.SampleSpectrum(cascade0);
            spectrum.SampleSpectrum(cascade1);
            spectrum.SampleSpectrum(cascade2);
            spectrum.CalculateInitials(cascade0, gaussianNoise);
            spectrum.CalculateInitials(cascade1, gaussianNoise);
            spectrum.CalculateInitials(cascade2, gaussianNoise);
            spectrum.Evolve(cascade0, Time.time);
            spectrum.Evolve(cascade1, Time.time);
            spectrum.Evolve(cascade2, Time.time);

            // Tranform to time domain
            spectrum.CalculateDisplacement(cascade0, renderingFFT, Time.deltaTime);
            spectrum.CalculateDisplacement(cascade1, renderingFFT, Time.deltaTime);
            spectrum.CalculateDisplacement(cascade2, renderingFFT, Time.deltaTime);
        }

        protected void FixedUpdate()
        {
            var spectrum = GetLerpedSpectrum();

            if (cascadePhys == null) return;
            if (spectrum == null) return;
            if (gaussianNoise == null) return;

            // Update spectrum slices
            cascadePhys.lengthScale = cascade0.lengthScale;
            cascadePhys.cutoffHigh = cascade0.cutoffHigh;

            spectrum.SampleSpectrum(cascadePhys);
            spectrum.CalculateInitials(cascadePhys, gaussianNoise);
            spectrum.Evolve(cascadePhys, Time.fixedTime + Time.fixedDeltaTime * deltaReadbackTime * 2);
            spectrum.CalculateDisplacement(cascadePhys, physicsFFT, Time.fixedDeltaTime);
            RequestReadbacks();
        }

        void RequestReadbacks()
        {
            if (!float.IsNaN(readbackRequestTime)) return;
            readbackRequestTime = Time.time;
            AsyncGPUReadback.Request(cascadePhys.displacement, 0, TextureFormat.RGBAFloat, OnCompleteReadback);
            AsyncGPUReadback.Request(cascadePhys.derivatives, 0, TextureFormat.RGBAFloat, request => OnCompleteReadback(request, derivativesReadback));
        }

        public float GetWaterHeight(Vector3 position)
        {
            // Newton approx. 4 iters
            Vector3 displacement = GetWaterDisplacement(position);
            displacement = GetWaterDisplacement(position - displacement);
            displacement = GetWaterDisplacement(position - displacement);

            return GetWaterDisplacement(position - displacement).y;
        }

        public Vector3 GetWaterDisplacement(Vector3 position)
        {
            Color c = displacementReadback.GetPixelBilinear(position.x / lengthScale0, position.z / lengthScale0);
            return new Vector3(c.r, c.g, c.b);
        }

        public Vector3 GetWaterNormal(Vector3 position)
        {
            Vector3 displacement = GetWaterDisplacement(position);
            displacement = GetWaterDisplacement(position - displacement);
            displacement = GetWaterDisplacement(position - displacement);
            position = GetWaterDisplacement(position - displacement);

            Color c = derivativesReadback.GetPixelBilinear(position.x / lengthScale0, position.z / lengthScale0);
            return new Vector3(c.r / (1 + c.b), 1, c.g / (1 + c.a));
        }

        void OnCompleteReadback(AsyncGPUReadbackRequest request)
        {
            if (displacementReadback != null)
            {
                deltaReadbackTime = Time.time - readbackRequestTime;
                readbackRequestTime = float.NaN;
            }
            OnCompleteReadback(request, displacementReadback);
        }

        void OnCompleteReadback(AsyncGPUReadbackRequest request, Texture2D result)
        {
            if (request.hasError)
            {
                Debug.Log("GPU readback error detected.");
                return;
            }
            if (result != null)
            {
                result.LoadRawTextureData(request.GetData<Color>());
                result.Apply();
            }
        }

        void Dispose()
        {
            if (cascade0 != null) { cascade0.Dispose(); }
            if (cascade1 != null) { cascade1.Dispose(); }
            if (cascade2 != null) { cascade2.Dispose(); }
            if (cascadePhys != null) { cascadePhys.Dispose(); }
            if (renderingFFT != null) { renderingFFT.Dispose(); }
            if (physicsFFT != null) { physicsFFT.Dispose(); }
            if (displacementReadback != null) { DestroyImmediate(displacementReadback); }
        }
    }
}
