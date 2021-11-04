using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    public class MCustomRenderPipeline: RenderPipeline
    {
        MCameraRender _cameraRenderer = new MCameraRender();
        private bool useDynamicBatching, useGpuInstancing;

        public MCustomRenderPipeline(bool _useDynamicBatching, bool _useGpuInstancing, bool _useSPRBatcher)
        {
            useDynamicBatching = _useDynamicBatching;
            useGpuInstancing = _useGpuInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = _useSPRBatcher;
        }
        
        
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (var camera in cameras)
            {
                _cameraRenderer.Render(context,camera,useDynamicBatching,useGpuInstancing);
            }
        }


    
    }
}