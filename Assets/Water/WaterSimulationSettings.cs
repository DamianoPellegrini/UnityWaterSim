using System;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Water
{
    [Serializable]
    [SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
    public class WaterSimulationSettings : IRenderPipelineResources
    {
        public int version => 0;

        public bool isAvailableInPlayerBuild => true;

        public enum PatchSize : int
        {
            Low = 64,
            Medium = 128,
            High = 256,
            Ultra = 512,
            Extreme = 1024,
        }

        [SerializeField] PatchSize _renderingPatchSize = PatchSize.High;
        [SerializeField] PatchSize _physicsPatchSize = PatchSize.Medium;

        public PatchSize renderingPatchSize { get => _renderingPatchSize; set => this.SetValueAndNotify(ref _renderingPatchSize, value); }
        public PatchSize physicsPatchSize { get => _physicsPatchSize; set => this.SetValueAndNotify(ref _physicsPatchSize, value); }

        [Category("Compute Shaders")]
        [ResourcePath("Water/Spectrum/Phillips/SamplePhillips.compute")]
        [SerializeField] ComputeShader _phillipsSamplerShader;

        [ResourcePath("Water/Spectrum/JONSWAP/SampleJONSWAP.compute")]
        [SerializeField] ComputeShader _JONSWAPSamplerShader;

        [ResourcePath("Water/Spectrum/InitialComplexSpectrum.compute")]
        [SerializeField] ComputeShader _initialComplexSpectrumShader;

        [ResourcePath("Water/Spectrum/EvolveComplexSpectrum.compute")]
        [SerializeField] ComputeShader _evolveComplexSpectrumShader;

        [ResourcePath("Water/CooleyTukeyFFT.compute")]
        [SerializeField] ComputeShader _fftShader;

        [ResourcePath("Water/WavesTexturesMerger.compute")]
        [SerializeField] ComputeShader _textureMergerShader;

        public ComputeShader PhillipsSamplerShader { get => _phillipsSamplerShader; set => this.SetValueAndNotify(ref _phillipsSamplerShader, value); }
        public ComputeShader JONSWAPSamplerShader { get => _JONSWAPSamplerShader; set => this.SetValueAndNotify(ref _JONSWAPSamplerShader, value); }
        public ComputeShader InitialComplexSpectrumShader { get => _initialComplexSpectrumShader; set => this.SetValueAndNotify(ref _initialComplexSpectrumShader, value); }
        public ComputeShader EvolveComplexSpectrumShader { get => _evolveComplexSpectrumShader; set => this.SetValueAndNotify(ref _evolveComplexSpectrumShader, value); }
        public ComputeShader FFTShader { get => _fftShader; set => this.SetValueAndNotify(ref _fftShader, value); }
        public ComputeShader TextureMergerShader { get => _textureMergerShader; set => this.SetValueAndNotify(ref _textureMergerShader, value); }

        // [SerializeField] public Material waterMaterial;

        public static RenderTexture CreateRenderTexture(string name, int size, RenderTextureFormat format, bool useMips = false)
        {
            RenderTexture rt = new RenderTexture(size, size, 0,
                format, RenderTextureReadWrite.Linear)
            {
                name = name,
                useMipMap = useMips,
                autoGenerateMips = false,
                anisoLevel = 8,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Repeat,
                enableRandomWrite = true
            };
            rt.Create();
            return rt;
        }
    }
}
