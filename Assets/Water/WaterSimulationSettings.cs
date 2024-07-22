using System;
using System.ComponentModel;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

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

    [SerializeField] private PatchSize _renderingPatchSize = PatchSize.High;
    [SerializeField] private PatchSize _physicsPatchSize = PatchSize.Medium;

    public PatchSize renderingPatchSize
    {
        get => _renderingPatchSize;
        set => this.SetValueAndNotify(ref _renderingPatchSize, value);
    }
    public PatchSize physicsPatchSize
    {
        get => _physicsPatchSize;
        set => this.SetValueAndNotify(ref _physicsPatchSize, value);
    }

    [Category("Compute Shaders")]
    [ResourcePath("Water/Spectrum/Phillips/SamplePhillips.compute")]
    public ComputeShader PhillipsSamplerShader;

    [ResourcePath("Water/Spectrum/JONSWAP/SampleJONSWAP.compute")]
    public ComputeShader JONSWAPSamplerShader;

    [ResourcePath("Water/Spectrum/InitialComplexSpectrum.compute")]
    public ComputeShader initialComplexSpectrumShader;
    public ComputeShader butterflyShader;
    public ComputeShader fftShader;

    //TODO: internal FastFourierTransform _renderingFFT;
    //TODO: internal FastFourierTransform _physicsFFT;

    [SerializeField] public Material waterMaterial;

    public static RenderTexture CreateRenderTexture(int size, RenderTextureFormat format, bool useMips = false)
    {
        RenderTexture rt = new RenderTexture(size, size, 0,
            format, RenderTextureReadWrite.Linear)
        {
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
