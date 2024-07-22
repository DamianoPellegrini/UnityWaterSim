using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

class EvolveSpectrumComputePass : ScriptableRenderPass
{

    public EvolveSpectrumComputePass()
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
