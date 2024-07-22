using System;
using System.Collections.Generic;
using System.Drawing;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

class FFTComputePass : ScriptableRenderPass, IDisposable
{
    const int LOCAL_WORK_GROUPS_X = 8;
    const int LOCAL_WORK_GROUPS_Y = 8;

    private readonly bool _inverse;
    private readonly uint _size;
    private readonly ComputeShader _cs;
    private RTHandle _inputBuffer;


    public FFTComputePass(uint size, RenderTexture input, TextureHandle output, ComputeShader cs, bool inverse = false)
    {
        var logSize = Mathf.Log(size, 2);
        if ((uint)logSize != logSize)
        {
            throw new ArgumentException("Size must be a power of 2");
        }

        _size = size;
        _inverse = inverse;
        _cs = cs;
        _inputBuffer = RTHandles.Alloc(input);
    }

    public void Dispose()
    {
        _inputBuffer.Release();
    }

    private class PassData
    {
        internal uint size;
        internal bool inverse;
        internal ComputeShader cs;
        internal TextureHandle precomputed;
        internal TextureHandle input;
        internal TextureHandle support;
        internal TextureHandle output;
        internal bool scale;
        internal bool permute;
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {

        // TODO: PrecomputeTwiddleFactorsAndInputIndices (WHERE ????)
        // var precomputedHandle = renderGraph.ImportTexture();
        var inputHandle = renderGraph.ImportTexture(_inputBuffer);

        var textureProperties = new RenderTextureDescriptor((int)_size, (int)_size, RenderTextureFormat.Default, 0);
        var outputHandle = UniversalRenderer.CreateRenderGraphTexture(renderGraph, textureProperties, "FFT Output Buffer", false);

        using (var builder = renderGraph.AddComputePass("ComputePass", out PassData passData))
        {
            passData.size = _size;
            passData.cs = _cs;
            passData.inverse = _inverse;
            // passData.precomputed = precomputedHandle;
            passData.input = inputHandle;
            passData.support = builder.CreateTransientTexture(new TextureDesc(textureProperties));
            passData.output = outputHandle;
            passData.scale = true; // TODO: make parameter
            passData.permute = true; // TODO: make parameter

            builder.UseTexture(passData.precomputed, AccessFlags.Read);
            builder.UseTexture(passData.input, AccessFlags.ReadWrite);
            builder.UseTexture(passData.support, AccessFlags.ReadWrite);
            builder.UseTexture(passData.output, AccessFlags.Write);

            builder.SetRenderFunc<PassData>(ExecutePass);
        }
    }

    static void ExecutePass(PassData data, ComputeGraphContext cgContext)
    {
        int logSize = (int)Mathf.Log(data.size, 2);
        bool pingPong = false;

        var kHStep = data.cs.FindKernel(data.inverse ? "HorizontalStepInverseFFT" : "HorizontalStepFFT");
        var kVStep = data.cs.FindKernel(data.inverse ? "VerticalStepInverseFFT" : "VerticalStepFFT");

        cgContext.cmd.SetComputeTextureParam(data.cs, kHStep, "PrecomputedData", data.precomputed);
        cgContext.cmd.SetComputeTextureParam(data.cs, kHStep, "Buffer0", data.input);
        cgContext.cmd.SetComputeTextureParam(data.cs, kHStep, "Buffer1", data.support);

        for (int i = 0; i < logSize; i++)
        {
            pingPong = !pingPong;
            cgContext.cmd.SetComputeIntParam(data.cs, "Step", i);
            cgContext.cmd.SetComputeIntParam(data.cs, "PingPong", pingPong ? 1 : 0);
            cgContext.cmd.DispatchCompute(data.cs, kHStep, (int)data.size / LOCAL_WORK_GROUPS_X, (int)data.size / LOCAL_WORK_GROUPS_Y, 1);
        }


        cgContext.cmd.SetComputeTextureParam(data.cs, kVStep, "PrecomputedData", data.precomputed);
        cgContext.cmd.SetComputeTextureParam(data.cs, kVStep, "Buffer0", data.input);
        cgContext.cmd.SetComputeTextureParam(data.cs, kVStep, "Buffer1", data.support);
        for (int i = 0; i < logSize; i++)
        {
            pingPong = !pingPong;
            cgContext.cmd.SetComputeIntParam(data.cs, "Step", i);
            cgContext.cmd.SetComputeIntParam(data.cs, "PingPong", pingPong ? 1 : 0);
            cgContext.cmd.DispatchCompute(data.cs, kVStep, (int)data.size / LOCAL_WORK_GROUPS_X, (int)data.size / LOCAL_WORK_GROUPS_Y, 1);
        }

        // TODO: find way to blit output
        if (pingPong)
        {
            Graphics.Blit(data.support, data.output);
            // Blitter.BlitTexture(cgContext.cmd, data.support, data.output);
        } else {
            Graphics.Blit(data.input, data.output);
            // Blitter.BlitTexture(cgContext.cmd, data.input, data.output);
        }

        if (data.permute)
        {
            var kPermute = data.cs.FindKernel("Permute");
            cgContext.cmd.SetComputeIntParam(data.cs, "Size", (int)data.size);
            cgContext.cmd.SetComputeTextureParam(data.cs, kPermute, "Buffer0", data.output);
            cgContext.cmd.DispatchCompute(data.cs, kPermute, (int)data.size / LOCAL_WORK_GROUPS_X, (int)data.size / LOCAL_WORK_GROUPS_Y, 1);
        }

        if (data.scale)
        {
            var kScale = data.cs.FindKernel("Scale");
            cgContext.cmd.SetComputeIntParam(data.cs, "Size", (int)data.size);
            cgContext.cmd.SetComputeTextureParam(data.cs, kScale, "Buffer0", data.output);
            cgContext.cmd.DispatchCompute(data.cs, kScale, (int)data.size / LOCAL_WORK_GROUPS_X, (int)data.size / LOCAL_WORK_GROUPS_Y, 1);
        }
    }
}
