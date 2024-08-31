using System;
using UnityEngine;
using UnityEngine.Rendering;
using Water.Spectrum;
using Water.Wakes;

namespace Water
{
    // [ExecuteInEditMode]
    [DisallowMultipleComponent]
    public partial class WakesSurface : MonoBehaviour
    {
        private WaterSimulationSettings waterSimSettings;
        public ComputeShader wakesShader;
        public Texture trailTex;
        public Texture gaussianNoise;
        public WaterFrequencySpectrum spectrum;
        public SpectrumCascade cascade0;
        public WakeCascade wakeCascade0;

        protected FastFourierTransform renderingFFT;

        void InitOrReset(WaterSimulationSettings settings, bool shouldDispose = false)
        {
            waterSimSettings = settings;
            if (shouldDispose)
            {
                Dispose();
            }

            cascade0 = new SpectrumCascade($"{name}_{nameof(cascade0)}", (int)settings.renderingPatchSize, 250, 0.0001f, 9999.0f);
            wakeCascade0 = new WakeCascade($"{name}_{nameof(wakeCascade0)}", (int)settings.renderingPatchSize, cascade0);
            renderingFFT = new FastFourierTransform((int)settings.renderingPatchSize, settings.FFTShader);
        }

        Action<WaterSimulationSettings, string> OnSettingsUpdate => (settings, name) =>
        {
            InitOrReset(settings, name == nameof(settings.renderingPatchSize) || name == nameof(settings.physicsPatchSize));
        };

        void OnEnable()
        {
            InitOrReset(GraphicsSettings.GetRenderPipelineSettings<WaterSimulationSettings>(), true);
            GraphicsSettings.Subscribe(OnSettingsUpdate);

            // TODO: tmp
            var renderer = GetComponent<MeshRenderer>();
            renderer.materials[0].SetTexture("_BaseMap", cascade0.displacement);
        }

        void OnDisable()
        {
            GraphicsSettings.Unsubscribe(OnSettingsUpdate);

            Dispose();
        }

        void Update()
        {

            if (cascade0 == null || wakeCascade0 == null) return;
            if (spectrum == null) return;
            if (gaussianNoise == null) return;

            Shader.SetGlobalFloat("_LengthScale0", 1);

            // Calculation in fourier space (spectrums aka frequencies)
            spectrum.SampleSpectrum(cascade0);
            spectrum.CalculateInitials(cascade0, gaussianNoise);
            // spectrum.Evolve(cascade0, Time.time);

            // Inject Heights and mask
            wakesShader.SetTexture(0, "_InjectHeightMap", trailTex);
            wakesShader.SetTexture(0, "_HDynamicField", wakeCascade0.heightField);
            wakesShader.Dispatch(0, (int)waterSimSettings.renderingPatchSize / 8, (int)waterSimSettings.renderingPatchSize / 8, 1);

            // Takes the fields to fourier space
            renderingFFT.FFT2D(wakeCascade0.heightField, wakeCascade0.heightSpectrum, false);
            renderingFFT.FFT2D(wakeCascade0.potentialField, wakeCascade0.potentialSpectrum, true);

            // Evolve frequency dynamics
            wakesShader.SetTexture(1, "_HDynamicSpectrum", wakeCascade0.heightSpectrum);
            wakesShader.SetTexture(1, "_PhiDynamicSpectrum", wakeCascade0.potentialSpectrum);

            wakesShader.SetTexture(1, "_WavesData", cascade0.wavesTexture);
            wakesShader.SetTexture(1, "_H0", cascade0.complexSpectrumTexture);

            wakesShader.SetTexture(1, "_Dx_Dz", cascade0.DxDz);
            wakesShader.SetTexture(1, "_Dy_Dxz", cascade0.DyDxz);
            wakesShader.SetTexture(1, "_Dyx_Dyz", cascade0.DyxDyz);
            wakesShader.SetTexture(1, "_Dxx_Dzz", cascade0.DxxDzz);

            wakesShader.SetFloat("_Time", Time.time);
            wakesShader.SetFloat("_DeltaTime", Time.deltaTime);
            wakesShader.SetFloat("_GravityAcceleration", spectrum.g);
            wakesShader.Dispatch(1, (int)waterSimSettings.renderingPatchSize / 8, (int)waterSimSettings.renderingPatchSize / 8, 1);

            // Takes the fields back out of fourier space
            // renderingFFT.IFFT2D(wakeCascade0.heightSpectrum, wakeCascade0.heightField, false, false, false);
            // renderingFFT.IFFT2D(wakeCascade0.potentialSpectrum, wakeCascade0.potentialField, false, false, false);

            // Tranform to time domain
            spectrum.CalculateDisplacement(cascade0, renderingFFT, Time.deltaTime);
        }

        void Dispose()
        {
            if (cascade0 != null) { cascade0.Dispose(); }
            if (wakeCascade0 != null) { wakeCascade0.Dispose(); }
        }
    }
}
