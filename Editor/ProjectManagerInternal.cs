using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEditor.Reflect
{
    
    class ProjectManagerInternal : IProgressTask
    {
        readonly Dictionary<string, Project> m_Projects = new Dictionary<string, Project>();

        public event Action onProjectsRefreshBegin;
        public event Action onProjectsRefreshEnd;

        public event Action<Exception> onError;
        
        public event Action<float, string> progressChanged;
        public event Action taskCompleted;

        readonly PlayerStorage m_LocalStorage;

        public IEnumerable<Project> Projects => m_Projects.Values;

        static readonly string k_Downloading = "Downloading";

        const int k_FileIOAttempts = 3;

        const int k_FileIORetryDelayMS = 1000;

        public ProjectManagerInternal(string storageRoot, bool useServerFolder, bool useProjectNameAsRootFolder)
        {
            m_LocalStorage = new PlayerStorage(storageRoot, useServerFolder, useProjectNameAsRootFolder);
        }

        bool IsProjectAvailable(Project project) => m_Projects.ContainsKey(project.serverProjectId);

        public bool IsProjectAvailableOffline(Project project) => IsProjectAvailable(project) && m_LocalStorage.HasLocalData(project);

        public bool IsProjectAvailableOnline(Project project) => IsProjectAvailable(project) && project.isAvailableOnline;

        public string GetProjectFolder(Project project)
        {
            return m_LocalStorage.GetProjectFolder(project);
        }

        internal struct DownloadError
        {
            public readonly Exception exception;
            public readonly ManifestEntry entry;

            public DownloadError(Exception exception, ManifestEntry entry)
            {
                this.exception = exception;
                this.entry = entry;
            }
        }

        internal class DownloadProgress
        {
            readonly object m_Lock = new object();
            
            public float percent
            {
                get
                {
                    lock (m_Lock)
                    {
                        return m_Total == 0 ? 0.0f : ((float)m_Current) / m_Total;
                    }
                }
            }

            public ConcurrentQueue<DownloadError> errors = new ConcurrentQueue<DownloadError>();

            int m_Total;
            int m_Current;

            public void SetTotal(int totalExpected)
            {
                lock (m_Lock)
                {
                    m_Current = 0;
                    m_Total = totalExpected;
                }

                errors = new ConcurrentQueue<DownloadError>();
            }

            public void ReportCompleted()
            {
                lock (m_Lock)
                {
                    m_Current += 1;
                }
            }
        }

        internal static async Task DownloadAndStore(IPlayerClient client, string sourceId, ManifestEntry entry, SyncManifest newManifest,
            string downloadFolder, DownloadProgress progress)
        {
            Exception exception = null;
            
            try
            {
                var syncModel = await client.GetSyncModelAsync(sourceId, entry.ModelPath, entry.Hash);

                if (syncModel != null)
                {
                    // Replace model name with local model paths
                    syncModel.Name = entry.ModelPath;
                    SetReferencedSyncModelPath(syncModel, newManifest);

                    var fullPath = Path.Combine(downloadFolder, entry.ModelPath);
                    var directory = Path.GetDirectoryName(fullPath);

                    await RunFileIOOperation(async () =>
                    {
                        Directory.CreateDirectory(directory);
                        await PlayerFile.SaveAsync(syncModel, fullPath);
                    });
                }
            }
            catch (Exception ex)
            {
                exception = ex;
            }
            
            if (exception != null)
            {
                progress.errors.Enqueue(new DownloadError(exception, entry));
            }
                            
            progress.ReportCompleted();
        }
        
        internal static async Task RunFileIOOperation(Func<Task> operation)
        {
            var remainingAttempts = k_FileIOAttempts;
            do
            {
                try
                {
                    await operation();
                    return;
                }
                catch (IOException e)
                {
                    if (--remainingAttempts <= 0)
                    {
                        var ex = new IOException($"File IO operation abandoned after {k_FileIOAttempts} attempts", e);
                        throw ex;
                    }
                }

                await Task.Delay(k_FileIORetryDelayMS);

            } while (remainingAttempts > 0);
        }

        static void SetReferencedSyncModelPath(ISyncModel syncModel, SyncManifest manifest)
        {
            switch (syncModel)
            {
                // Keep for backward compatibility with old client cache model
                case SyncPrefab syncPrefab:
                    SetReferencedSyncModelPath(syncPrefab, manifest);
                    break;

                case SyncObject syncObject:
                    SetReferencedSyncModelPath(syncObject, manifest);
                    break;

                case SyncMaterial syncMaterial:
                    SetReferencedSyncModelPath(syncMaterial, manifest);
                    break;
                
                case SyncObjectInstance syncObjectInstance:
                    SetReferencedSyncModelPath(syncObjectInstance, manifest);
                    break;
            }
        }

        static void SetReferencedSyncModelPath(SyncPrefab syncPrefab, SyncManifest manifest)
        {
            foreach (var instance in syncPrefab.Instances)
            {
                SetReferencedSyncModelPath(instance, manifest);
            }
        }

        static void SetReferencedSyncModelPath(SyncObjectInstance instance, SyncManifest manifest)
        {
            instance.ObjectId = new SyncId(GetSyncModelLocalPath<SyncObject>(instance.ObjectId.Value, manifest));
        }

        static void SetReferencedSyncModelPath(SyncObject syncObject, SyncManifest manifest)
        {
            syncObject.MeshId = new SyncId(GetSyncModelLocalPath<SyncMesh>(syncObject.MeshId.Value, manifest));
            for (var i = 0; i < syncObject.MaterialIds.Count; ++i)
            {
                syncObject.MaterialIds[i] = new SyncId(GetSyncModelLocalPath<SyncMaterial>(syncObject.MaterialIds[i].Value, manifest));
            }
            foreach (var child in syncObject.Children)
            {
                SetReferencedSyncModelPath(child, manifest);
            }
        }

        static void SetReferencedSyncModelPath(SyncMaterial material, SyncManifest manifest)
        {
            SetReferencedSyncModelPath(material.AlbedoMap, manifest);
            SetReferencedSyncModelPath(material.AlphaMap, manifest);
            SetReferencedSyncModelPath(material.NormalMap, manifest);
            SetReferencedSyncModelPath(material.CutoutMap, manifest);
            SetReferencedSyncModelPath(material.EmissionMap, manifest);
            SetReferencedSyncModelPath(material.GlossinessMap, manifest);
            SetReferencedSyncModelPath(material.MetallicMap, manifest);
        }

        static void SetReferencedSyncModelPath(SyncMap map, SyncManifest manifest)
        {
            if (map == null || map.TextureId == SyncId.None)
                return;

            map.TextureId = new SyncId(GetSyncModelLocalPath<SyncTexture>(map.TextureId.Value, manifest));
        }

        static string GetSyncModelLocalPath<T>(string modelName, SyncManifest manifest) where T : ISyncModel
        {
            var path = "";
            if (string.IsNullOrEmpty(modelName))
            {
                return path;
            }

            var key = PersistentKey.GetKey<T>(modelName);
            if (manifest.Content.TryGetValue(key, out var entry))
            {
                path = entry.ModelPath;
            }
            else
            {
                Debug.LogError("Unable to get local path for '" + key + "'...");
            }

            return path;
        }

        void UpdateProjectInternal(Project project, bool canAddProject)
        {
            if (m_Projects.ContainsKey(project.serverProjectId))
            {
                m_Projects[project.serverProjectId] = project;
            }
            else if (canAddProject)
            {
                m_Projects[project.serverProjectId] = project;
            }
        }

        void RemoveProjectInternal(string serverProjectId)
        {
            if (m_Projects.TryGetValue(serverProjectId, out var project))
            {
                m_Projects.Remove(serverProjectId);
            }
        }

        public IEnumerator RefreshProjectListCoroutine()
        {
            onProjectsRefreshBegin?.Invoke();

            m_Projects.Clear();
            ProjectServer.Init();

            var user = ProjectServer.UnityUser;
            var projects = new List<Project>();

            if (user != null)
            {
                // Use ContinueWith to make sure the task doesn't throw
                var task = Task.Run(() => ProjectServer.Client.ListProjects(user, m_LocalStorage)).ContinueWith(t => t);
                while (!task.IsCompleted)
                {
                    yield return null;
                }

                var listTask = task.Result;
                if (listTask.IsFaulted)
                {
                    Debug.LogException(listTask.Exception);
                    onError?.Invoke(listTask.Exception);
                    onProjectsRefreshEnd?.Invoke();
                    yield break;
                }

                var result = listTask.Result;
                if (result.Status == UnityProjectCollection.StatusOption.AuthenticationError)
                {
                    // NOTE: Keep on going, we may be able to display offline data
                }
                else if (result.Status == UnityProjectCollection.StatusOption.ComplianceError)
                {
                    onError?.Invoke(new ProjectListRefreshException(result.ErrorMessage, result.Status));
                    // NOTE: Keep on going, we may be able to display offline data
                }                
                else if (result.Status != UnityProjectCollection.StatusOption.Success)
                {
                    // Log error with details but report simplified message in `onError` event
                    Debug.LogError($"Project list refresh failed: {result.ErrorMessage}");
                    onError?.Invoke(new ProjectListRefreshException($"Could not connect to Reflect cloud service, check your internet connection.", result.Status));
                    // NOTE: Keep on going, we may be able to display offline data
                }

                projects.AddRange(listTask.Result.Select(p => new Project(p)));
            }

            projects.ForEach(p => UpdateProjectInternal(p, true));

            var projectIds = projects.Select(p => p.serverProjectId);
            var removedProjectIds = m_Projects.Keys.Except(projectIds).ToList();
            removedProjectIds.ForEach(RemoveProjectInternal);

            onProjectsRefreshEnd?.Invoke();
        }

        public void Cancel()
        {
            ProjectServer.Cleanup();
        }
    }
}
