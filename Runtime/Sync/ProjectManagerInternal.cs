using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using Unity.Reflect.Utils;
using File = Unity.Reflect.IO.File;

namespace UnityEngine.Reflect
{
    public class Project : ISerializationCallbackReceiver
    {
        [SerializeField]
        string m_ProjectId;

        [SerializeField]
        string m_ProjectName;

        [SerializeField]
        string m_ServerId;

        [SerializeField]
        string m_ServerName;

        [SerializeField]
        string[] m_EndpointAddresses;

        [NonSerialized]
        UnityProject m_UnityProject;

        public static Project Empty { get; } = new Project(new UnityProject(UnityProjectHost.LocalService, string.Empty, string.Empty));
        public string serverProjectId => $"{m_UnityProject.Host.ServerId}:{m_UnityProject.ProjectId}";
        public string projectId => m_UnityProject.ProjectId;
        public string name => m_UnityProject.Name;
        public string description => m_UnityProject.Host.ServerName;
        internal bool isAvailableOnline { get; set; }

        private Project()
        {
        }

        internal Project(UnityProject onlineUnityProject)
        {
            m_UnityProject = onlineUnityProject;
            isAvailableOnline = true;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            m_ProjectId = m_UnityProject.ProjectId;
            m_ProjectName = m_UnityProject.Name;
            m_ServerId = m_UnityProject.Host.ServerId;
            m_ServerName = m_UnityProject.Host.ServerName;
            m_EndpointAddresses = m_UnityProject.Host.EndpointAddresses.ToArray();
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            var host = m_ServerId == UnityProjectHost.LocalService.ServerId ?
                UnityProjectHost.LocalService : new UnityProjectHost(m_ServerId, m_ServerName, m_EndpointAddresses);
            m_UnityProject = new UnityProject(host, m_ProjectId, m_ProjectName);
            isAvailableOnline = false;
        }

        public static implicit operator UnityProject(Project project)
        {
            return project.m_UnityProject;
        }
    }

    abstract class ProjectManagerInternal : IProgressTask
    {
        class ProjectEntry
        {
            public Project project;
            public List<Action<Project>> listeners = new List<Action<Project>>();

            public ProjectEntry(Project project)
            {
                this.project = project;
            }

            public void NotifyChange()
            {
                foreach (var action in listeners)
                {
                    action.Invoke(project);
                }
            }
        }

        readonly Dictionary<string, ProjectEntry> m_Projects = new Dictionary<string, ProjectEntry>();

        readonly string m_UserProjectsPersistentPath;

        protected Dictionary<string, string[]> m_UserProjects = new Dictionary<string, string[]>();

        public event Action<Project> onProjectAdded;
        public event Action<Project> onProjectChanged;

        readonly HashSet<string> m_CurrentDownloadingProjectId = new HashSet<string>();

        readonly LocalStorage m_LocalStorage;

        public IEnumerable<Project> Projects => m_Projects.Values.Select(v => v.project);

        const string k_ProjectDataFileName = "index.json";

        const string k_ProjectDataFolderName = "ProjectData";

        const string k_Downloading = "Downloading";

        #region Abstract

        public abstract void StartDiscovery();

        public abstract void StopDiscovery();

        public abstract void OnEnable();

        public abstract void OnDisable();

        public abstract void Update();

        #endregion

        protected ProjectManagerInternal(string storageRoot, bool useServerFolder, bool useProjectNameAsRootFolder)
        {
            m_LocalStorage = new LocalStorage(storageRoot, useServerFolder, useProjectNameAsRootFolder);
            m_UserProjectsPersistentPath = Path.Combine(storageRoot, "userProjects.data");
            m_UserProjects = JsonSerializer.Load<Dictionary<string, string[]>>(m_UserProjectsPersistentPath);

            // Check for local projects
            var projects = GetLocalProjectsData(storageRoot);
            foreach (var project in projects)
            {
                UpdateProjectInternal(project, true);
            }
        }

        protected ProjectManagerInternal() : this($"{Application.persistentDataPath}/{k_ProjectDataFolderName}", true, false)
        {
        }

        protected void SaveUserProjectList()
        {
#if !UNITY_EDITOR
            JsonSerializer.Save(m_UserProjectsPersistentPath, m_UserProjects);
#endif
        }

        bool IsProjectAvailable(Project project) => m_Projects.ContainsKey(project.serverProjectId);

        public bool IsProjectAvailableOffline(Project project) => IsProjectAvailable(project) && m_LocalStorage.HasLocalData(project);

        public bool IsProjectAvailableOnline(Project project) => IsProjectAvailable(project) && project.isAvailableOnline;

        public bool IsProjectVisibleToUser(Project project)
        {
            if (ProjectServerEnvironment.UnityUser == null)
            {
                return false;
            }

            var userId = ProjectServerEnvironment.UnityUser.UserId;
            return m_UserProjects.ContainsKey(userId) && m_UserProjects[userId].Contains(project.serverProjectId);
        }

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

