using System;
using System.Collections.Generic;
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
        // Used in shaders
        static bool IsTransparent(SyncMaterial syncMaterial)
        {
            return syncMaterial.Alpha < 1.0f || !string.IsNullOrEmpty(syncMaterial.AlphaMap.Texture);
        }
        
        public static Shader GetShader(SyncMaterial syncMaterial)
        {
            var shaderName = IsTransparent(syncMaterial) ? "UnityReflect/Standard Transparent" : "UnityReflect/Standard Opaque";
            
            return Shader.Find(shaderName);
        }

        public static Material ComputeMaterial(SyncMaterial syncMaterial, Material material, ITextureCache textureCache)
        {
            material.name = syncMaterial.Name;
            material.shader = GetShader(syncMaterial);

            // Tint
            material.AssignColor("_Tint", syncMaterial.Tint, false);
                
            // Albedo
            material.AssignColor("_AlbedoColor", syncMaterial.AlbedoColor);
            material.AssignMap("_MainTex", syncMaterial.AlbedoMap, textureCache);
            material.SetFloat("_MainTex_Fade", syncMaterial.AlbedoFade);

            var isTransparent = IsTransparent(syncMaterial);
            
            // Alpha
            if (isTransparent)
            {
                material.SetFloat("_Alpha", syncMaterial.Alpha);
                material.AssignMap("_AlphaMap", syncMaterial.AlphaMap, textureCache);
            }

            if (!string.IsNullOrEmpty(syncMaterial.CutoutMap.Texture))
            {
                material.AssignMap("_CutoutMap", syncMaterial.CutoutMap, textureCache);
            }
                
            // Normal
            material.AssignMap("_BumpMap", syncMaterial.NormalMap, textureCache);
            material.SetFloat("_BumpScale", syncMaterial.NormalScale);
                
            // Smoothness
            material.SetFloat("_Smoothness", syncMaterial.Glossiness);
            material.AssignMap("_SmoothnessMap", syncMaterial.GlossinessMap, textureCache);
            material.SetFloat("_SmoothnessMap_I", 1.0f); // TODO should be determined from the exporter

            // Metallic
            if (!isTransparent) // TODO Fix transparent VS metallic issues
            {
                material.SetFloat("_Metallic", syncMaterial.Metallic);
                material.AssignMap("_MetallicMap", syncMaterial.MetallicMap, textureCache);
            }

            // Emission
            var emissionMode = EmissionMode.None;
                
            if (!string.IsNullOrEmpty(syncMaterial.EmissionMap.Texture))
            {
                material.AssignMap("_EmissionMap", syncMaterial.EmissionMap, textureCache);
                    
                emissionMode = EmissionMode.Map;
            }
            else if (syncMaterial.Emission != SyncColor.Black())
            {
                var emissionColor = ImportersUtils.ColorFromTemperature(syncMaterial.EmissionTemperature);
                emissionColor *= ImportersUtils.GetUnityColor(syncMaterial.Emission);
                material.SetColor("_Emission", emissionColor);

                emissionMode = EmissionMode.Color;
            }

            material.globalIlluminationFlags = emissionMode != EmissionMode.None
                ? MaterialGlobalIlluminationFlags.RealtimeEmissive
                : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                
            material.SetFloat("_EmissionMode", (float)emissionMode);
                
            // Compile material
            ComputeKeywords(material);

            return material;
        }
        
        public static void ComputeKeywords(Material material)
        {
            var activeMaps = new List<string>();
            
            SetMapKeyword(material, "_UseAlbedoMap", "_MainTex", ref activeMaps);
            
            SetMapKeyword(material, "_UseNormalMap", "_BumpMap", ref activeMaps, !material.GetFloat("_BumpScale").Equals(0.0f));
            
            SetMapKeyword(material, "_UseSmoothnessMap", "_SmoothnessMap", ref activeMaps);
            SetMapKeyword(material, "_UseMetallicMap", "_MetallicMap", ref activeMaps);

            var emissionMode = (EmissionMode) material.GetFloat("_EmissionMode");

            SetMapKeyword(material, "_UseEmissionMap", "_EmissionMap", ref activeMaps, emissionMode == EmissionMode.Map);
            
            if (material.HasProperty("_CutoutThreshold"))
            {
                var hasCutout = material.GetTexture("_CutoutMap") != null && material.GetFloat("_CutoutThreshold") > 0.0f;
                
                SetMapKeyword(material, "_UseCutout", "_CutoutMap", ref activeMaps, hasCutout);

                if (material.HasProperty("_Cull"))
                {
                    material.SetInt("_Cull", hasCutout ?
                        (int) CullMode.Off : (int) CullMode.Back);
                }
            }
            
            if (material.HasProperty("_Alpha"))
            {
                SetMapKeyword(material, "_ALPHA_MAP", "_AlphaMap", ref activeMaps, !material.GetFloat("_Alpha").Equals(0.0f));
            }

            SetMapRotationKeyword(material, activeMaps);
        }

        static void SetMapKeyword(Material material, string keyword, string name, ref List<string> activeMaps, bool extraCondition = true)
        {
            var active = extraCondition && material.GetTexture(name) != null;
            material.SetFloat(keyword, active ? 1.0f : 0.0f);
            if (active)
                activeMaps.Add(name);
        }
        
        static void SetMapRotationKeyword(Material material, List<string> activeMaps)
        {
            var hasRot = false;

            foreach (var mapName in activeMaps)
            {
                var rot = material.GetFloat(mapName + "_R");
                hasRot = !rot.Equals(0.0f) && !rot.Equals(360.0f);
                
                if (hasRot) break;
            }
            
            SetKeyword(material, "_MAP_ROTATION", hasRot);
        }

        static void SetKeyword(Material m, string keyword, bool state)
        {
            if (state)
                m.EnableKeyword(keyword);
            else
                m.DisableKeyword(keyword);
        }

        static void AssignColor(this Material material, string name, SyncColor color, bool keepAlpha = true)
        {
            material.SetColor(name, ImportersUtils.GetUnityColor(color, keepAlpha));
        }

        static void AssignMap(this Material material, string name, SyncMap map, ITextureCache textureCache)
        {
            var texture2D = string.IsNullOrEmpty(map.Texture) ? null : textureCache.GetTexture(map.Texture);

            material.SetTexture(name, texture2D);
            material.SetTextureOffset(name, new Vector2(map.Offset.X, map.Offset.Y));
            material.SetTextureScale(name, new Vector2(map.Tiling.X, map.Tiling.Y));
            material.SetFloat(name + "_B", map.Brightness);
            material.SetFloat(name + "_I", map.Invert ? 1.0f : 0.0f);
            material.SetFloat(name + "_R", map.RotationDegrees);
        }
    }
}