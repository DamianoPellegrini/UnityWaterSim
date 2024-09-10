using Unity.Mathematics;
using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Water.Spectrum
{
    [CreateAssetMenu(fileName = "New JONSWAP Spectrum settings", menuName = "Water/Spectrums/JONSWAP spectrum")]
    public class JONSWAPSpectrum : WaterFrequencySpectrum
    {
        private ComputeShader sampleSpectrumShader;
        private int KERNEL_SAMPLE;
        private ComputeBuffer spectrumBuffer;

        public JONSWAPSpectrumBand localBand = new()
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

        public JONSWAPSpectrumBand swellBand = new()
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

        protected override void OnEnable()
        {
            unsafe { spectrumBuffer = new ComputeBuffer(2, sizeof(JONSWAPSpectrumParameters)); }

            base.OnEnable();
        }

        protected override void OnDisable()
        {
            base.OnDisable();

            if (spectrumBuffer != null)
                spectrumBuffer.Release();
        }

        private void UploadSpectrumBuffer()
        {
            var gpuReadySpectrums = new JONSWAPSpectrumParameters[2] { localBand.GPUParameters(g), swellBand.GPUParameters(g) };
            spectrumBuffer.SetData(gpuReadySpectrums);
            sampleSpectrumShader.SetBuffer(KERNEL_SAMPLE, SPECTRUMS_PROP, spectrumBuffer);
        }

        public override void SampleSpectrum(SpectrumCascade cascade)
        {
            sampleSpectrumShader = settings.JONSWAPSamplerShader;
            KERNEL_SAMPLE = sampleSpectrumShader.FindKernel("SampleSpectrum"); // Here so it gets updated when the shader changes

            if (sampleSpectrumShader == null) return;

            var size = cascade.spectrumSamplesTexture.width;

            UploadSpectrumBuffer();

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

            var inst = CreateInstance<JONSWAPSpectrum>();

            if (end is JONSWAPSpectrum endPhil)
            {
                inst.depth = math.lerp(this.depth, end.depth, time);
                inst.g = math.lerp(this.g, end.g, time);
                inst.lambda = math.lerp(this.lambda, end.lambda, time);

                inst.localBand = JONSWAPSpectrumBand.Lerp(this.localBand, endPhil.localBand, time);
                inst.swellBand = JONSWAPSpectrumBand.Lerp(this.swellBand, endPhil.swellBand, time);
            }
            else
            {
                inst.depth = math.lerp(this.depth, end.depth, time);
                inst.g = math.lerp(this.g, end.g, time);
                inst.lambda = math.lerp(this.lambda, end.lambda, time);

                inst.localBand = localBand;
                inst.swellBand = swellBand;
            }

            return inst;
        }

        public override string CSVFormat()
        {
            return $"{base.CSVFormat()},{CSVFormatBand(nameof(localBand), localBand)},{CSVFormatBand(nameof(swellBand), swellBand)}";
        }

        private string CSVFormatBand(string bandName, JONSWAPSpectrumBand band)
        {
            return $"{bandName}{nameof(band.scale)},{bandName}{nameof(band.windSpeed)}," +
            $"{bandName}{nameof(band.windDirection)},{bandName}{nameof(band.fetch)}," +
            $"{bandName}{nameof(band.spreadBlend)},{bandName}{nameof(band.swell)}," +
            $"{bandName}{nameof(band.shortWavesFade)},{bandName}{nameof(band.peakEnhancement)}";
        }

        public override string ToCSV()
        {
            return $"{base.ToCSV()},{ToCSVBand(localBand)},{ToCSVBand(swellBand)}";
        }

        private string ToCSVBand(JONSWAPSpectrumBand band)
        {
            return $"{band.scale},{band.windSpeed},{band.windDirection}," +
            $"{band.fetch},{band.spreadBlend},{band.swell}," +
            $"{band.shortWavesFade},{band.peakEnhancement}";
        }

        // Spectrum
        readonly int SPECTRUMS_PROP = Shader.PropertyToID("_Spectrums");
    }
}
