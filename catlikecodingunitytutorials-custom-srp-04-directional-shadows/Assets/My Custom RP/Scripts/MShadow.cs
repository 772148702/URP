using System.Diagnostics.SymbolStore;
using UnityEngine;
using UnityEngine.Rendering;

namespace MRender
{
    public class Shadow
    {
        private const string BufferName = "Shadow";
        private const int MaxShadowedDirLightCount = 4, MaxCascades = 4;

        private static string[] _directionalFilterKeywords =
        {
            "_DIRECTIONAL_PCF3",
            "_DIRECTIONAL_PCF5",
            "_DIRECTIONAL_PCF7"
        };

        private static string [] _cascadeBlendKeywords =
        {
            "_CASCADE_BLEND_SOFT",
            "_CASCADE_BLEND_HARD"
        };

        static int
            _dirShadowAtlasId = Shader.PropertyToID("_DirectionalShadowAtlas"),
            dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowMatrices"),
            cascadeCountId = Shader.PropertyToID("_CascadeCount"),
            CascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres"),
            CascadeDataId = Shader.PropertyToID("_CascadeData"),
            shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize"),
            ShadowDistanceFadeId = Shader.PropertyToID("_ShadowDistanceFade");

        private static Vector4[]
            cascadeCullingSphere = new Vector4[MaxCascades],
            cascadeData = new Vector4[MaxCascades];
        
        static Matrix4x4[] 
            dirShadowMatrices = new Matrix4x4[MaxCascades*MaxShadowedDirLightCount];

        struct ShadowDirectionalLight
        {
            public int VisibleLightIndex;
            public float SlopeScaleBias;
            public float NearPlaneOffset;
        }
        
        ShadowDirectionalLight[] _shadowDirectionalLights = new ShadowDirectionalLight[MaxShadowedDirLightCount];

        int _shadowedDirLightCount;

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
            _shadowedDirLightCount = 0;
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
            if (_shadowedDirLightCount > 0)
            {
                RenderDirectionalShadows();
            }
            else
            {
                _commandBuffer.GetTemporaryRT(_dirShadowAtlasId,1,1,32,
                    FilterMode.Bilinear,RenderTextureFormat.Shadowmap);
            }
        }

        void RenderDirectionalShadows()
        {
            int atlasSize = (int) settings.directional.atlasSize;
            _commandBuffer.GetTemporaryRT(_dirShadowAtlasId,atlasSize,atlasSize,32,FilterMode.Bilinear,RenderTextureFormat.Depth);
            _commandBuffer.SetRenderTarget(_dirShadowAtlasId,RenderBufferLoadAction.DontCare,RenderBufferStoreAction.Store);
            _commandBuffer.ClearRenderTarget(true,false,Color.clear);
            _commandBuffer.BeginSample(BufferName);
            ExecuteBuffer();
            int tiles = _shadowedDirLightCount * settings.directional.cascadeCount;
            int split = tiles <= 1 ? 1 : tiles <= 4 ? 2 : 4;
            int tileSize = atlasSize / split;

            for (int i = 0; i < _shadowedDirLightCount; i++)
            {
                RenderDirectionalShadows(i,split,tileSize);
            }
            _commandBuffer.SetGlobalInt(cascadeCountId,settings.directional.cascadeCount);
            _commandBuffer.SetGlobalVectorArray(
                CascadeCullingSpheresId,cascadeCullingSphere);
            
            _commandBuffer.SetGlobalVectorArray(CascadeDataId,cascadeData);
            _commandBuffer.SetGlobalMatrixArray(dirShadowMatricesId,dirShadowMatrices);
            float f = 1f - settings.directional.cascadeFade;
            _commandBuffer.SetGlobalVector(
                ShadowDistanceFadeId,new Vector4(1f/settings.maxDistance,1f/settings.distanceFade,1f/(1f-f*f)));
            SetKeywords(_directionalFilterKeywords,(int) settings.directional.filterMode-1);
            SetKeywords(_cascadeBlendKeywords,(int)settings.directional.cascadeBlend-1);
            _commandBuffer.SetGlobalVector(shadowAtlasSizeId,new Vector4(atlasSize,1f/atlasSize));
            _commandBuffer.EndSample(BufferName);
            ExecuteBuffer();
        }

        void SetKeywords(string[] keywords, int enabledIndex)
        {
            for (int i = 0; i < keywords.Length; i++)
            {
                if (i == enabledIndex)
                {
                    _commandBuffer.EnableShaderKeyword(keywords[i]);
                }
                else
                {
                    _commandBuffer.DisableShaderKeyword(keywords[i]);
                }
                
            }
        }
        

        void RenderDirectionalShadows(int index, int split, int tileSize)
        {
            ShadowDirectionalLight light = _shadowDirectionalLights[index];
            var shadowSettings = new ShadowDrawingSettings(cullingResults,light.VisibleLightIndex);
            int cascadeCount = settings.directional.cascadeCount;
            int tileOffset = index * cascadeCount;
            var ratios = settings.directional.CascadeRatios;
            float cullingFactor  = Mathf.Max(0f, 0.8f - settings.directional.cascadeFade);

            for (int i = 0; i < cascadeCount; i++)
            {
                cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.VisibleLightIndex, i,
                    cascadeCount, ratios, tileSize, light.NearPlaneOffset, out Matrix4x4 viewMatrix,
                    out Matrix4x4 projMatrix, out ShadowSplitData shadowSplitData);
                shadowSplitData.shadowCascadeBlendCullingFactor = cullingFactor;
                shadowSettings.splitData = shadowSplitData;
                if (index == 0)
                {
                    SetCascadeData(i,shadowSplitData.cullingSphere,tileSize);
                }

                int tileIndex = tileOffset + i;
                dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, SetTileViewport(tileIndex, split, tileSize), split);
                _commandBuffer.SetViewProjectionMatrices(viewMatrix,projMatrix);
                _commandBuffer.SetGlobalDepthBias(0f,light.SlopeScaleBias);
                ExecuteBuffer();
                context.DrawShadows(ref shadowSettings);
                _commandBuffer.SetGlobalDepthBias(0f,0f);
            }

        }

        //according to light settings, get the shadow parameter (i.g.?)
        public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex)
        {
            if (_shadowedDirLightCount < MaxShadowedDirLightCount && light.shadows != LightShadows.None &&
                light.shadowStrength > 0f && cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds b))
            {
                _shadowDirectionalLights[_shadowedDirLightCount] = new ShadowDirectionalLight
                {    
                    VisibleLightIndex = visibleLightIndex,
                    SlopeScaleBias = light.shadowBias,
                    NearPlaneOffset = light.shadowNearPlane
                };
                //pay attention z must be light.shadowNormalBias, it should not be light.shadowBias
                return new Vector3(light.shadowStrength,settings.directional.cascadeCount*_shadowedDirLightCount++,light.shadowNormalBias);
            }

            return Vector3.zero;
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