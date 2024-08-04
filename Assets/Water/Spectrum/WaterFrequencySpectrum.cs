using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace Water.Spectrum
{
    [Serializable]
    public abstract class WaterFrequencySpectrum : ScriptableObject
    {
        /// <summary>
        /// Used to sample initial wave directions and sample the spectrum using those waves samples.
        /// </summary>
        /// <param name="cascade">Cascade to write information to.</param>
        public abstract void SampleSpectrum(SpectrumCascade cascade);

        public float g = 9.81f;
        public float depth = 500f;
        [Range(0, 1)]
        public float lambda = 1;

        Texture2D noise;

        protected WaterSimulationSettings settings;
        ComputeShader initialSpectrumShader;
        int KERNEL_INITIAL_SPECTRUM;
        int KERNEL_CONJ_SPECTRUM;
        ComputeShader evolveSpectrumShader;
        int KERNEL_FOURIER_SIGNALS;

        void OnSettingsUpdate(WaterSimulationSettings settings, string propertyName)
        {
            Debug.Log($"Water settings updated for {this.name}", this);
            this.settings = settings;
        }

        public virtual void OnEnable()
        {
            settings = GraphicsSettings.GetRenderPipelineSettings<WaterSimulationSettings>();
            GraphicsSettings.Subscribe<WaterSimulationSettings>(OnSettingsUpdate);
        }

        public virtual void Reset() { }

        public virtual void OnDisable()
        {
            GraphicsSettings.Unsubscribe<WaterSimulationSettings>(OnSettingsUpdate);
        }

        // static float NormalRandom()
        // {
        //     return Mathf.Cos(2 * Mathf.PI * UnityEngine.Random.value) * Mathf.Sqrt(-2 * Mathf.Log(UnityEngine.Random.value));
        // }

        // void GenerateNoiseTexture(WaterSimulationSettings settings)
        // {
        //     var size = (int)settings.renderingPatchSize;
        //     noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true)
        //     {
        //         filterMode = FilterMode.Point
        //     };
        //     for (int i = 0; i < size; i++)
        //     {
        //         for (int j = 0; j < size; j++)
        //         {
        //             noise.SetPixel(i, j, new Vector4(NormalRandom(), NormalRandom()));
        //         }
        //     }
        //     noise.Apply();
        // }

        public void CalculateInitials(SpectrumCascade cascade, Texture noise)
        {
            var size = cascade.spectrumSamplesTexture.width;

            initialSpectrumShader = settings.InitialComplexSpectrumShader;
            KERNEL_INITIAL_SPECTRUM = initialSpectrumShader.FindKernel("CalculateInitialSpectrum");
            KERNEL_CONJ_SPECTRUM = initialSpectrumShader.FindKernel("CalculateConjugateSpectrum");

            initialSpectrumShader.SetFloat(LENGTH_SCALE_PROP, cascade.lengthScale);
            initialSpectrumShader.SetFloat(CUTOFF_HIGH_PROP, cascade.cutoffHigh);
            initialSpectrumShader.SetFloat(CUTOFF_LOW_PROP, cascade.cutoffLow);

            initialSpectrumShader.SetFloat(G_PROP, g);
            initialSpectrumShader.SetFloat(DEPTH_PROP, depth);

            initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, WAVES_TEX_PROP, cascade.wavesTexture);
            initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, SAMPLES_TEX_PROP, cascade.spectrumSamplesTexture);

            initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, H0_TEX_PROP, cascade.complexSpectrumTexture);
            initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, H0K_TEX_PROP, cascade.initialSpectrumTexture);
            initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, NOISE_TEX_PROP, noise);

            initialSpectrumShader.SetInt(SIZE_PROP, size);

            initialSpectrumShader.Dispatch(KERNEL_INITIAL_SPECTRUM, size / 8, size / 8, 1);

            initialSpectrumShader.SetTexture(KERNEL_CONJ_SPECTRUM, H0K_TEX_PROP, cascade.initialSpectrumTexture);
            initialSpectrumShader.SetTexture(KERNEL_CONJ_SPECTRUM, H0_TEX_PROP, cascade.complexSpectrumTexture);
            initialSpectrumShader.Dispatch(KERNEL_CONJ_SPECTRUM, size / 8, size / 8, 1);
        }

        public void Evolve(SpectrumCascade cascade, float time)
        {
            if (time < 0) return;

            var size = cascade.spectrumSamplesTexture.width;

            evolveSpectrumShader = settings.EvolveComplexSpectrumShader;
            KERNEL_FOURIER_SIGNALS = evolveSpectrumShader.FindKernel("CalculateFourierSignals");

            // Calculating complex amplitudes
            evolveSpectrumShader.SetTexture(KERNEL_FOURIER_SIGNALS, Dx_Dz_TEX_PROP, cascade.DxDz);
            evolveSpectrumShader.SetTexture(KERNEL_FOURIER_SIGNALS, Dy_Dxz_TEX_PROP, cascade.DyDxz);
            evolveSpectrumShader.SetTexture(KERNEL_FOURIER_SIGNALS, Dyx_Dyz_TEX_PROP, cascade.DyxDyz);
            evolveSpectrumShader.SetTexture(KERNEL_FOURIER_SIGNALS, Dxx_Dzz_TEX_PROP, cascade.DxxDzz);
            evolveSpectrumShader.SetTexture(KERNEL_FOURIER_SIGNALS, H0_TEX_PROP, cascade.complexSpectrumTexture);
            evolveSpectrumShader.SetTexture(KERNEL_FOURIER_SIGNALS, WAVES_TEX_PROP, cascade.wavesTexture);
            evolveSpectrumShader.SetFloat(TIME_PROP, time);
            evolveSpectrumShader.Dispatch(KERNEL_FOURIER_SIGNALS, size / 8, size / 8, 1);
        }

        public void CalculateDisplacement(SpectrumCascade cascade, FastFourierTransform fft, float deltaTime)
        {
            var size = cascade.spectrumSamplesTexture.width;

            // Calculating IFFTs of complex amplitudes
            fft.IFFT2D(cascade.DxDz, cascade.initialSpectrumTexture, true, false, true);
            fft.IFFT2D(cascade.DyDxz, cascade.initialSpectrumTexture, true, false, true);
            fft.IFFT2D(cascade.DyxDyz, cascade.initialSpectrumTexture, true, false, true);
            fft.IFFT2D(cascade.DxxDzz, cascade.initialSpectrumTexture, true, false, true);

            var texturesMergerShader = settings.TextureMergerShader;
            var KERNEL_RESULT_TEXTURES = texturesMergerShader.FindKernel("FillResultTextures");

            // Filling displacement and normals textures
            texturesMergerShader.SetFloat("_DeltaTime", deltaTime);

            texturesMergerShader.SetTexture(KERNEL_RESULT_TEXTURES, Dx_Dz_TEX_PROP, cascade.DxDz);
            texturesMergerShader.SetTexture(KERNEL_RESULT_TEXTURES, Dy_Dxz_TEX_PROP, cascade.DyDxz);
            texturesMergerShader.SetTexture(KERNEL_RESULT_TEXTURES, Dyx_Dyz_TEX_PROP, cascade.DyxDyz);
            texturesMergerShader.SetTexture(KERNEL_RESULT_TEXTURES, Dxx_Dzz_TEX_PROP, cascade.DxxDzz);
            texturesMergerShader.SetTexture(KERNEL_RESULT_TEXTURES, DISPLACEMENT_TEX_PROP, cascade.displacement);
            texturesMergerShader.SetTexture(KERNEL_RESULT_TEXTURES, DERIVATIVES_TEX_PROP, cascade.derivatives);
            texturesMergerShader.SetTexture(KERNEL_RESULT_TEXTURES, TURBULENCE_TEX_PROP, cascade.turbulence);
            texturesMergerShader.SetFloat(LAMBDA_PROP, lambda);
            texturesMergerShader.Dispatch(KERNEL_RESULT_TEXTURES, size / 8, size / 8, 1);

            cascade.derivatives.GenerateMips();
            cascade.turbulence.GenerateMips();
        }

        // Textures
        protected readonly int WAVES_TEX_PROP = Shader.PropertyToID("_WavesData");
        protected readonly int SAMPLES_TEX_PROP = Shader.PropertyToID("_SpectrumSamples");
        readonly int H0K_TEX_PROP = Shader.PropertyToID("_H0K");
        readonly int H0_TEX_PROP = Shader.PropertyToID("_H0");
        readonly int NOISE_TEX_PROP = Shader.PropertyToID("_Noise");
        readonly int Dx_Dz_TEX_PROP = Shader.PropertyToID("_Dx_Dz");
        readonly int Dy_Dxz_TEX_PROP = Shader.PropertyToID("_Dy_Dxz");
        readonly int Dyx_Dyz_TEX_PROP = Shader.PropertyToID("_Dyx_Dyz");
        readonly int Dxx_Dzz_TEX_PROP = Shader.PropertyToID("_Dxx_Dzz");
        readonly int DISPLACEMENT_TEX_PROP = Shader.PropertyToID("_Displacement");
        readonly int DERIVATIVES_TEX_PROP = Shader.PropertyToID("_Derivatives");
        readonly int TURBULENCE_TEX_PROP = Shader.PropertyToID("_Turbulence");

        // Globals
        protected readonly int TIME_PROP = Shader.PropertyToID("_Time");
        protected readonly int SIZE_PROP = Shader.PropertyToID("_Size");
        protected readonly int G_PROP = Shader.PropertyToID("_GravityAcceleration");
        protected readonly int DEPTH_PROP = Shader.PropertyToID("_Depth");
        protected readonly int LAMBDA_PROP = Shader.PropertyToID("_Lambda");

        // Cascades
        protected readonly int LENGTH_SCALE_PROP = Shader.PropertyToID("_LengthScale");
        protected readonly int CUTOFF_HIGH_PROP = Shader.PropertyToID("_CutoffHigh");
        protected readonly int CUTOFF_LOW_PROP = Shader.PropertyToID("_CutoffLow");
    }

}
