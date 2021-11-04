using System;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    public partial class MCameraRender
    {
        private const string bufferName = "Render Camera";
        static  ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit"),
                            litShaderTagId = new ShaderTagId("CustomLit");

        private CommandBuffer _buffer = new CommandBuffer
        {
            name = bufferName
        };

        private ScriptableRenderContext context;

        private Camera camera;

        private CullingResults _cullingResults;

        Lighting _lighting = new Lighting();
        public void Render(ScriptableRenderContext context, Camera camera,bool useDynamicBatching,bool useGpuInstancing,ShadowSettings shadowSettings)
        {
            this.context = context;
            this.camera = camera;
            
            PrepareBuffer();
            PrepareForSceneWindow();
            if (!Cull(shadowSettings.maxDistance))
            {
                return;
            }
            //we can render shadow in this
            _buffer.BeginSample(SampleName);
            ExecuteBuffer();
            _lighting.Setup(context,_cullingResults,shadowSettings);
            _buffer.EndSample(SampleName);
            
            Setup();
            DrawVisibleGeometry(useDynamicBatching,useGpuInstancing);
            DrawUnsupportedShaders();
            DrawGizmos();
            //Do we really need to release temporary RT in every frame
            _lighting.CleanUp();
            Submit();
        }


        bool Cull(float maxShadowDistance)
        {
            if (camera.TryGetCullingParameters(out ScriptableCullingParameters scriptableCullingParameters))
            {
                scriptableCullingParameters.shadowDistance = Mathf.Min(maxShadowDistance, camera.farClipPlane);
                _cullingResults = context.Cull(ref scriptableCullingParameters);
                return true;
            }
            return true;
        }

        void Setup()
        {
            context.SetupCameraProperties(camera);
            CameraClearFlags flags = camera.clearFlags;
            _buffer.ClearRenderTarget(
                flags<=CameraClearFlags.Depth,
                flags==CameraClearFlags.Color,
                flags==CameraClearFlags.Color? camera.backgroundColor.linear:Color.clear
                );
            _buffer.BeginSample(SampleName);
            ExecuteBuffer();
        }

        void ExecuteBuffer()
        {
            context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        void Submit()
        {
            _buffer.EndSample(SampleName);
            ExecuteBuffer();
            context.Submit();
        }

        
        void DrawVisibleGeometry(bool _useDynamicBatching,bool _useGpuInstancing)
        {
            var sortingSettings = new SortingSettings(camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSetting = new DrawingSettings(unlitShaderTagId, sortingSettings)
            {
                enableDynamicBatching = _useDynamicBatching,
                enableInstancing =  _useGpuInstancing
            };
            drawingSetting.SetShaderPassName(1,litShaderTagId);
            
            var filterSetting = new FilteringSettings(RenderQueueRange.opaque);
            context.DrawRenderers(_cullingResults,ref drawingSetting,ref filterSetting);
            
            context.DrawSkybox(camera);

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSetting.sortingSettings = sortingSettings;
            filterSetting.renderQueueRange = RenderQueueRange.transparent;
            context.DrawRenderers(_cullingResults,ref drawingSetting,ref filterSetting);
        }
        
    }
}