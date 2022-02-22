using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    public interface IProjectProvider
    {
        public struct ProjectProviderResult
        {
            public IEnumerable<Project> Projects;
            public UnityProjectCollection.StatusOption Status;
            public string ErrorMessage;
        }
        
        Task<ProjectProviderResult> ListProjects();
    }

    public interface ISyncModelProvider
    {
        Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync(CancellationToken token);
        Task<ISyncModel> GetSyncModelAsync(StreamKey streamKey, string hash, CancellationToken token);
    }
}
