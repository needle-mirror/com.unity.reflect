using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class Project
    {
        readonly UnityProject m_UnityProject;

        public static Project Empty { get; } = new Project(new UnityProject(UnityProjectHost.LocalService, string.Empty, string.Empty));
        public string serverProjectId => $"{m_UnityProject.Host.ServerId}:{m_UnityProject.ProjectId}";
        public string projectId => m_UnityProject.ProjectId;
        public string name => m_UnityProject.Name;
        public string description => m_UnityProject.Host.ServerName;
        internal bool isAvailableOnline => m_UnityProject.Source == UnityProject.SourceOption.ProjectServer;

        internal Project(UnityProject unityProject)
        {
            m_UnityProject = unityProject;
        }

        public static implicit operator UnityProject(Project project)
        {
            return project.m_UnityProject;
        }
    }

    class ProjectListRefreshException : Exception
    {
        public UnityProjectCollection.StatusOption Status { get; }

        public ProjectListRefreshException(string message, UnityProjectCollection.StatusOption status)
            : base(message)
        {
            Status = status;
        }
    }

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

        const float k_FileIORetryDelaySeconds = 1;

        public ProjectManagerInternal(string storageRoot, bool useServerFolder, bool useProjectNameAsRootFolder)
        {
            m_LocalStorage = new PlayerStorage(storageRoot, useServerFolder, useProjectNameAsRootFolder);
        }

        public ProjectManagerInternal() : this(ProjectServerEnvironment.ProjectDataPath, true, false)
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

        public IEnumerator DownloadProjectLocally(Project project, bool incremental, Action<Exception> errorHandler, string temporaryDownloadFolder = null)
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
                    client = Player.CreateClient(project, ProjectServerEnvironment.UnityUser, ProjectServerEnvironment.Client);
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
                        message = $"A connection with the server {unityProject.Host.ServerName} could not be established. This server may be outside your local network (LAN) or may not accept external connections due to firewall policies.";
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
                        }, temporaryDownloadFolder, errorHandler);
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

        public IEnumerator DownloadSourceProjectLocally(Project project, string sourceId, SyncManifest oldManifest, SyncManifest newManifest, IPlayerClient client,
            Action<float> onProgress, string temporaryDownloadFolder = null, Action<Exception> errorHandler = null)
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

            onProgress?.Invoke(0.0f);

            var useDownloadFolder = !string.IsNullOrEmpty(temporaryDownloadFolder);

            var destinationFolder = m_LocalStorage.GetSourceProjectFolder(project, sourceId);
            var downloadFolder = useDownloadFolder ? Path.Combine(destinationFolder, temporaryDownloadFolder) : destinationFolder;

            int i = 0,
                total = entries.Count;
            foreach (ManifestEntry entry in entries)
            {
                Action<Exception> downloadErrorHandler = e => Debug.LogError($"Unable to download '{entry}' : {e.Message}");
                ISyncModel syncModel = null;
                try
                {
                    // TODO No need to deserialize then serialize back the SyncModel when all we need is to download the file locally
                    // TODO: Use async service call and yield until completion
                    syncModel = client.GetSyncModel(sourceId, entry.ModelPath, entry.Hash); // TODO var bitArray = client.GetSyncModelRaw(...) or client.Download(...)
                }
                catch (Exception ex)
                {
                    downloadErrorHandler(ex);
                }

                if (syncModel != null)
                {
                    // Replace model name with local model paths
                    syncModel.Name = entry.ModelPath;
                    SetReferencedSyncModelPath(syncModel, newManifest);

                    var fullPath = Path.Combine(downloadFolder, entry.ModelPath);
                    var directory = Path.GetDirectoryName(fullPath);
                    yield return RunFileIOOperation(() =>
                    {
                        Directory.CreateDirectory(directory);
                        PlayerFile.Save(syncModel, fullPath);
                    }, downloadErrorHandler);
                }

                onProgress?.Invoke(((float) ++i) / total);

                yield return null;
            }

            // Don't forget the manifest itself
            yield return RunFileIOOperation(
                () => newManifest.EditorSave(downloadFolder),
                (e) =>
                {
                    onError?.Invoke(e);
                    errorHandler?.Invoke(e);
                    throw e;
                });

            if (useDownloadFolder)
            {
                // Move all content to from temporary download folder to the final destination
                MoveDirectory(downloadFolder, destinationFolder);
            }
        }

        IEnumerator RunFileIOOperation(Action operation, Action<Exception> errorHandler)
        {
            var remainingAttempts = k_FileIOAttempts;
            do
            {
                try
                {
                    operation();
                    yield break;
                }
                catch (IOException e)
                {
                    if (--remainingAttempts <= 0)
                    {
                        var ex = new IOException($"File IO operation abandoned after {k_FileIOAttempts} attempts", e);
                        errorHandler?.Invoke(ex);
                        yield break;
                    }

                    Debug.LogWarning($"File IO operation failed, retrying in {k_FileIORetryDelaySeconds} seconds: {e.Message}");
                }

                yield return new WaitForSecondsRealtime(k_FileIORetryDelaySeconds);
            } while (remainingAttempts > 0);
        }

        static void SetReferencedSyncModelPath(ISyncModel syncModel, SyncManifest manifest)
        {
            switch (syncModel)
            {
                case SyncPrefab syncPrefab:
                    SetReferencedSyncModelPath(syncPrefab, manifest);
                    break;

                case SyncObject syncObject:
                    SetReferencedSyncModelPath(syncObject, manifest);
                    break;

                case SyncMaterial syncMaterial:
                    SetReferencedSyncModelPath(syncMaterial, manifest);
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

            var user = ProjectServerEnvironment.UnityUser;
            var projects = new List<Project>();

            if (user != null)
            {
                // Use ContinueWith to make sure the task doesn't throw
                var task = Task.Run(() => ProjectServerEnvironment.Client.ListProjects(user, m_LocalStorage)).ContinueWith(t => t);
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
            // TODO
        }

        // Directory.Move does not support partial moves / overrides.
        static void MoveDirectory(string source, string target)
        {
            var sourcePath = FormatPath(source);
            var targetPath = FormatPath(target);
            var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).GroupBy(Path.GetDirectoryName);

            foreach (var folder in files)
            {
                var targetFolder = FormatPath(folder.Key).Replace(sourcePath, targetPath);
                Directory.CreateDirectory(targetFolder);
                foreach (var file in folder)
                {
                    var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));

                    if (System.IO.File.Exists(targetFile))
                    {
                        System.IO.File.Delete(targetFile);
                    }

                    System.IO.File.Move(file, targetFile);
                }
            }
            Directory.Delete(source, true);
        }

        static string FormatPath(string path)
        {
            return path.Replace("\\", "/").TrimEnd('/', ' ');
        }
    }
}
