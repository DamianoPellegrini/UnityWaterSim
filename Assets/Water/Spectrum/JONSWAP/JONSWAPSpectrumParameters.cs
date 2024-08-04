using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
namespace Water.Spectrum
{
    [StructLayout(LayoutKind.Sequential)]
    [GenerateHLSL(PackingRules.Exact, false, generateCBuffer = false)]
    public struct JONSWAPSpectrumParameters
    {
        public float scale;
        public float spreadBlend;
        public float swell;
        public float shortWavesFade;
        public float angle;
        public float alpha;
        public float peakOmega;
        public float gamma;
    }


    [Serializable]
    public struct JONSWAPSpectrumBand
    {
        [Range(0, 1)]
        public float scale;

        [Header("Wind parameters")]

        [Min(0)]
        public float windSpeed;

        [Range(0, 359)]
        public float windDirection;

        [Min(0.1f)]
        public float fetch;

        [Header("Waves parameters")]
        [Range(0, 1)]
        public float spreadBlend;
        [Range(0.01f, 1)]
        public float swell;
        public float shortWavesFade;
        [Min(0.001f)]
        public float peakEnhancement;

        public JONSWAPSpectrumParameters GPUParameters(float g) => new()
        {
            scale = scale,
            spreadBlend = spreadBlend,
            swell = swell,
            shortWavesFade = shortWavesFade,
            angle = windDirection / 180f * Mathf.PI,
            alpha = JonswapAlpha(g, fetch, windSpeed),
            peakOmega = JonswapPeakFrequency(g, fetch, windSpeed),
            gamma = peakEnhancement
        };

        private float JonswapAlpha(float g, float fetch, float windSpeed)
        {
            return 0.076f * Mathf.Pow(g * fetch / windSpeed / windSpeed, -0.22f);
        }

        private float JonswapPeakFrequency(float g, float fetch, float windSpeed)
        {
            return 22 * Mathf.Pow(windSpeed * fetch / g / g, -0.33f);
        }
    }
}
