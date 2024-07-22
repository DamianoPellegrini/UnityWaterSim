using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "New JONSWAP Spectrum settings", menuName = "Water/Spectrums/JONSWAP spectrum")]
public class JONSWAPSpectrumSettings : WaterFrequencySpectrum
{
    private ComputeShader sampleSpectrumShader;
    private int KERNEL_SAMPLE;
    private ComputeBuffer spectrumBuffer;


    public JONSWAPSpectrum local = new()
    {
        scale = 1,
        windSpeed = 0.5f,
        windDirection = 76,
        fetch = 100000,
        spreadBlend = 1,
        swell = 0.01f,
        peakEnhancement = 3.3f,
        shortWavesFade = 0.01f
    };

    public JONSWAPSpectrum swell = new()
    {
        scale = 0.2f,
        windSpeed = 10f,
        windDirection = 50,
        fetch = 400000,
        spreadBlend = 1,
        swell = 1f,
        peakEnhancement = 3.3f,
        shortWavesFade = 0.01f
    };

    protected override void InitOrReset(WaterSimulationSettings settings)
    {
        sampleSpectrumShader = settings.JONSWAPSamplerShader;

        unsafe
        {
            spectrumBuffer = new ComputeBuffer(2, sizeof(JONSWAPSpectrumParameters));

            // Upload here aswell since OnValidate is not called when resetting
            UploadSpectrumBuffer();

        }

        base.InitOrReset(settings);
    }

    public override void Dispose()
    {
        base.Dispose();

        if (spectrumBuffer != null)
            spectrumBuffer.Release();
    }

    public override void OnValidate()
    {
        if (spectrumBuffer == null) return;

        KERNEL_SAMPLE = sampleSpectrumShader.FindKernel("SampleSpectrum"); // Here so it gets updated when the shader changes

        UploadSpectrumBuffer();

        sampleSpectrumShader.SetFloat(LENGTH_SCALE_PROP, 250);
        sampleSpectrumShader.SetFloat(CUTOFF_HIGH_PROP, 9999);
        sampleSpectrumShader.SetFloat(CUTOFF_LOW_PROP, 0.0001f);

        sampleSpectrumShader.SetFloat(G_PROP, g);
        sampleSpectrumShader.SetFloat(DEPTH_PROP, depth);

        base.OnValidate();
    }

    public override void SampleSpectrum(ref RenderTexture waves, ref RenderTexture samples)
    {
        if (waves == null) return;
        if (samples == null) return;

        sampleSpectrumShader.SetTexture(KERNEL_SAMPLE, WAVES_TEX_PROP, waves);
        sampleSpectrumShader.SetTexture(KERNEL_SAMPLE, SAMPLES_TEX_PROP, samples);

        sampleSpectrumShader.SetInt(SIZE_PROP, samples.width);

        sampleSpectrumShader.Dispatch(KERNEL_SAMPLE, samples.width / 8, samples.height / 8, 1);
    }

    private void UploadSpectrumBuffer()
    {
        var gpuReadySpectrums = new JONSWAPSpectrumParameters[2] { local.GPUReady(g), swell.GPUReady(g) };
        spectrumBuffer.SetData(gpuReadySpectrums);
        sampleSpectrumShader.SetBuffer(KERNEL_SAMPLE, SPECTRUMS_PROP, spectrumBuffer);
    }

    // Spectrum
    readonly int SPECTRUMS_PROP = Shader.PropertyToID("_Spectrums");
}
