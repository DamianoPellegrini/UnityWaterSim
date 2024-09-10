using NUnit.Framework.Constraints;
using Unity.Mathematics;
using UnityEngine;

namespace Water.Spectrum
{
    [CreateAssetMenu(fileName = "New Phillips Spectrum settings", menuName = "Water/Spectrums/Phillips spectrum")]
    public class PhillipsSpectrum : WaterFrequencySpectrum
    {
        private ComputeShader sampleSpectrumShader;
        private int KERNEL_SAMPLE;


        [Min(0.00001f)]
        public float alpha = 0.0001f;

        [Min(0)]
        public float windSpeed = 20.0f;

        [Range(0, 360)]
        public float windDirection = 32.0f;

        [Min(0.00001f)]
        public float suppressThreshold = 0.05f;

        public override void SampleSpectrum(SpectrumCascade cascade)
        {
            sampleSpectrumShader = settings.PhillipsSamplerShader;
            KERNEL_SAMPLE = sampleSpectrumShader.FindKernel("SampleSpectrum"); // Here so it updated when the shader changes

            if (sampleSpectrumShader == null) return;

            var size = cascade.spectrumSamplesTexture.width;

            sampleSpectrumShader.SetFloat("_Alpha", alpha);
            sampleSpectrumShader.SetFloat(WIND_SPEED_PROP, windSpeed);
            sampleSpectrumShader.SetFloat(WIND_DIRECTION_PROP, windDirection);
            sampleSpectrumShader.SetFloat(SUPPRESS_THRESH_PROP, suppressThreshold);

            sampleSpectrumShader.SetFloat(LENGTH_SCALE_PROP, cascade.lengthScale);
            sampleSpectrumShader.SetFloat(CUTOFF_HIGH_PROP, cascade.cutoffHigh);
            sampleSpectrumShader.SetFloat(CUTOFF_LOW_PROP, cascade.cutoffLow);

            sampleSpectrumShader.SetFloat(G_PROP, g);
            sampleSpectrumShader.SetFloat(DEPTH_PROP, depth);

            sampleSpectrumShader.SetTexture(KERNEL_SAMPLE, WAVES_TEX_PROP, cascade.wavesTexture);
            sampleSpectrumShader.SetTexture(KERNEL_SAMPLE, SAMPLES_TEX_PROP, cascade.spectrumSamplesTexture);

            sampleSpectrumShader.SetInt(SIZE_PROP, size);

            sampleSpectrumShader.Dispatch(KERNEL_SAMPLE, size / 8, size / 8, 1);
        }

        public override WaterFrequencySpectrum Lerp(WaterFrequencySpectrum end, float time)
        {
            if (time < 0) return this;
            if (time > 1) return end;

            var inst = CreateInstance<PhillipsSpectrum>();

            if (end is PhillipsSpectrum endPhil)
            {
                inst.depth = math.lerp(depth, end.depth, time);
                inst.g = math.lerp(g, end.g, time);
                inst.lambda = math.lerp(lambda, end.lambda, time);

                inst.alpha = math.lerp(alpha, endPhil.alpha, time);
                inst.windSpeed = math.lerp(windSpeed, endPhil.windSpeed, time);
                inst.windDirection = math.lerp(windDirection, endPhil.windDirection, time);
                inst.suppressThreshold = math.lerp(suppressThreshold, endPhil.suppressThreshold, time);
            }
            else
            {
                inst.depth = math.lerp(depth, end.depth, time);
                inst.g = math.lerp(g, end.g, time);
                inst.lambda = math.lerp(lambda, end.lambda, time);
                inst.alpha = alpha;
                inst.windSpeed = windSpeed;
                inst.windDirection = windDirection;
                inst.suppressThreshold = suppressThreshold;
            }

            return inst;
        }

        public override string CSVFormat()
        {
            return $"{base.CSVFormat()},{nameof(alpha)},{nameof(windSpeed)},{nameof(windDirection)},{nameof(suppressThreshold)}";
        }

        public override string ToCSV()
        {
            return $"{base.ToCSV()},{alpha},{windSpeed},{windDirection},{suppressThreshold}";
        }

        // Spectrum
        readonly int WIND_SPEED_PROP = Shader.PropertyToID("_WindSpeed");
        readonly int WIND_DIRECTION_PROP = Shader.PropertyToID("_WindDirection");
        readonly int SUPPRESS_THRESH_PROP = Shader.PropertyToID("_SuppressThreshold");

    }

}
