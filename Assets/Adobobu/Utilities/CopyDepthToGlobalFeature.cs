using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Adobobu.Utilities
{
    public class CopyDepthToGlobalFeature : ScriptableRendererFeature
    {
        class CustomRenderPass : ScriptableRenderPass
        {
            private RenderTexture _targetDepthTexture;
        
            public CustomRenderPass(RenderTexture renderTexture)
            {
                _targetDepthTexture = renderTexture;
                ConfigureInput(ScriptableRenderPassInput.Depth);
            }
            // This class stores the data needed by the RenderGraph pass.
            // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
            private class PassData
            {
                internal TextureHandle depthTexture;
            }

            private static void ExecuteCopyDepthPass(RasterCommandBuffer cmd, RTHandle sourceTexture)
            {
                Blitter.BlitTexture(cmd, sourceTexture, new Vector4(1, 1, 0, 0), 0.0f, false);
            }

            // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
            // FrameData is a context container through which URP resources can be accessed and managed.
            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                const string passName = "CopyDepthToGlobal";
            
                var resourceData = frameData.Get<UniversalResourceData>();
                //
                // var destinationDesc = renderGraph.GetTextureDesc(resourceData.cameraColor);
                // destinationDesc.clearBuffer = false;
                // destinationDesc.name = $"_CopyDepthToGlobal";
                // TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
                using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
                {
                    //var cameraData = frameData.Get<UniversalResourceData>();
                    passData.depthTexture = resourceData.cameraDepthTexture;
                    if (!passData.depthTexture.IsValid())
                    {
                        Debug.LogError("Camera depth texture is not valid!");
                        return;
                    }
                
                    builder.UseTexture(passData.depthTexture, AccessFlags.Read);
                
                    RTHandle rtHandle = RTHandles.Alloc(_targetDepthTexture);
                    TextureHandle targetTexture = renderGraph.ImportTexture(rtHandle);
                
                    builder.SetRenderAttachment(targetTexture, 0, AccessFlags.Write);
                    //Shader.SetGlobalTexture("_CameraDepth", destination);

                    // Assigns the ExecutePass function to the render pass delegate. This will be called by the render graph when executing the pass.
                    builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecuteCopyDepthPass(context.cmd, data.depthTexture));
                
                }
            }
       
        }

        CustomRenderPass m_ScriptablePass;

        [SerializeField] private RenderTexture _depthTexture;
    
        /// <inheritdoc/>
        public override void Create()
        {
            if (!_depthTexture)
            {
                _depthTexture = new RenderTexture(1024, 1024, 0, RenderTextureFormat.RFloat);            
            }
            m_ScriptablePass = new CustomRenderPass(_depthTexture);

            // Configures where the render pass should be injected.
            m_ScriptablePass.renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (!_depthTexture)
                return;
            Shader.SetGlobalTexture("_CopyDepthTexture", _depthTexture);
            renderer.EnqueuePass(m_ScriptablePass);
        }
    }
}
