using System;
using UnityEngine;

namespace Water
{
    public class FastFourierTransform : IDisposable
    {

        readonly int size;
        readonly ComputeShader fftShader;
        readonly RenderTexture butterflyTexture;

        public FastFourierTransform(int size, ComputeShader fftShader)
        {
            this.size = size;
            this.fftShader = fftShader;

            KERNEL_PRECOMPUTE = fftShader.FindKernel("PrecomputeTwiddleFactorsAndInputIndices");
            KERNEL_HORIZONTAL_STEP_FFT = fftShader.FindKernel("HorizontalStepFFT");
            KERNEL_VERTICAL_STEP_FFT = fftShader.FindKernel("VerticalStepFFT");
            KERNEL_HORIZONTAL_STEP_IFFT = fftShader.FindKernel("HorizontalStepInverseFFT");
            KERNEL_VERTICAL_STEP_IFFT = fftShader.FindKernel("VerticalStepInverseFFT");
            KERNEL_SCALE = fftShader.FindKernel("Scale");
            KERNEL_PERMUTE = fftShader.FindKernel("Permute");

            butterflyTexture = PrecomputeTwiddleFactorsAndInputIndices();
        }

        public void FFT2D(RenderTexture input, RenderTexture buffer, bool outputToInput = false)
        {
            int logSize = (int)Mathf.Log(size, 2);
            bool pingPong = false;

            fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_FFT, PROP_BUTTERFLY_TEX, butterflyTexture);
            fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_FFT, PROP_BUFFER0_TEX, input);
            fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_FFT, PROP_BUFFER1_TEX, buffer);
            for (int i = 0; i < logSize; i++)
            {
                pingPong = !pingPong;
                fftShader.SetInt(PROP_STEP, i);
                fftShader.SetBool(PROP_PINGPONG, pingPong);
                fftShader.Dispatch(KERNEL_HORIZONTAL_STEP_FFT, size / 8, size / 8, 1);
            }

            fftShader.SetTexture(KERNEL_VERTICAL_STEP_FFT, PROP_BUTTERFLY_TEX, butterflyTexture);
            fftShader.SetTexture(KERNEL_VERTICAL_STEP_FFT, PROP_BUFFER0_TEX, input);
            fftShader.SetTexture(KERNEL_VERTICAL_STEP_FFT, PROP_BUFFER1_TEX, buffer);
            for (int i = 0; i < logSize; i++)
            {
                pingPong = !pingPong;
                fftShader.SetInt(PROP_STEP, i);
                fftShader.SetBool(PROP_PINGPONG, pingPong);
                fftShader.Dispatch(KERNEL_VERTICAL_STEP_FFT, size / 8, size / 8, 1);
            }

            if (pingPong && outputToInput)
            {
                Graphics.Blit(buffer, input);
            }

            if (!pingPong && !outputToInput)
            {
                Graphics.Blit(input, buffer);
            }
        }

        public void IFFT2D(RenderTexture input, RenderTexture buffer, bool outputToInput = false, bool scale = true, bool permute = false)
        {
            int logSize = (int)Mathf.Log(size, 2);
            bool pingPong = false;

            fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_IFFT, PROP_BUTTERFLY_TEX, butterflyTexture);
            fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_IFFT, PROP_BUFFER0_TEX, input);
            fftShader.SetTexture(KERNEL_HORIZONTAL_STEP_IFFT, PROP_BUFFER1_TEX, buffer);
            for (int i = 0; i < logSize; i++)
            {
                pingPong = !pingPong;
                fftShader.SetInt(PROP_STEP, i);
                fftShader.SetBool(PROP_PINGPONG, pingPong);
                fftShader.Dispatch(KERNEL_HORIZONTAL_STEP_IFFT, size / 8, size / 8, 1);
            }

            fftShader.SetTexture(KERNEL_VERTICAL_STEP_IFFT, PROP_BUTTERFLY_TEX, butterflyTexture);
            fftShader.SetTexture(KERNEL_VERTICAL_STEP_IFFT, PROP_BUFFER0_TEX, input);
            fftShader.SetTexture(KERNEL_VERTICAL_STEP_IFFT, PROP_BUFFER1_TEX, buffer);
            for (int i = 0; i < logSize; i++)
            {
                pingPong = !pingPong;
                fftShader.SetInt(PROP_STEP, i);
                fftShader.SetBool(PROP_PINGPONG, pingPong);
                fftShader.Dispatch(KERNEL_VERTICAL_STEP_IFFT, size / 8, size / 8, 1);
            }

            if (pingPong && outputToInput)
            {
                Graphics.Blit(buffer, input);
            }

            if (!pingPong && !outputToInput)
            {
                Graphics.Blit(input, buffer);
            }

            if (permute)
            {
                fftShader.SetInt(PROP_SIZE, size);
                fftShader.SetTexture(KERNEL_PERMUTE, PROP_BUFFER0_TEX, outputToInput ? input : buffer);
                fftShader.Dispatch(KERNEL_PERMUTE, size / 8, size / 8, 1);
            }

            if (scale)
            {
                fftShader.SetInt(PROP_SIZE, size);
                fftShader.SetTexture(KERNEL_SCALE, PROP_BUFFER0_TEX, outputToInput ? input : buffer);
                fftShader.Dispatch(KERNEL_SCALE, size / 8, size / 8, 1);
            }
        }

        RenderTexture PrecomputeTwiddleFactorsAndInputIndices()
        {
            int logSize = (int)Mathf.Log(size, 2);
            RenderTexture rt = new RenderTexture(logSize, size, 0,
                RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            rt.filterMode = FilterMode.Point;
            rt.wrapMode = TextureWrapMode.Repeat;
            rt.enableRandomWrite = true;
            rt.Create();

            fftShader.SetInt(PROP_SIZE, size);
            fftShader.SetTexture(KERNEL_PRECOMPUTE, PROP_BUTTERFLY_TEX, rt);
            fftShader.Dispatch(KERNEL_PRECOMPUTE, logSize, size / 2 / 8, 1);
            return rt;
        }

        public void Dispose()
        {
            butterflyTexture.Release();
        }

        // Kernel IDs:
        readonly int KERNEL_PRECOMPUTE;
        readonly int KERNEL_HORIZONTAL_STEP_FFT;
        readonly int KERNEL_VERTICAL_STEP_FFT;
        readonly int KERNEL_HORIZONTAL_STEP_IFFT;
        readonly int KERNEL_VERTICAL_STEP_IFFT;
        readonly int KERNEL_SCALE;
        readonly int KERNEL_PERMUTE;

        // Property IDs:
        readonly int PROP_BUTTERFLY_TEX = Shader.PropertyToID("_ButterflyTexture");
        readonly int PROP_BUFFER0_TEX = Shader.PropertyToID("_Buffer0");
        readonly int PROP_BUFFER1_TEX = Shader.PropertyToID("_Buffer1");
        readonly int PROP_SIZE = Shader.PropertyToID("_Size");
        readonly int PROP_STEP = Shader.PropertyToID("_Step");
        readonly int PROP_PINGPONG = Shader.PropertyToID("_PingPong");
    }
}
