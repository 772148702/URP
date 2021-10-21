using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    public class MCustomRenderPipeline: RenderPipeline
    {
        MCameraRender _cameraRenderer = new MCameraRender();
        
        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            foreach (var camera in cameras)
            {
                _cameraRenderer.Render(context,camera);
            }
        }
        
        
    }
}