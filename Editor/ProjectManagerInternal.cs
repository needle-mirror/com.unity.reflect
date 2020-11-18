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

        public event Action<Project> onProjectAdded;
        public event Action<Project> onProjectChanged;
        public event Action<Project> onProjectRemoved;

        public event Action<Exception> onError;

        public event Action onAuthenticationFailure;
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

        public ProjectManagerInternal() : this(ProjectServer.ProjectDataPath, true, false)
        {
        }

        bool IsProjectAvailable(Project project) => m_Projects.ContainsKey(project.serverProjectId);

        public bool IsProjectAvailableOffline(Project project) => IsProjectAvailable(project) && m_LocalStorage.HasLocalData(project);

        public bool IsProjectAvailableOnline(Project project) => IsProjectAvailable(project) && project.isAvailableOnline;

        public string GetProjectFolder(Project project)
        {
            return m_LocalStorage.GetProjectFolder(project);
        }

        public string GetSourceProjectFolder(Project project, string sessionId)
        {
            return m_LocalStorage.GetSourceProjectFolder(project, sessionId);
        }

        public IEnumerable<SourceProject> LoadProjectManifests(Project project)
        {
            return m_LocalStorage.LoadProjectManifests(project);
        }

        public IEnumerator DownloadProjectLocally(Project project, bool incremental, Action<Exception> errorHandler)
        {
            Action<Exception> completeWithError = (ex) =>
            {
                onError?.Invoke(ex);
                errorHandler?.Invoke(ex);
            };

            IPlayerClient client = null;
            try
            {
                try
                {
                    client = Player.CreateClient(project, ProjectServer.UnityUser, ProjectServer.Client);
                }
                catch (ConnectionException ex)
                {
                    var unityProject = (UnityProject)project;
                    string message;
                    if (unityProject.Host == UnityProjectHost.LocalService)
                    {
                        message = "A connection with your local server could not be established. Make sure the Unity Reflect Service is running.";
                    }
                    else
                    {
                        if (unityProject.Host.ServerName == "Cloud")
                        {
                            message = $"A connection with Reflect Cloud could not be established.";
                        }
                        else
                        {
                            message = $"A connection with the server {unityProject.Host.ServerName} could not be established. This server may be outside your local network (LAN) or may not accept external connections due to firewall policies.";
                        }
                    }

                    ex = new ConnectionException(message, ex);
                    completeWithError(ex);
                    throw ex;
                }
                catch (Exception ex)
                {
                    completeWithError(ex);
                    throw;
                }

                ManifestAsset[] manifestEntries;
                try
                {
                    // TODO: Use async service call and yield until completion
                    manifestEntries = client.GetManifests().ToArray();
                }
                catch (Exception ex)
                {
                    completeWithError(ex);
                    throw;
                }

                progressChanged?.Invoke(0.0f, k_Downloading);

                var total = manifestEntries.Length;
                var projectPercent = 1.0f / total;

                var localManifests = new Dictionary<string, SyncManifest>();

                if (incremental)
                {
                    IEnumerable<SourceProject> localSourceProjects = null;

                    try
                    {
                        localSourceProjects = m_LocalStorage.LoadProjectManifests(project).ToArray();
                    }
                    catch (ReflectVersionException)
                    {
                        if (manifestEntries.Length == 0)
                        {
                            var ex = new Exception($"Cannot open project {project.name} because it has been exported with a different version of Unity Reflect.");
                            completeWithError(ex);
                            throw ex;
                        }
                    }
                    catch (Exception ex)
                    {
                        completeWithError(ex);
                        throw;
                    }

                    if (localSourceProjects != null)
                    {
                        foreach (var sourceProject in localSourceProjects)
                        {
                            localManifests.Add(sourceProject.sourceId, sourceProject.manifest);
                        }
                    }
                }

                m_LocalStorage.SaveProjectData(project);

                for (int i = 0; i < total; ++i)
                {
                    var manifestEntry = manifestEntries[i];

                    localManifests.TryGetValue(manifestEntry.SourceId, out var oldManifest);

                    yield return DownloadSourceProjectLocally(project, manifestEntry.SourceId,
                        oldManifest, manifestEntry.Manifest,
                        client,
                        p =>
                        {
                            var percent = (i + p) * projectPercent;
                            progressChanged?.Invoke(percent, k_Downloading);
                        }, errorHandler);
                }

                onProjectChanged?.Invoke(project);

                progressChanged?.Invoke(1.0f, k_Downloading);
            }
            finally
            {
                client?.Dispose();
                taskCompleted?.Invoke();
            }
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

        DownloadProgress m_DownloadProgress = new DownloadProgress();

        public IEnumerator DownloadSourceProjectLocally(Project project, string sourceId, SyncManifest oldManifest, SyncManifest newManifest, IPlayerClient client,
            Action<float> onProgress, Action<Exception> errorHandler = null)
        {
            onProgress?.Invoke(0.0f);

            var task = Task.Run(() => DownloadTask(client, oldManifest, newManifest, project, sourceId, m_DownloadProgress));

            // Breath and let one frame render for UI feedback
            yield return null;
            
            do
            {
                while (m_DownloadProgress.errors.TryDequeue(out var error))
                {
                    Debug.LogError($"Unable to download '{error.entry}' : {error.exception?.Message}");
                }

                onProgress?.Invoke(m_DownloadProgress.percent);

                // Breath
                yield return null;

            } while (!task.IsCompleted || !m_DownloadProgress.errors.IsEmpty);

            onProgress?.Invoke(1.0f);

            if (task.IsFaulted)
            {
                var exception = task.Exception?.InnerException ?? task.Exception;
                
                Debug.LogError($"Error while downloading Source '{sourceId}' in Project '{project.name}' : {exception?.Message}");
                onError?.Invoke(exception);
                errorHandler?.Invoke(exception);
            }
        }

        async Task DownloadTask(IPlayerClient client, SyncManifest oldManifest, SyncManifest newManifest,
            UnityProject project, string sourceId, DownloadProgress progress)
        {
            List<ManifestEntry> entries;

            if (oldManifest == null)
            {
                var content = newManifest.Content;
                entries = content.Values.ToList();
            }
            else
            {
                oldManifest.ComputeDiff(newManifest, out var modified, out var deleted);
                entries = modified.ToList();

                // TODO Handle deleted models
            }
            
            progress.SetTotal(entries.Count);

            var destinationFolder = m_LocalStorage.GetSourceProjectFolder(project, sourceId);

            var tasks = entries.Select(entry => DownloadAndStore(client, sourceId, entry, newManifest, destinationFolder, progress)).ToList();

            // Don't forget the manifest itself
            tasks.Add(RunFileIOOperation(() =>
            {
                newManifest.EditorSave(destinationFolder);
                return Task.CompletedTask;
            }));
            
            // Wait for all download to finish
            await Task.WhenAll(tasks);
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

        public IEnumerator DeleteProjectLocally(Project project)
        {
            var projectFolderPath = m_LocalStorage.GetProjectFolder(project);
            if (!Directory.Exists(projectFolderPath))
            {
                Debug.LogWarning($"Cannot delete locally stored project '{project.projectId}'");
                yield break;
            }

            const string kDeleting = "Deleting";

            progressChanged?.Invoke(0.0f, kDeleting);

            // Deleting each file individually is slow. Instead, get all leaf directories and delete them one after the other.
            var projectDirectories = Directory.EnumerateDirectories(projectFolderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !Directory.EnumerateDirectories(f, "*.*", SearchOption.TopDirectoryOnly).Any()).ToList();

            var folderCount = projectDirectories.Count;

            for (int i = 0; i < projectDirectories.Count; ++i)
            {
                Directory.Delete(projectDirectories[i], true);

                progressChanged?.Invoke((float)i / folderCount, kDeleting);
                yield return null;
            }

            Directory.Delete(projectFolderPath, true);

            progressChanged?.Invoke(1.0f, kDeleting);
            yield return null;

            UpdateProjectInternal(project, false);

            taskCompleted?.Invoke();
        }

        void UpdateProjectInternal(Project project, bool canAddProject)
        {
            if (m_Projects.ContainsKey(project.serverProjectId))
            {
                m_Projects[project.serverProjectId] = project;
                onProjectChanged?.Invoke(project);
            }
            else if (canAddProject)
            {
                m_Projects[project.serverProjectId] = project;
                onProjectAdded?.Invoke(project);
            }
        }

        void RemoveProjectInternal(string serverProjectId)
        {
            if (m_Projects.TryGetValue(serverProjectId, out var project))
            {
                m_Projects.Remove(serverProjectId);
                onProjectRemoved?.Invoke(project);
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
                    onAuthenticationFailure?.Invoke();
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
