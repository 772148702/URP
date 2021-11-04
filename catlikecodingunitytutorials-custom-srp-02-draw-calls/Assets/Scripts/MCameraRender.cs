using UnityEditor.PackageManager.UI;
using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    public partial class MCameraRender
    {
        private const string bufferName = "Render Camera";
        static  ShaderTagId unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

        private CommandBuffer _buffer = new CommandBuffer
        {
            name = bufferName
        };

        private ScriptableRenderContext _context;

        private Camera _camera;

        private CullingResults _cullingResults;

        public void Render(ScriptableRenderContext context, Camera camera,bool _useDynamicBatching,bool _useGpuInstancing)
        {
            this._context = context;
            this._camera = camera;
            
            PrepareBuffer();
            PrepareForSceneWindow();
            if (!Cull())
            {
                return;
            }
            Setup();
            DrawVisibleGeometry(_useDynamicBatching,_useGpuInstancing);
            DrawUnsupportedShaders();
            DrawGizmos();
            Submit();
        }


        bool Cull()
        {
            if (_camera.TryGetCullingParameters(out ScriptableCullingParameters scriptableCullingParameters))
            {
                _cullingResults = _context.Cull(ref scriptableCullingParameters);
                return true;
            }
            return true;
        }

        void Setup()
        {
            _context.SetupCameraProperties(_camera);
            CameraClearFlags flags = _camera.clearFlags;
            _buffer.ClearRenderTarget(
                flags<=CameraClearFlags.Depth,
                flags==CameraClearFlags.Color,
                flags==CameraClearFlags.Color? _camera.backgroundColor.linear:Color.clear
                );
            _buffer.BeginSample(SampleName);
            ExecuteBuffer();
        }

        void ExecuteBuffer()
        {
            _context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        void Submit()
        {
            _buffer.EndSample(SampleName);
            ExecuteBuffer();
            _context.Submit();
        }

        
        void DrawVisibleGeometry(bool _useDynamicBatching,bool _useGpuInstancing)
        {
            var sortingSettings = new SortingSettings(_camera)
            {
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSetting = new DrawingSettings(unlitShaderTagId, sortingSettings)
            {
                enableDynamicBatching = _useDynamicBatching,
                enableInstancing =  _useGpuInstancing
            };
            var filterSetting = new FilteringSettings(RenderQueueRange.opaque);
            _context.DrawRenderers(_cullingResults,ref drawingSetting,ref filterSetting);
            
            _context.DrawSkybox(_camera);

            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSetting.sortingSettings = sortingSettings;
            filterSetting.renderQueueRange = RenderQueueRange.transparent;
            _context.DrawRenderers(_cullingResults,ref drawingSetting,ref filterSetting);
        }
        
    }
}