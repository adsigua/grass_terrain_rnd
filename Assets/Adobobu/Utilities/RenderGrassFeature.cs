using System.Collections.Generic;
using Adobobu.Grass;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Adobobu.Utilities
{
    public class RenderGrassFeature : ScriptableRendererFeature
    {
        class RenderGrassPass : ScriptableRenderPass
        {
            private RenderTexture _targetDepthTexture;
        
            public RenderGrassPass()
            {
                renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            
            private class VisibilityPassData
            {
                internal List<GrassRendererData> rendererData;
            }
            
            private class GrassRendererData
            {
                internal ComputeShader computeShader;
                internal Vector3Int threadSizes;
                internal int kernelIndex;
            }
            
            private static void ExecuteVisibilityComputePass(ComputeCommandBuffer cmd, VisibilityPassData passData)
            {
                foreach (var grassData in passData.rendererData)
                {
                    cmd.DispatchCompute(grassData.computeShader, grassData.kernelIndex, grassData.threadSizes.x, grassData.threadSizes.y, grassData.threadSizes.z);
                }
            }

            // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
            // FrameData is a context container through which URP resources can be accessed and managed.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();
                
                // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
                using (var builder = renderGraph.AddComputePass<VisibilityPassData>("VisibilityPassData", out var passData))
                {
                    // passData.depthTexture = resourceData.cameraDepthTexture;
                    // if (!passData.depthTexture.IsValid())
                    // {
                    //     Debug.LogError("Camera depth texture is not valid!");
                    //     return;
                    // }
                    //
                    // builder.UseTexture(passData.depthTexture, AccessFlags.Read);
                    passData.rendererData = new List<GrassRendererData>();
                    var grassRendererList = GrassManager.instance.grassRenderers;
                    foreach (var grassRenderer in grassRendererList)
                    {
                        var renderData = new GrassRendererData();
                        renderData.computeShader = grassRenderer.grassVisibilityComputeShader;
                        renderData.kernelIndex = 0;
                        renderData.threadSizes = grassRenderer.visibilityThreadSizes;
                        passData.rendererData.Add(renderData);
                    }

                    // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                    builder.SetRenderFunc((VisibilityPassData data, ComputeGraphContext context) =>
                    {
                        ExecuteVisibilityComputePass(context.cmd, data);
                    });
                }

                // using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                // {
                //     //var cameraData = frameData.Get<UniversalResourceData>();
                //     passData.depthTexture = resourceData.cameraDepthTexture;
                //     if (!passData.depthTexture.IsValid())
                //     {
                //         Debug.LogError("Camera depth texture is not valid!");
                //         return;
                //     }
                //
                //     builder.UseTexture(passData.depthTexture, AccessFlags.Read);
                //
                //     RTHandle rtHandle = RTHandles.Alloc(_targetDepthTexture);
                //     TextureHandle targetTexture = renderGraph.ImportTexture(rtHandle);
                //
                //     builder.SetRenderAttachment(targetTexture, 0, AccessFlags.Write);
                //     //Shader.SetGlobalTexture("_CameraDepth", destination);
                //
                //     // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                //     builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecuteCopyDepthPass(context.cmd, data.depthTexture));
                // }
            }
       
        }

        private RenderGrassPass m_renderGrassPass;

        //[SerializeField] private RenderTexture _depthTexture;
    
        /// <inheritdoc/>
        public override void Create()
        {
            m_renderGrassPass = new RenderGrassPass ();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!GrassManager.instance)
                return;
            renderer.EnqueuePass(m_renderGrassPass);
        }
    }
}
