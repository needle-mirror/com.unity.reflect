using System;
using System.Collections.Generic;
using Unity.Reflect;
using Unity.Reflect.Model;
using UnityEngine.Rendering;

namespace UnityEngine.Reflect
{
    public enum EmissionMode
    {
        None = 0,
        Color = 1,
        Map = 2
    }
    
    public static class StandardShaderHelper
    {
        struct SuffixId
        {
            public readonly int brightness;
            public readonly int invert;
            public readonly int rotation;

            public SuffixId(string name)
            {
                brightness = Shader.PropertyToID(string.Format(k_FormatBrightness, name));
                invert = Shader.PropertyToID(string.Format(k_FormatInvert, name));
                rotation = Shader.PropertyToID(string.Format(k_FormatRotation, name));
            }
        }
        
        // suffix formats
        const string k_FormatBrightness = "{0}_B";
        const string k_FormatInvert = "{0}_I";
        const string k_FormatRotation = "{0}_R";

        // shaders
        //[ObsoleteAttribute]
        static readonly string k_ShaderStandardTransparent = "UnityReflect/Standard Transparent";
        //[ObsoleteAttribute]
        static readonly string k_ShaderStandardOpaque = "UnityReflect/Standard Opaque";

        // tint
        static readonly int k_IdTint = Shader.PropertyToID("_Tint");
        
        // albedo
        static readonly int k_IdAlbedoColor = Shader.PropertyToID("_AlbedoColor");
        const string k_NameMainTex = "_MainTex";
        static readonly int k_IdMainTex = Shader.PropertyToID(k_NameMainTex);
        static readonly int k_IdMainTexFade = Shader.PropertyToID("_MainTex_Fade");
        
        // alpha
        static readonly int k_IdAlpha = Shader.PropertyToID("_Alpha");
        const string k_NameAlphaMap = "_AlphaMap";
        static readonly int k_IdAlphaMap = Shader.PropertyToID(k_NameAlphaMap);
        
        // cutout
        const string k_NameCutoutMap = "_CutoutMap";
        static readonly int k_IdCutoutMap = Shader.PropertyToID(k_NameCutoutMap);
        static readonly int k_IdCutoutThreshold = Shader.PropertyToID("_CutoutThreshold");
        static readonly int k_IdCull = Shader.PropertyToID("_Cull");
        
        // normal
        const string k_NameBumpMap = "_BumpMap";
        static readonly int k_IdBumpMap = Shader.PropertyToID(k_NameBumpMap);
        static readonly int k_IdBumpScale = Shader.PropertyToID("_BumpScale");
        
        // smoothness
        static readonly int k_IdSmoothness = Shader.PropertyToID("_Smoothness");
        const string k_NameSmoothnessMap = "_SmoothnessMap";
        static readonly int k_IdSmoothnessMap = Shader.PropertyToID(k_NameSmoothnessMap);
        
        // metallic
        static readonly int k_IdMetallic = Shader.PropertyToID("_Metallic");
        const string k_NameMetallicMap = "_MetallicMap";
        static readonly int k_IdMetallicMap = Shader.PropertyToID(k_NameMetallicMap);
        
        // emission
        const string k_NameEmissionMap = "_EmissionMap";
        static readonly int k_IdEmissionMap = Shader.PropertyToID(k_NameEmissionMap);
        static readonly int k_IdEmission = Shader.PropertyToID("_EmissionColor");
        static readonly int k_IdEmissionMode = Shader.PropertyToID("_EmissionMode");

        // use map flags
        static readonly int k_IdUseAlbedoMap = Shader.PropertyToID("_UseAlbedoMap");
        static readonly int k_IdUseNormalMap = Shader.PropertyToID("_UseNormalMap");
        static readonly int k_IdUseSmoothnessMap = Shader.PropertyToID("_UseSmoothnessMap");
        static readonly int k_IdUseMetallicMap = Shader.PropertyToID("_UseMetallicMap");
        static readonly int k_IdUseEmissionMap = Shader.PropertyToID("_UseEmissionMap");
        static readonly int k_IdUseAlphaMap = Shader.PropertyToID("_UseAlphaMap");

        // keywords
        static readonly string k_IdUseCutout = "_USECUTOUT";
        const string k_KeywordMapRotation = "_MAP_ROTATION";

