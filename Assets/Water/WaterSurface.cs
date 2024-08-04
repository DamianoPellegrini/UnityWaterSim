using System;
using UnityEngine;
using UnityEngine.Rendering;
using Water.Spectrum;

namespace Water
{
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public class WaterSurface : MonoBehaviour
    {
        public WaterFrequencySpectrum spectrum;
        public Texture2D gaussianNoise;

        public float lengthScale0 = 250;
        public float lengthScale1 = 17;
        public float lengthScale2 = 5;

        private WaterSimulationSettings waterSimSettings;
        public SpectrumCascade cascade0;
        public SpectrumCascade cascade1;
        public SpectrumCascade cascade2;
        private SpectrumCascade cascadePhys;
        private FastFourierTransform renderingFFT;
        private FastFourierTransform physicsFFT;

        void InitOrReset(WaterSimulationSettings settings, bool shouldDispose = false)
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
        }

        Action<WaterSimulationSettings, string> OnSettingsUpdate => (settings, name) =>
        {
            InitOrReset(settings, name == nameof(settings.renderingPatchSize) || name == nameof(settings.physicsPatchSize));
        };

        void OnEnable()
        {
            InitOrReset(GraphicsSettings.GetRenderPipelineSettings<WaterSimulationSettings>());
            GraphicsSettings.Subscribe(OnSettingsUpdate);
        }

        void OnDisable()
        {
            GraphicsSettings.Unsubscribe(OnSettingsUpdate);

            Dispose();
        }

        void Update()
        {
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

            spectrum.SampleSpectrum(cascade0);
            spectrum.SampleSpectrum(cascade1);
            spectrum.SampleSpectrum(cascade2);
            spectrum.CalculateInitials(cascade0, gaussianNoise);
            spectrum.CalculateInitials(cascade1, gaussianNoise);
            spectrum.CalculateInitials(cascade2, gaussianNoise);
            spectrum.Evolve(cascade0, Time.time);
            spectrum.Evolve(cascade1, Time.time);
            spectrum.Evolve(cascade2, Time.time);

            spectrum.CalculateDisplacement(cascade0, renderingFFT, Time.deltaTime);
            spectrum.CalculateDisplacement(cascade1, renderingFFT, Time.deltaTime);
            spectrum.CalculateDisplacement(cascade2, renderingFFT, Time.deltaTime);
        }

        void sFixedUpdate()
        {
            if (spectrum == null) return;
            if (gaussianNoise == null) return;

            // Update spectrum slices
            cascadePhys.lengthScale = lengthScale0;
            cascadePhys.cutoffHigh = 2 * Mathf.PI / lengthScale1 * 6f;

            spectrum.SampleSpectrum(cascadePhys);
            spectrum.CalculateInitials(cascadePhys, gaussianNoise);
            // spectrum.Evolve(cascadePhys, Time.time + averageReadbackTime, Time.deltaTime);
            // TODO: Ask for readback
        }

        void Dispose()
        {
            if (cascade0 != null) { cascade0.Dispose(); }
            if (cascade1 != null) { cascade1.Dispose(); }
            if (cascade2 != null) { cascade2.Dispose(); }
            if (cascadePhys != null) { cascadePhys.Dispose(); }
            if (renderingFFT != null) { renderingFFT.Dispose(); }
            if (physicsFFT != null) { physicsFFT.Dispose(); }
        }
    }
}
