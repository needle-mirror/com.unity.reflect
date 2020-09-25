using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public interface IMeshCache
    {
        Mesh GetMesh(SyncId id);
    }
    
    class RuntimeMeshCache : RuntimeCache<SyncMesh, Mesh>, IMeshCache
    {       
        public RuntimeMeshCache(AssetSource sources)
            : base(new SyncMeshImporter(), sources)
        {
        }
        
        public Mesh GetMesh(SyncId id)
        {
            return Import(id.Value, null);
        }  
        
        public void Reimport(string key)
        {
            Reimport(key, null);
        }
    }
}