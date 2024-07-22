using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class FFTWaterRendererFeature : ScriptableRendererFeature
{

    public enum PatchSize
    {
        Low = 64,
        Medium = 128,
        High = 256,
    }
    public PatchSize patchSize = PatchSize.Low;


    public ComputeShader fftShader;
    private FFTComputePass fftPass;

    /// Gets run when settings change or it is enabled/disabled
    /// 
    /// <inheritdoc/>
    public override void Create()
    {
        // TODO: Generate Gaussian Noise

        // fftPass = new FFTComputePass((uint)patchSize, null, TextureHandle.nullHandle, fftShader);
        // fftPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        // TODO: Precompute fft data? this may be done in the pass
        // TODO: Compute initial Spectrum
        base.SetupRenderPasses(renderer, renderingData);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Check if the system support compute shaders, if not make an early exit.
        if (!SystemInfo.supportsComputeShaders)
        {
            Debug.LogWarning("Device does not support compute shaders. The pass will be skipped.");
            return;
        }
        // TODO:
    }
}
