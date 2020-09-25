using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    public interface IProjectProvider
    {
        Task<UnityProjectCollection> ListProjects();
    }
    
    public interface ISyncModelProvider
    {
        Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync();
        Task<ISyncModel> GetSyncModelAsync(StreamKey streamKey, string hash);
    }
}
