using System;
using UnityEngine;

namespace Water.Spectrum
{
    public class SpectrumCascade : IDisposable
    {
        public float lengthScale;
        public float cutoffLow;
        public float cutoffHigh;

        // Spectrum samples
        public RenderTexture wavesTexture;
        public RenderTexture spectrumSamplesTexture;

        // Complex Samples
        /// <summary>
        /// Spectrum without its conjugate (only uses RG channels for real & im parts)
        /// </summary>
        public RenderTexture initialSpectrumTexture;
        /// <summary>
        /// Full spectrum with in addition to initial opposite conjugate on BA channels
        /// </summary>
        public RenderTexture complexSpectrumTexture;

        // Fourier Signals
        public RenderTexture DxDz;
        public RenderTexture DyDxz;
        public RenderTexture DyxDyz;
        public RenderTexture DxxDzz;

        // Results
        public RenderTexture displacement;
        public RenderTexture derivatives;
        public RenderTexture turbulence;

        public SpectrumCascade(string name, int size, float lengthScale, float cutoffLow, float cutoffHigh)
        {
            this.lengthScale = lengthScale;
            this.cutoffLow = cutoffLow;
            this.cutoffHigh = cutoffHigh;

            wavesTexture = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(wavesTexture)}", size, RenderTextureFormat.ARGBFloat, false);
            spectrumSamplesTexture = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(spectrumSamplesTexture)}", size, RenderTextureFormat.RFloat, false);
            initialSpectrumTexture = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(initialSpectrumTexture)}", size, RenderTextureFormat.RGFloat, false);
            complexSpectrumTexture = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(complexSpectrumTexture)}", size, RenderTextureFormat.ARGBFloat, false);
            DxDz = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(DxDz)}", size, RenderTextureFormat.RGFloat, false);
            DyDxz = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(DyDxz)}", size, RenderTextureFormat.RGFloat, false);
            DyxDyz = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(DyxDyz)}", size, RenderTextureFormat.RGFloat, false);
            DxxDzz = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(DxxDzz)}", size, RenderTextureFormat.RGFloat, false);
            displacement = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(displacement)}", size, RenderTextureFormat.ARGBFloat, false);
            derivatives = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(derivatives)}", size, RenderTextureFormat.ARGBFloat, true);
            turbulence = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(turbulence)}", size, RenderTextureFormat.RFloat, true);
        }

        public void Dispose()
        {
            wavesTexture.Release();
            spectrumSamplesTexture.Release();
            initialSpectrumTexture.Release();
            complexSpectrumTexture.Release();
            DxDz.Release();
            DyDxz.Release();
            DyxDyz.Release();
            DxxDzz.Release();
            displacement.Release();
            derivatives.Release();
            turbulence.Release();
        }

    }
}
