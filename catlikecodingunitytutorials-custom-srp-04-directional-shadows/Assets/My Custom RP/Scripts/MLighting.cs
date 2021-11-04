

using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    public class Lighting
    {
        private const string BufferName = "Lighting";
        private const int MaxDirLightCount = 4;

        private static int
            _dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            _dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
            _dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
            _dirLightShadowDatasId = Shader.PropertyToID("_DirectionalLightShadowData");
        
        static Vector4[]
            DirLightColors = new Vector4[MaxDirLightCount],
            DirLightDirections = new Vector4[MaxDirLightCount],
            DirLightShadowData = new Vector4[MaxDirLightCount];

        private CommandBuffer _buffer = new CommandBuffer
        {
            name = BufferName
        };

        private CullingResults _cullingResults;
        Shadow _shadow = new Shadow();

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
        {
             this._cullingResults = cullingResults;
            _buffer.BeginSample(BufferName);
            _shadow.SetUp(context,cullingResults,shadowSettings);
            SetupLights();
            _shadow.Render();
            _buffer.EndSample(BufferName);
            context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        void SetupLights()
        {
            NativeArray<VisibleLight> visibleLights = _cullingResults.visibleLights;
            int dirLightCount = 0;
            for (int i = 0; i < visibleLights.Length; i++)
            {
                VisibleLight visibleLight = visibleLights[i];
                if (visibleLight.lightType == LightType.Directional)
                {
                    SetupDirectionalLight(dirLightCount++, ref visibleLight);
                    if (dirLightCount > MaxDirLightCount)
                    {
                        break;
                    }
                }
            }
            _buffer.SetGlobalInt(_dirLightCountId,dirLightCount);
            _buffer.SetGlobalVectorArray(_dirLightColorsId,DirLightColors);
            _buffer.SetGlobalVectorArray(_dirLightDirectionsId,DirLightDirections);
            _buffer.SetGlobalVectorArray(_dirLightShadowDatasId,DirLightShadowData);
        }

        void SetupDirectionalLight(int index, ref VisibleLight visibleLight)
        {
            DirLightColors[index] = visibleLight.finalColor;
            DirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
            DirLightShadowData[index] = _shadow.ReserveDirectionalShadows(visibleLight.light, index);
        }

        public void CleanUp()
        {
            _shadow.CleanUp();
        }
    }
}