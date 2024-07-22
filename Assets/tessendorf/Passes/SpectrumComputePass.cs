using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

class SpectrumComputePass : ScriptableRenderPass
{

    public SpectrumComputePass(RTHandle spectr)
    {
    }

    class PassData
    {
    }

    public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
    {
        using (var builder = renderGraph.AddComputePass("ComputePass", out PassData passData))
        {
        }
    }

    static void ExecutePass(PassData data, ComputeGraphContext cgContext)
    {
    }
}