        public IEnumerator DownloadProjectLocally(string serverProjectId, bool incremental)
        {
            if (!m_Projects.TryGetValue(serverProjectId, out var entry))
            {
                Debug.LogError($"Cannot find project '{serverProjectId}'");
                yield break;
            }

            var project = entry.project;

            if (!IsProjectAvailableOnline(project))
            {
                Debug.LogError($"Cannot download project '{project.projectId}' from server.");
                yield break;
            }

            if (m_CurrentDownloadingProjectId.Contains(project.projectId))
            {
                Debug.LogError($"Already downloading project '{project.projectId}'");
                yield break;
            }

            m_CurrentDownloadingProjectId.Add(project.projectId);
            IPlayerClient client = null;
            try
            {
                // Create and connect gRPC channel to SyncServer
                client = Player.CreateClient(project, ProjectServerEnvironment.UnityUser);
                var responses = client.GetManifests();

                var manifestEntries = responses.ToArray();

                progressChanged?.Invoke(0.0f, k_Downloading);

                var total = manifestEntries.Length;
                var projectPercent = 1.0f / total;

                var localManifests = new Dictionary<string, SyncManifest>();

                if (incremental)
                {
                    var localSourceProjects = m_LocalStorage.LoadProjectManifests(project);

                    foreach (var sourceProject in localSourceProjects)
                    {
                        localManifests.Add(sourceProject.sourceId, sourceProject.manifest);
                    }
                }

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
                        });
                }

                SaveProjectDataLocally(project);

                onProjectChanged?.Invoke(project);

                progressChanged?.Invoke(1.0f, k_Downloading);
            }
            finally
            {
                m_CurrentDownloadingProjectId.Remove(project.projectId);
                client?.Dispose();
                taskCompleted?.Invoke();
            }
        }

        public IEnumerator DownloadSourceProjectLocally(Project project, string sourceId, SyncManifest oldManifest, SyncManifest newManifest, IPlayerClient client, Action<float> onProgress)
        {
            List<string> dstPaths;

            if (oldManifest == null)
            {
                var content = newManifest.Content;
                dstPaths = content.Values.Select(e => e.DstPath).ToList();
            }
            else
            {
                oldManifest.ComputeDiff(newManifest, out var modified, out var deleted);
                dstPaths = modified.Select(e => e.DstPath).ToList();

                // TODO Handle deleted models
            }

            onProgress?.Invoke(0.0f);

            var destinationFolder = m_LocalStorage.GetSourceProjectFolder(project, sourceId);

            var total = dstPaths.Count;

            for (int i = 0; i < total; ++i)
            {
                var dstPath = dstPaths[i];

                // TODO No need to deserialize then serialize back the SyncModel when all we need is to download the file locally
                var syncModel = client.GetSyncModel(sourceId, dstPath); // TODO var bitArray = client.GetSyncModelRaw(...) or client.Download(...)

                if (syncModel != null)
                {
                    var fullPath = Path.Combine(destinationFolder, dstPath);

                    var directory = Path.GetDirectoryName(fullPath);

                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    // Replace model name with local model paths
                    syncModel.Name = dstPath;
                    SetReferencedSyncModelPath(syncModel, newManifest);

                    File.Save(syncModel, fullPath);
                }
                else
                {
                    Debug.LogError("Unable to get '" + dstPath + "'...");
                }

                onProgress?.Invoke((i + 1.0f) / total);

                yield return null;
            }

            // Don't forget the manifest itself
            newManifest.Save(destinationFolder);
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
                path = entry.DstPath;
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

            if (m_CurrentDownloadingProjectId.Contains(project.projectId))
            {
                Debug.LogWarning($"Cannot delete currently downloading project '{project.projectId}'");
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

        void SaveProjectDataLocally(Project project)
        {
            var localProjectFolder = m_LocalStorage.GetProjectFolder(project);
            var projectDataFilePath = Path.Combine(localProjectFolder, k_ProjectDataFileName);
            var directory = Path.GetDirectoryName(projectDataFilePath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            System.IO.File.WriteAllText(projectDataFilePath, JsonUtility.ToJson(project));
        }

        static IEnumerable<Project> GetLocalProjectsData(string storageRoot)
        {
            if (!Directory.Exists(storageRoot))
                return Enumerable.Empty<Project>();

            var projectsDataPath = Directory.EnumerateFiles(storageRoot, k_ProjectDataFileName, SearchOption.AllDirectories);
            return projectsDataPath.Select(path => JsonUtility.FromJson<Project>(System.IO.File.ReadAllText(path)));
        }

        public void RegisterToProject(string serverProjectId, Action<Project> callback)
        {
            if (!m_Projects.TryGetValue(serverProjectId, out var entry))
            {
                m_Projects[serverProjectId] = entry = new ProjectEntry(Project.Empty);
            }

            entry.listeners.Add(callback);
        }

        protected void UpdateProjectInternal(Project project, bool canAddProject)
        {
            if (project == Project.Empty)
            {
                return;
            }

            if (m_Projects.TryGetValue(project.serverProjectId, out var entry))
            {
                entry.project = project;
                entry.NotifyChange();
                onProjectChanged?.Invoke(project);
            }
            else if (canAddProject)
            {
                m_Projects[project.serverProjectId] = new ProjectEntry(project);
                onProjectAdded?.Invoke(project);
            }
        }

        public void Cancel()
        {
            // TODO
        }

        public event Action<float, string> progressChanged;
        public event Action taskCompleted;
    }
}
