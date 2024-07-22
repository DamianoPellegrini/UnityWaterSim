using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "New Phillips Spectrum settings", menuName = "Water/Spectrums/Phillips spectrum")]
public class PhillipsSpectrumSettings : WaterFrequencySpectrum
{
    private ComputeShader sampleSpectrumShader;
    private int KERNEL_SAMPLE;

    public float windSpeed = 20.0f;
    public float windDirection = 32.0f;
    public float suppressThreshold = 0.001f;

    protected override void InitOrReset(WaterSimulationSettings settings)
    {
        sampleSpectrumShader = settings.PhillipsSamplerShader;

        base.InitOrReset(settings);
    }

    public override void OnValidate()
    {
        KERNEL_SAMPLE = sampleSpectrumShader.FindKernel("SampleSpectrum"); // Here so it updated when the shader changes

        sampleSpectrumShader.SetFloat(WIND_SPEED_PROP, windSpeed);
        sampleSpectrumShader.SetFloat(WIND_DIRECTION_PROP, windDirection);
        sampleSpectrumShader.SetFloat(SUPPRESS_THRESH_PROP, suppressThreshold);

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

    // Spectrum
    readonly int WIND_SPEED_PROP = Shader.PropertyToID("_WindSpeed");
    readonly int WIND_DIRECTION_PROP = Shader.PropertyToID("_WindDirection");
    readonly int SUPPRESS_THRESH_PROP = Shader.PropertyToID("_SuppressThreshold");

}
