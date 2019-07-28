
using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public interface ITextureCache
    {
        Texture2D GetTexture(string key);
    }
    
    class RuntimeTextureCache : RuntimeCache<SyncTexture, Texture2D>, ITextureCache
    {
        public RuntimeTextureCache(AssetSource sources)
            : base(new SyncTextureImporter(), sources)
        {
        }
        
        public Texture2D GetTexture(string key)
        {
            return Import(key, null);
        }
        
        public void Reimport(string key)
        {
            Reimport(key, null);
        }
    }
}