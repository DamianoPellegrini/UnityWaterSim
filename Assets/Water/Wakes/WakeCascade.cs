using System;
using UnityEngine;
using Water.Spectrum;

namespace Water.Wakes
{
    public class WakeCascade : IDisposable
    {
        public SpectrumCascade cascade;

        public RenderTexture heightSpectrum;
        public RenderTexture potentialSpectrum;
        public RenderTexture heightField;
        public RenderTexture potentialField;

        public WakeCascade(string name, int size, SpectrumCascade cascade)
        {
            heightSpectrum = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(heightSpectrum)}", size, RenderTextureFormat.RGFloat, false);
            potentialSpectrum = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(potentialSpectrum)}", size, RenderTextureFormat.RGFloat, false);
            heightField = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(heightField)}", size, RenderTextureFormat.RGFloat, false);
            potentialField = WaterSimulationSettings.CreateRenderTexture($"{name}_{nameof(potentialField)}", size, RenderTextureFormat.RGFloat, false);
        }

        public void Dispose()
        {
            heightSpectrum.Release();
            potentialSpectrum.Release();
            heightField.Release();
            potentialField.Release();
        }

    }
}
