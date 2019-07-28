using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public interface IMeshCache
    {
        Mesh GetMesh(string key);
    }
    
    class RuntimeMeshCache : RuntimeCache<SyncMesh, Mesh>, IMeshCache
    {       
        public RuntimeMeshCache(AssetSource sources)
            : base(new SyncMeshImporter(), sources)
        {
        }
        
        public Mesh GetMesh(string key)
        {
            return Import(key, null);
        }  
        
        public void Reimport(string key)
        {
            Reimport(key, null);
        }
    }
}