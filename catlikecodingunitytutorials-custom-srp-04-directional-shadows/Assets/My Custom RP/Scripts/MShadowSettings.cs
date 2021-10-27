using UnityEngine;
namespace MRender
{
    [System.Serializable]
    public class ShadowSettings
    {
        public enum MapSize  {
            _256 = 256, _512 = 512, _1024 = 1024,
            _2048 = 2048, _4096 = 4096, _8192 = 8192
        }

        public enum FilterMode
        {
            PCF2X2,PCF3X3,PCF5X5,PCF7X7
        }
        
        [Min(0.001f)]
        public float minDistance = 100f;

        [Range(0.001f, 1f)] 
        public float distanceFade = 0.1f;
        
        [System.Serializable]
        public struct Directional
        {
            public MapSize atlasSize;
            public FilterMode filterMode;

            [Range(0,4)]
            public int cascadeCount;

            [Range(0, 1f)]
            public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
            
            public Vector3 CascadeRatios=> new Vector3(cascadeRatio1,cascadeRatio2,cascadeRatio3);

            [Range(0.001f, 1)] public float cascadeFade;

            public enum CascadeBlendMode
            {
                Soft,
                Hard,
                Dither
            }

            public  CascadeBlendMode cascadeBlend;
        }

        public Directional directional = new Directional
        {
            atlasSize = MapSize._1024,
            filterMode = FilterMode.PCF3X3,
            cascadeCount =  4,
            cascadeRatio1 = 0.1f,
            cascadeRatio2 = 0.25f,
            cascadeRatio3 = 0.5f,
            cascadeFade = 0.1f,
            cascadeBlend = Directional.CascadeBlendMode.Hard
        };

    }
        
}