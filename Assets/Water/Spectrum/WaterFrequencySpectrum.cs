

using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable]
public abstract class WaterFrequencySpectrum : ScriptableObject, IDisposable
{
    /// <summary>
    /// Called OnValidate aka when parameters change
    /// </summary>
    /// <param name="waves">Texture to write wave direction and magnitude to.</param>
    /// <param name="samples">Texture to write spectrum samples to.</param>
    public abstract void SampleSpectrum(ref RenderTexture waves, ref RenderTexture samples);

    public float g = 9.81f;
    public float depth = 500f;
    [Range(0, 1)]
    public float lambda = 1;

    public RenderTexture wavesTex;
    public RenderTexture samplesTex;
    public RenderTexture complexInitialSpectrumTexK;
    public RenderTexture complexInitialSpectrumTex;
    public Texture2D noise;

    protected WaterSimulationSettings settings;
    private int KERNEL_INITIAL_SPECTRUM;
    private ComputeShader initialSpectrumShader;

    private void OnSettingsUpdate(WaterSimulationSettings settings, string propertyName)
    {
        Debug.Log("Global settings updated");

        InitOrReset(settings);
    }

    public void Awake()
    {
        Debug.Log("General Awake");

        settings = GraphicsSettings.GetRenderPipelineSettings<WaterSimulationSettings>();
        InitOrReset(settings);

        GraphicsSettings.Subscribe<WaterSimulationSettings>(OnSettingsUpdate);
    }

    public void Reset()
    {
        settings = GraphicsSettings.GetRenderPipelineSettings<WaterSimulationSettings>();
        InitOrReset(settings);
    }

    public virtual void Dispose()
    {
        GraphicsSettings.Unsubscribe<WaterSimulationSettings>(OnSettingsUpdate);

        wavesTex.Release();
        samplesTex.Release();
        complexInitialSpectrumTex.Release();
        complexInitialSpectrumTexK.Release();
    }


    public virtual void OnValidate()
    {

        KERNEL_INITIAL_SPECTRUM = initialSpectrumShader.FindKernel("CalculateInitialSpectrum");
        initialSpectrumShader.SetFloat(LENGTH_SCALE_PROP, 250.0f);
        initialSpectrumShader.SetFloat(CUTOFF_HIGH_PROP, 9999.0f);
        initialSpectrumShader.SetFloat(CUTOFF_LOW_PROP, 0.0001f);

        initialSpectrumShader.SetFloat(G_PROP, g);
        initialSpectrumShader.SetFloat(DEPTH_PROP, depth);

        // README: Here all textures and resources SHOULD be allocated
        SampleAndCalculateComplexSpectrum();
    }

    protected virtual void InitOrReset(WaterSimulationSettings settings)
    {
        GenerateNoiseTexture(settings);
        initialSpectrumShader = settings.initialComplexSpectrumShader;
        ReAllocateIfNeeded(settings);

        SampleAndCalculateComplexSpectrum();
    }

