
using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    [CreateAssetMenu(menuName= "Rendering/M CustomRender Pipeline Asset")]
    public class MCustomRenderPipelineAsset:RenderPipelineAsset
    {
        protected override RenderPipeline CreatePipeline() 
        {
            return new MCustomRenderPipeline();
        }
    }
}