        static readonly Dictionary<int, SuffixId> k_SuffixIds = new Dictionary<int, SuffixId>()
        {
            { k_IdMainTex, new SuffixId(k_NameMainTex) }, 
            { k_IdAlphaMap, new SuffixId(k_NameAlphaMap) }, 
            { k_IdCutoutMap, new SuffixId(k_NameCutoutMap) }, 
            { k_IdBumpMap, new SuffixId(k_NameBumpMap) }, 
            { k_IdSmoothnessMap, new SuffixId(k_NameSmoothnessMap) }, 
            { k_IdMetallicMap, new SuffixId(k_NameMetallicMap) }, 
            { k_IdEmissionMap, new SuffixId(k_NameEmissionMap) }
        };

        static readonly List<int> k_ActiveMapIds = new List<int>();
        
        // Used in shaders
        public static bool IsTransparent(SyncMaterial syncMaterial)
        {
            // It doesn't make a lot of sense for a material to be both emissive and transparent.
            // So in case of emissive materials, force opaque shader.
            if (syncMaterial.Emission != SyncColor.Black || syncMaterial.EmissionMap.TextureId != SyncId.None)
                return false;
            
            return syncMaterial.Alpha < 1.0f || syncMaterial.AlphaMap.TextureId != SyncId.None;
        }
        
        //[ObsoleteAttribute]
        public static Shader GetShader(SyncMaterial syncMaterial)
        {
            return GetShader(IsTransparent(syncMaterial));
        }

        //[ObsoleteAttribute]
        public static Shader GetShader(bool isTransparent)
        {
            var shaderName = isTransparent ? k_ShaderStandardTransparent : k_ShaderStandardOpaque;
            return Shader.Find(shaderName);
        }
        
        static bool IsUsingRenderPipeline()
        {
            return GraphicsSettings.renderPipelineAsset != null;
        }

        public static void ComputeMaterial(SyncedData<SyncMaterial> syncedMaterial, Material material, ITextureCache textureCache)
        {
            var syncMaterial = syncedMaterial.data;
            var sourceId = syncedMaterial.key.source;
            
            // Tint
            material.AssignColor(k_IdTint, syncMaterial.Tint, false, true);
                
            // Albedo
            material.AssignColor(k_IdAlbedoColor, syncMaterial.AlbedoColor, true, true);
            material.AssignMap(sourceId, k_IdMainTex, syncMaterial.AlbedoMap, textureCache);
            material.SetFloat(k_IdMainTexFade, syncMaterial.AlbedoFade);

            var isTransparent = IsTransparent(syncMaterial);
            
            // Alpha
            if (isTransparent)
            {
                // Alpha is globally not transparent enough.
                // Arbitrary adjustment of the alpha based on empirical visual experimentation.
                var alpha = Mathf.Clamp01(syncMaterial.Alpha * 0.3f);
                material.SetFloat(k_IdAlpha, alpha);
                material.AssignMap(sourceId, k_IdAlphaMap, syncMaterial.AlphaMap, textureCache);
            }

            if (syncMaterial.CutoutMap.TextureId != SyncId.None)
            {
                material.AssignMap(sourceId, k_IdCutoutMap, syncMaterial.CutoutMap, textureCache);
            }
                
            // Normal
            material.AssignMap(sourceId, k_IdBumpMap, syncMaterial.NormalMap, textureCache);
            material.SetFloat(k_IdBumpScale, syncMaterial.NormalScale);
                
            // Smoothness
            material.SetFloat(k_IdSmoothness, syncMaterial.Glossiness);
            material.AssignMap(sourceId, k_IdSmoothnessMap, syncMaterial.GlossinessMap, textureCache);
            material.SetFloat(k_SuffixIds[k_IdSmoothnessMap].invert, 1.0f); // TODO should be determined from the exporter

            // Metallic
            if (!isTransparent) // TODO Fix transparent VS metallic issues
            {
                material.SetFloat(k_IdMetallic, syncMaterial.Metallic);
                material.AssignMap(sourceId, k_IdMetallicMap, syncMaterial.MetallicMap, textureCache);
            }

            // Emission
            var emissionMode = EmissionMode.None;
                
            if (syncMaterial.EmissionMap.TextureId != SyncId.None)
            {
                material.AssignMap(sourceId, k_IdEmissionMap, syncMaterial.EmissionMap, textureCache);
                    
                emissionMode = EmissionMode.Map;
            }
            else if (syncMaterial.Emission != SyncColor.Black)
            {
                var emissionColor = ImportersUtils.ColorFromTemperature(syncMaterial.EmissionTemperature);
                emissionColor *= ImportersUtils.GetUnityColor(syncMaterial.Emission);
                material.SetColor(k_IdEmission, emissionColor);

                emissionMode = EmissionMode.Color;
            }

            material.globalIlluminationFlags = emissionMode != EmissionMode.None
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                
            material.SetFloat(k_IdEmissionMode, (float)emissionMode);

            // Compile material
            ComputeFlagsAndKeywords(material);
        }