    private void ReAllocateIfNeeded(WaterSimulationSettings settings)
    {
        if (wavesTex == null)
            wavesTex = WaterSimulationSettings.CreateRenderTexture((int)settings.renderingPatchSize, RenderTextureFormat.ARGBFloat);
        if (samplesTex == null)
            samplesTex = WaterSimulationSettings.CreateRenderTexture((int)settings.renderingPatchSize, RenderTextureFormat.RFloat);
        if (complexInitialSpectrumTex == null)
            complexInitialSpectrumTex = WaterSimulationSettings.CreateRenderTexture((int)settings.renderingPatchSize, RenderTextureFormat.ARGBFloat);
        if (complexInitialSpectrumTexK == null)
            complexInitialSpectrumTexK = WaterSimulationSettings.CreateRenderTexture((int)settings.renderingPatchSize, RenderTextureFormat.RGFloat);

        if (wavesTex != null && wavesTex.width != (int)settings.renderingPatchSize)
        {
            wavesTex.Release();
            wavesTex = WaterSimulationSettings.CreateRenderTexture((int)settings.renderingPatchSize, RenderTextureFormat.ARGBFloat);
        }

        if (samplesTex != null && samplesTex.width != (int)settings.renderingPatchSize)
        {
            samplesTex.Release();
            samplesTex = WaterSimulationSettings.CreateRenderTexture((int)settings.renderingPatchSize, RenderTextureFormat.RFloat);
        }

        if (complexInitialSpectrumTex != null && complexInitialSpectrumTex.width != (int)settings.renderingPatchSize)
        {
            complexInitialSpectrumTex.Release();
            complexInitialSpectrumTex = WaterSimulationSettings.CreateRenderTexture((int)settings.renderingPatchSize, RenderTextureFormat.ARGBFloat);
        }

        if (complexInitialSpectrumTexK != null && complexInitialSpectrumTexK.width != (int)settings.renderingPatchSize)
        {
            complexInitialSpectrumTexK.Release();
            complexInitialSpectrumTexK = WaterSimulationSettings.CreateRenderTexture((int)settings.renderingPatchSize, RenderTextureFormat.RGFloat);
        }
    }

    private static float NormalRandom()
    {
        return Mathf.Cos(2 * Mathf.PI * UnityEngine.Random.value) * Mathf.Sqrt(-2 * Mathf.Log(UnityEngine.Random.value));
    }

    private void GenerateNoiseTexture(WaterSimulationSettings settings)
    {
        var size = (int)settings.renderingPatchSize;
        noise = new Texture2D(size, size, TextureFormat.RGFloat, false, true)
        {
            filterMode = FilterMode.Point
        };
        for (int i = 0; i < size; i++)
        {
            for (int j = 0; j < size; j++)
            {
                noise.SetPixel(i, j, new Vector4(NormalRandom(), NormalRandom()));
            }
        }
        noise.Apply();
    }

    private void SampleAndCalculateComplexSpectrum()
    {
        SampleSpectrum(ref wavesTex, ref samplesTex);

        initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, WAVES_TEX_PROP, wavesTex);
        initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, SAMPLES_TEX_PROP, samplesTex);

        initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, H0_TEX_PROP, complexInitialSpectrumTex);
        initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, H0K_TEX_PROP, complexInitialSpectrumTexK);
        initialSpectrumShader.SetTexture(KERNEL_INITIAL_SPECTRUM, NOISE_TEX_PROP, noise);

        initialSpectrumShader.SetInt(SIZE_PROP, samplesTex.width);

        initialSpectrumShader.Dispatch(KERNEL_INITIAL_SPECTRUM, samplesTex.width / 8, samplesTex.height / 8, 1);
    }

    // Textures
    protected readonly int WAVES_TEX_PROP = Shader.PropertyToID("_WavesData");
    protected readonly int SAMPLES_TEX_PROP = Shader.PropertyToID("_SpectrumSamples");
    readonly int H0K_TEX_PROP = Shader.PropertyToID("_H0K");
    readonly int H0_TEX_PROP = Shader.PropertyToID("_H0");
    readonly int NOISE_TEX_PROP = Shader.PropertyToID("_Noise");

    // Globals
    protected readonly int SIZE_PROP = Shader.PropertyToID("_Size");
    protected readonly int G_PROP = Shader.PropertyToID("_GravityAcceleration");
    protected readonly int DEPTH_PROP = Shader.PropertyToID("_Depth");

    // Cascades TODO: See if these are global
    protected readonly int LENGTH_SCALE_PROP = Shader.PropertyToID("_LengthScale");
    protected readonly int CUTOFF_HIGH_PROP = Shader.PropertyToID("_CutoffHigh");
    protected readonly int CUTOFF_LOW_PROP = Shader.PropertyToID("_CutoffLow");
}
