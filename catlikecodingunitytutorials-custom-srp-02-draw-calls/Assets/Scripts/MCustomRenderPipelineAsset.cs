
using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    [CreateAssetMenu(menuName= "Rendering/M CustomRender Pipeline Asset")]
    public class MCustomRenderPipelineAsset:RenderPipelineAsset
    {
        [SerializeField] private bool useDynamicBatching = true, useGPUInstancing = true, useSRPBatcher = true;
        
        protected override RenderPipeline CreatePipeline() 
        {
            return new MCustomRenderPipeline(useDynamicBatching,useGPUInstancing,useSRPBatcher);
        }
    }
}