        public static void ComputeFlagsAndKeywords(Material material)
        {
            k_ActiveMapIds.Clear();

            SetMapFlag(material, k_IdUseAlbedoMap, k_IdMainTex, k_ActiveMapIds);

            SetMapFlag(material, k_IdUseNormalMap, k_IdBumpMap, k_ActiveMapIds, !material.GetFloat(k_IdBumpScale).Equals(0.0f));

            SetMapFlag(material, k_IdUseSmoothnessMap, k_IdSmoothnessMap, k_ActiveMapIds);
            SetMapFlag(material, k_IdUseMetallicMap, k_IdMetallicMap, k_ActiveMapIds);

            var emissionMode = (EmissionMode) material.GetFloat(k_IdEmissionMode);

            SetMapFlag(material, k_IdUseEmissionMap, k_IdEmissionMap, k_ActiveMapIds, emissionMode == EmissionMode.Map);
            
            if (material.HasProperty(k_IdCutoutThreshold))
            {
                var hasCutout = material.GetTexture(k_IdCutoutMap) != null && material.GetFloat(k_IdCutoutThreshold) > 0.0f;
                
                SetMapKeyword(material, k_IdUseCutout, k_IdCutoutMap, k_ActiveMapIds, hasCutout);

                if (material.HasProperty(k_IdCull))
                {
                    material.SetInt(k_IdCull, hasCutout ?
                        (int) CullMode.Off : (int) CullMode.Back);
                }
            }
            
            if (material.HasProperty(k_IdAlpha))
            {
                SetMapFlag(material, k_IdUseAlphaMap, k_IdAlphaMap, k_ActiveMapIds, !material.GetFloat(k_IdAlpha).Equals(0.0f));
            }

            SetMapRotationKeyword(material, k_ActiveMapIds);
        }

        static void SetMapFlag(Material material, int propertyId, int id, List<int> activeMaps, bool extraCondition = true)
        {
            var active = extraCondition && material.GetTexture(id) != null;

            material.SetInt(propertyId, active ? 1 : 0);

            if (active)
                activeMaps.Add(id);
        }

        static void SetMapKeyword(Material material, string keyword, int id, List<int> activeMaps, bool extraCondition = true)
        {
            var active = extraCondition && material.GetTexture(id) != null;

            SetKeyword(material, keyword, active);

            if (active)
                activeMaps.Add(id);
        }
        
        static void SetMapRotationKeyword(Material material, List<int> activeMaps)
        {
            var hasRot = false;

            foreach (var mapId in activeMaps)
            {
                var rot = material.GetFloat(k_SuffixIds[mapId].rotation);
                hasRot = !rot.Equals(0.0f) && !rot.Equals(360.0f);
                
                if (hasRot) break;
            }
            
            SetKeyword(material, k_KeywordMapRotation, hasRot);
        }

        static void SetKeyword(Material m, string keyword, bool state)
        {
            if (state)
                m.EnableKeyword(keyword);
            else
                m.DisableKeyword(keyword);
        }

        static void AssignColor(this Material material, int id, SyncColor color, bool keepAlpha = true, bool preventBlack = false)
        {
            var finalColor = ImportersUtils.GetUnityColor(color, keepAlpha);

            if (preventBlack)
            {
                // Too dark color does render nicely since black usually kills any other color and reflection
                const float threshold = 0.3f;
                if (finalColor.r < threshold && finalColor.g < threshold && finalColor.b < threshold)
                {
                    finalColor = new Color(threshold, threshold, threshold, finalColor.a);
                }
            }
            
            material.SetColor(id, finalColor);
        }

        static void AssignMap(this Material material, string sourceId, int id, SyncMap map, ITextureCache textureCache)
        {
            Texture2D texture2D = null;

            if (map.TextureId != SyncId.None)
            {
                var textureKey = StreamKey.FromSyncId<SyncTexture>(sourceId, map.TextureId);
                texture2D = textureCache.GetTexture(textureKey);
            }

            material.SetTexture(id, texture2D);

            var offset = map.Offset;
            material.SetTextureOffset(id, new Vector2(offset.X, offset.Y));

            var tiling = map.Tiling;
            material.SetTextureScale(id, new Vector2(tiling.X, tiling.Y));

            var mapSuffixId = k_SuffixIds[id];
            material.SetFloat(mapSuffixId.brightness, map.Brightness);
            material.SetFloat(mapSuffixId.invert, map.Invert ? 1.0f : 0.0f);
            material.SetFloat(mapSuffixId.rotation, map.RotationDegrees);
        }
    }
}