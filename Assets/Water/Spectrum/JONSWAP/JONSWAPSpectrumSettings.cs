using UnityEditor.Recorder.Input;
using UnityEngine;

namespace Water.Spectrum
{
    [CreateAssetMenu(fileName = "New JONSWAP Spectrum settings", menuName = "Water/Spectrums/JONSWAP spectrum")]
    public class JONSWAPSpectrumSettings : WaterFrequencySpectrum
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

        public override void OnEnable() {
            unsafe { spectrumBuffer = new ComputeBuffer(2, sizeof(JONSWAPSpectrumParameters)); }

            base.OnEnable();
        }

        public override void OnDisable()
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

        // Spectrum
        readonly int SPECTRUMS_PROP = Shader.PropertyToID("_Spectrums");
    }
}
