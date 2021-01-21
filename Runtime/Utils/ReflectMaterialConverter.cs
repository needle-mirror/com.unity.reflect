using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect;
using Unity.Reflect.Model;
using UnityEditor;
using UnityEngine.Rendering;

namespace UnityEngine.Reflect
{
    public interface IReflectMaterialConverter
    {
        string name { get; }
        bool IsAvailable { get; }

        Material defaultMaterial { get; }

        Shader GetShader(SyncMaterial syncMaterial);

        void SetMaterialProperties(SyncedData<SyncMaterial> syncMaterial, Material material, ITextureCache textureCache);
    }
    
    class StandardPipelineMaterialConverter : IReflectMaterialConverter
    {
        public string name => "Reflect Standard";
        public bool IsAvailable => true;

        public Shader GetShader(SyncMaterial syncMaterial)
        {
            return StandardShaderHelper.GetShader(syncMaterial);
        }

        public void SetMaterialProperties(SyncedData<SyncMaterial> syncMaterial, Material material, ITextureCache textureCache)
        {
            StandardShaderHelper.ComputeMaterial(syncMaterial, material, textureCache);
        }
        
        Material m_DefaultMaterial;

        public Material defaultMaterial
        {
            get
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");
#endif

                if (m_DefaultMaterial == null)
                {
                    m_DefaultMaterial = new Material(StandardShaderHelper.GetShader(false));
                    m_DefaultMaterial.SetColor("_AlbedoColor", Color.gray);
                }

                return m_DefaultMaterial;
            }
        }
    }
    
    abstract class SRPMaterialConverter : IReflectMaterialConverter
    {
        public abstract string name { get; }
        public abstract bool IsAvailable { get; }
        
        public Shader GetShader(SyncMaterial syncMaterial)
        {
            return currentRenderPipeline.defaultShader;
        }
        
        public void SetMaterialProperties(SyncedData<SyncMaterial> syncMaterial, Material material, ITextureCache textureCache)
        {
            SetMaterialPropertiesInternal(syncMaterial, material, textureCache);
        }

        protected static RenderPipelineAsset currentRenderPipeline
        {
            get {
#if UNITY_2019_3
                return GraphicsSettings.currentRenderPipeline;
#else
                return GraphicsSettings.renderPipelineAsset;
#endif
            }
        }
        
        protected abstract void SetMaterialPropertiesInternal(SyncedData<SyncMaterial> syncMaterial, Material material, ITextureCache textureCache);

        public Material defaultMaterial => currentRenderPipeline.defaultMaterial;

        protected static void AssignMap(Material material, string sourceId, string id, SyncMap map, ITextureCache textureCache) // TODO Use int ID
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
        }
    }

    class UniversalRenderPipelineMaterialConverter : SRPMaterialConverter
    {
        public override string name => "Universal RenderPipeline";

        public override bool IsAvailable
        {
            get
            {
#if URP_AVAILABLE
                if (currentRenderPipeline != null && currentRenderPipeline.GetType().Name.Contains("UniversalRenderPipeline"))
                    return true;
#endif

                return false;
            }
        }

        protected override void SetMaterialPropertiesInternal(SyncedData<SyncMaterial> syncedMaterial, Material material, ITextureCache textureCache)
        {
            var syncMaterial = syncedMaterial.data;
            var sourceId = syncedMaterial.key.source;
            
            var transparent = StandardShaderHelper.IsTransparent(syncMaterial);

            var tint = ImportersUtils.GetUnityColor(syncMaterial.Tint, false);
            if (transparent)
            {
                tint.a = syncMaterial.Alpha;
            }
            
            // Albedo
            material.SetColor("_BaseColor", tint);
            AssignMap(material, sourceId, "_BaseMap", syncMaterial.AlbedoMap, textureCache);
            
            // Metallic
            material.SetFloat("_Metallic", syncMaterial.Metallic);
            if (syncMaterial.MetallicMap.TextureId != SyncId.None)
            {
                AssignMap(material, sourceId, "_MetallicGlossMap", syncMaterial.NormalMap, textureCache);
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            
            // Smoothness
            material.SetFloat("_Smoothness", syncMaterial.Glossiness);
            if (syncMaterial.GlossinessMap.TextureId != SyncId.None)
            {
                // TODO
            }

            // Normal
            if (syncMaterial.NormalMap.TextureId != SyncId.None)
            {
                AssignMap(material, sourceId, "_BumpMap", syncMaterial.NormalMap, textureCache);
                material.SetFloat("_BumpScale", syncMaterial.NormalScale);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            }

            // Transparency
            if (transparent)
            {
                material.SetColor("_Color", tint);
                material.SetFloat("_Surface", 1);
                material.SetFloat("_BlendMode", 0);
                material.SetFloat("_DstBlend",10);
                material.SetFloat("_AlphaDstBlend",10);
                material.SetFloat("_AlphaCutoffEnable", 0);
                material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1);
                material.renderQueue = (int)RenderQueue.Transparent;

                material.EnableKeyword("_BLENDMODE_ALPHA");
                material.EnableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
                material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            
            // Cutout
            // TODO Not supported?

            // Emission
            if (syncMaterial.Emission != SyncColor.Black || syncMaterial.EmissionMap.TextureId != SyncId.None)
            {
                material.SetColor("_EmissionColor", ImportersUtils.GetUnityColor(syncMaterial.Emission));

                if (syncMaterial.EmissionMap.TextureId != SyncId.None)
                {
                    AssignMap(material, sourceId, "_EmissionMap", syncMaterial.EmissionMap, textureCache);
                }
                
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }
    }
    
    class HDRenderPipelineMaterialConverter : SRPMaterialConverter
    {
        public override string name => "HD RenderPipeline";

        public override bool IsAvailable
        {
            get
            {
#if HDRP_AVAILABLE
                if (currentRenderPipeline != null && currentRenderPipeline.GetType().Name.Contains("HDRenderPipeline"))
                    return true;
#endif

                return false;
            }
        }

        protected override void SetMaterialPropertiesInternal(SyncedData<SyncMaterial> syncedMaterial, Material material, ITextureCache textureCache)
        {
            var syncMaterial = syncedMaterial.data;
            var sourceId = syncedMaterial.key.source;
            
            var transparent = StandardShaderHelper.IsTransparent(syncMaterial);
            var emission = syncMaterial.Emission != SyncColor.Black || syncMaterial.EmissionMap.TextureId != SyncId.None;

            var tint = ImportersUtils.GetUnityColor(syncMaterial.Tint, false);
            if (transparent)
            {
                tint.a = syncMaterial.Alpha;
            }
            
            // Albedo
            material.SetColor("_BaseColor", tint);
            AssignMap(material, sourceId, "_BaseColorMap", syncMaterial.AlbedoMap, textureCache);
            
            // Metallic
            material.SetFloat("_Metallic", syncMaterial.Metallic);
            if (syncMaterial.MetallicMap.TextureId != SyncId.None)
            {
                // TODO
            }
            
            // Smoothness
            material.SetFloat("_Smoothness", syncMaterial.Glossiness);
            if (syncMaterial.GlossinessMap.TextureId != SyncId.None)
            {
                // TODO
            }

            // Normal
            if (syncMaterial.NormalMap.TextureId != SyncId.None)
            {
                AssignMap(material, sourceId, "_NormalMap", syncMaterial.NormalMap, textureCache);
                material.SetFloat("_NormalScale", syncMaterial.NormalScale);
                material.EnableKeyword("_NORMALMAP");
                material.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
            }

            // Transparency
            if (!emission && transparent)
            {
                material.SetColor("_Color", tint);
                material.SetFloat("_SurfaceType", 1);
                material.SetFloat("_BlendMode", 0);
                material.SetFloat("_DstBlend",10);
                material.SetFloat("_AlphaDstBlend",10);
                material.SetFloat("_AlphaCutoffEnable", 0);
                material.SetFloat("_EnableBlendModePreserveSpecularLighting", 1);
                material.renderQueue = (int)RenderQueue.Transparent;

                material.EnableKeyword("_BLENDMODE_ALPHA");
                material.EnableKeyword("_BLENDMODE_PRESERVE_SPECULAR_LIGHTING");
                material.EnableKeyword("_ENABLE_FOG_ON_TRANSPARENT");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            
            // Cutout
            // TODO Not supported?

            // Emission
            if (syncMaterial.Emission != SyncColor.Black || syncMaterial.EmissionMap.TextureId != SyncId.None)
            {
                var emissionColor = ImportersUtils.GetUnityColor(syncMaterial.Emission);
                material.SetColor("_EmissiveColor", emissionColor);
                material.SetColor("_EmissiveColorLDR", emissionColor);

                if (syncMaterial.EmissionMap.TextureId != SyncId.None)
                {
                    AssignMap(material, sourceId, "_EmissiveColorMap", syncMaterial.EmissionMap, textureCache);
                }
                
                material.SetInt("_UseEmissiveIntensity", 1);
                //material.EnableKeyword("_Emissive");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }
    }

    class LightweightRenderConverter : UniversalRenderPipelineMaterialConverter
    {
        public override string name => "Lightweight RenderPipeline";

        public override bool IsAvailable
        {
            get
            {
#if LWRP_AVAILABLE
                if (currentRenderPipeline != null && currentRenderPipeline.GetType().Name.Contains("LightweightRenderPipeline"))
                    return true;
#endif

                return false;
            }
        }
    }
    
    public static class ReflectMaterialManager
    {
        static List<IReflectMaterialConverter> s_BuiltInConverters = new List<IReflectMaterialConverter>
        {
            // Order is important // TODO Use priority member
            new UniversalRenderPipelineMaterialConverter(),
            new HDRenderPipelineMaterialConverter(),
            new LightweightRenderConverter(),
            new StandardPipelineMaterialConverter()
        };

        static Dictionary<string, IReflectMaterialConverter> s_Converters;

        public static void RegisterConverter(IReflectMaterialConverter converter)
        {
            if (s_Converters == null)
                s_Converters = new Dictionary<string, IReflectMaterialConverter>();

            s_Converters[converter.name] = converter;
        }
        
        static IReflectMaterialConverter currentConverter
        {
            get
            {
                var converter = s_Converters?.Values.FirstOrDefault(c => c.IsAvailable);
                return converter ?? s_BuiltInConverters.First(c => c.IsAvailable);
            }
        }

        public static void ComputeMaterial(SyncedData<SyncMaterial> syncMaterial, Material material, ITextureCache textureCache)
        {
            material.name = syncMaterial.data.Name;
            material.shader = currentConverter.GetShader(syncMaterial.data);
            currentConverter.SetMaterialProperties(syncMaterial, material, textureCache);
        }
        
        public static Material defaultMaterial => currentConverter.defaultMaterial;
        public static string converterName => currentConverter.name;
        public static Shader GetShader(SyncMaterial syncMaterial)
        {
            return currentConverter.GetShader(syncMaterial);
        }
    }
}
