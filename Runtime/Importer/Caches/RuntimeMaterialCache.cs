using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{   
    public interface IMaterialCache
    {
        Material GetMaterial(string key);
    }
    
    class RuntimeMaterialCache : RuntimeCache<SyncMaterial, Material>, IMaterialCache
    {
        ITextureCache m_TextureCache;
        
        public RuntimeMaterialCache(ITextureCache textureCache, AssetSource sources)
            : base(new SyncMaterialImporter(), sources)
        {
            m_TextureCache = textureCache;
        }
                
        public Material GetMaterial(string key)
        {
            return string.IsNullOrEmpty(key) ? null : Import(key, m_TextureCache);
        }
        
        public Material Reimport(string key)
        {
            return Reimport(key, m_TextureCache);
        }
    }
}