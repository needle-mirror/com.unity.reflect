using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{   
    public interface IMaterialCache
    {
        Material GetMaterial(SyncId id);
    }
    
    class RuntimeMaterialCache : RuntimeCache<SyncMaterial, Material>, IMaterialCache
    {
        ITextureCache m_TextureCache;
        
        public RuntimeMaterialCache(ITextureCache textureCache, AssetSource sources)
            : base(new SyncMaterialImporter(), sources)
        {
            m_TextureCache = textureCache;
        }
                
        public Material GetMaterial(SyncId id)
        {
            return id == SyncId.None ? null : Import(id.Value, m_TextureCache);
        }
        
        public Material Reimport(string key)
        {
            return Reimport(key, m_TextureCache);
        }
    }
}