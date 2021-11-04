using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    public class MCustomRenderPipeline: RenderPipeline
    {
        MCameraRender _cameraRenderer = new MCameraRender();
        private bool useDynamicBatching, useGpuInstancing;
        private ShadowSettings shadowSettings;
        
        public MCustomRenderPipeline(bool useDynamicBatching, bool useGpuInstancing, bool useSPRBatcher,ShadowSettings shadowSetting)
        {
            this.shadowSettings = shadowSetting;
            this.useDynamicBatching = useDynamicBatching;
            this.useGpuInstancing = useGpuInstancing;
            GraphicsSettings.useScriptableRenderPipelineBatching = useSPRBatcher;
        }
        
        
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (var camera in cameras)
            {
                _cameraRenderer.Render(context,camera,useDynamicBatching,useGpuInstancing,shadowSettings);
            }
        }


    
    }
}