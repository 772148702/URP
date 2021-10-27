

using System.Diagnostics.SymbolStore;
using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    public class Shadow
    {
        private const string BufferName = "Shadow";
        private const int MaxShadowedDirLightCount = 4, MaxCascades = 4;

        private static string[] DirectionalFilterKeywords =
        {
            "Directional_PCF3",
            "Directional_PCF5",
            "Directional_PCF7"
        };

        private string [] cascadeBlendKeywords =
        {
            "_CASCADE_BLEND_SOFT",
            "_CASCADE_BLEND_HARD"
        };

        static int
            _dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
            DirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
            CascadeCountId = Shader.PropertyToID("_CascadeCount"),
            CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
            CascadeDataId = Shader.PropertyToID("_CascadeData"),
            ShadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
            ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

        private static Vector4[]
            cascadeCullingSphere = new Vector4[MaxCascades],
            cascadeData = new Vector4[MaxCascades];
        
        static Matrix4x4[] 
            dirShadowMatrice = new Matrix4x4[MaxCascades*MaxShadowedDirLightCount];

        struct ShadowDirectionalLight
        {
            public int VisibleLightIndex;
            public float SlopeScaleBias;
            public float NearPlaneOffset;
        }
        
        ShadowDirectionalLight[] _shadowDirectionalLights = new ShadowDirectionalLight[MaxShadowedDirLightCount];

        int shadowedDirLightCount;

        private CommandBuffer _commandBuffer = new CommandBuffer
        {
            name = BufferName
        };
        
        private ScriptableRenderContext context;
        private CullingResults cullingResults;
        private ShadowSettings settings;

        public void SetUp(ScriptableRenderContext context, CullingResults cullingResults, ShadowSettings shadowSettings)
        {
            this.context = context;
            this.cullingResults = cullingResults;
            this.settings = shadowSettings;
            shadowedDirLightCount = 0;
        }

        public void CleanUp()
        {
            _commandBuffer.ReleaseTemporaryRT(_dirShadowAtlasId);
            ExecuteBuffer();
        }

        public void ExecuteBuffer()
        {
            context.ExecuteCommandBuffer(_commandBuffer);
            _commandBuffer.Clear();
        }

        public void Render()
        {
            if (_shadowDirectionalLights.Length > 0)
            {
                
            }
        }

        void RenderDirectionalShadows()
        {
            int atlasSize = (int) settings.directional.atlasSize;
            
        }
        
        public void RenderDirectionalLights(int index,int split,int tileSize)
        {
            ShadowDirectionalLight light = _shadowDirectionalLights[index];
            var shadowSettings = new ShadowDrawingSettings(cullingResults, light.VisibleLightIndex);
            int cascadeCount = settings.directional.cascadeCount;
            int tileOffset = index * cascadeCount;
            Vector3 ratios = settings.directional.CascadeRatios;
            float cullingFactor = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);

            for (int i = 0; i < cascadeCount; i++)
            {
                cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.VisibleLightIndex, i,
                    cascadeCount,
                    ratios, tileSize, light.NearPlaneOffset, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix,
                    out ShadowSplitData splitData);
                splitData.shadowCascadeBlendCullingFactor = cullingFactor;
                shadowSettings.splitData = splitData;
                if (index == 0)
                {
                   // SetCase
                   SetCascadeData(index,splitData.cullingSphere,tileSize);
                }

                int tileIndex = tileOffset + i;
                dirShadowMatrice[tileIndex] = ConvertToAtlasMatrix(
                    projMatrix * viewMatrix,
                    SetTileViewport(tileIndex, split, tileSize), split
                );
                _commandBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                _commandBuffer.SetGlobalDepthBias(0f, light.SlopeScaleBias);
                ExecuteBuffer();
                context.DrawShadows(ref shadowSettings);
                _commandBuffer.SetGlobalDepthBias(0f, 0f);
            }
         
        }

        Vector2 SetTileViewport (int index, int split, float tileSize) {
            Vector2 offset = new Vector2(index % split, index / split);
            _commandBuffer.SetViewport(new Rect(
                offset.x * tileSize, offset.y * tileSize, tileSize, tileSize
            ));
            return offset;
        }
        
        //What is this
        void SetCascadeData(int index, Vector4 cullingSphere, float tileSize)
        {
            float texSize = 2f * cullingSphere.w / tileSize;
            float filterSize = texSize * ((float) settings.directional.filterMode + 1f);
            cullingSphere.w -= filterSize;
            cullingSphere.w *= cullingSphere.w;
            cascadeCullingSphere[index] = cullingSphere;
            cascadeData[index] = new Vector4(
                1f / cullingSphere.w,
                filterSize * 1.4142136f
            );
        }

        Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 m, Vector2 offset, int split)
        {
            if (SystemInfo.usesReversedZBuffer)
            {
                m.m20 = -m.m20;
                m.m21 = -m.m21;
                m.m22 = -m.m22;
                m.m23 = -m.m23;
            }
            float scale = 1f / split;
            m.m00 = (0.5f * (m.m00 + m.m30) + offset.x * m.m30) * scale;
            m.m01 = (0.5f * (m.m01 + m.m31) + offset.x * m.m31) * scale;
            m.m02 = (0.5f * (m.m02 + m.m32) + offset.x * m.m32) * scale;
            m.m03 = (0.5f * (m.m03 + m.m33) + offset.x * m.m33) * scale;
            m.m10 = (0.5f * (m.m10 + m.m30) + offset.y * m.m30) * scale;
            m.m11 = (0.5f * (m.m11 + m.m31) + offset.y * m.m31) * scale;
            m.m12 = (0.5f * (m.m12 + m.m32) + offset.y * m.m32) * scale;
            m.m13 = (0.5f * (m.m13 + m.m33) + offset.y * m.m33) * scale;
            m.m20 = 0.5f * (m.m20 + m.m30);
            m.m21 = 0.5f * (m.m21 + m.m31);
            m.m22 = 0.5f * (m.m22 + m.m32);
            m.m23 = 0.5f * (m.m23 + m.m33);
            return m;
        }
    }
}