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
using Unity.Reflect.Services;
using Unity.Reflect.Utils;
using File = Unity.Reflect.IO.File;

namespace UnityEngine.Reflect
{
    [Serializable]
    public class Project
    {
        [XmlIgnore]
        public static Project Empty = new Project(string.Empty, string.Empty, string.Empty, string.Empty, null);

        public string serverProjectId;
        public string projectId;
        public string name;
        public string description;
        [XmlIgnore]
        public TargetChannel channel;

        public Project()
        {
        }

        public Project(string serverProjectId, string projectId, string name, string description, TargetChannel channel)
        {
            this.serverProjectId = serverProjectId;
            this.projectId = projectId;
            this.name = name;
            this.description = description;
            this.channel = channel;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(serverProjectId);
        }
    }

    abstract class ProjectManagerInternal : IProgressTask
    {
        public class ProjectEntry
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

        public event Action<Project> onProjectAdded;
        public event Action<Project> onProjectChanged;

        readonly HashSet<string> m_CurrentDownloadingProjectId = new HashSet<string>();

        LocalStorage m_LocalStorage;

        public IEnumerable<Project> Projects => m_Projects.Values.Select(v => v.project);

        const string k_ProjectDataFileName = "index.xml";

        readonly bool m_UseProjectNameAsRootFolder;

        const string kDownloading = "Downloading";

        #region Abstract

        public abstract void StartDiscovery();

        public abstract void StopDiscovery();

        public abstract void OnEnable();

        public abstract void OnDisable();

        public abstract void Update();

        #endregion

        protected ProjectManagerInternal(string storageRoot, bool useProjectNameAsRootFolder)
        {
            m_UseProjectNameAsRootFolder = useProjectNameAsRootFolder;
            Init(storageRoot);
        }

        protected ProjectManagerInternal()
        {
            Init(Application.persistentDataPath);
        }

        void Init(string storageRoot)
        {
            m_LocalStorage = new LocalStorage(storageRoot);

            // Check for local projects
            var projects = GetLocalProjectsData(storageRoot);
            foreach (var project in projects)
            {
                UpdateProjectInternal(project, true);
            }
        }

        string ResolveProjectFolderName(Project project)
        {
            return m_UseProjectNameAsRootFolder ? FileUtils.SanitizeName(project.name) : project.projectId;
        }

        bool IsProjectAvailable(Project project) => m_Projects.ContainsKey(project.serverProjectId);

        public bool IsProjectAvailableOffline(Project project) => IsProjectAvailable(project) && m_LocalStorage.HasLocalData(ResolveProjectFolderName(project));

        public bool IsProjectAvailableOnline(Project project) => IsProjectAvailable(project) && project.channel != null;

        public string GetProjectFolder(Project project)
        {
            return m_LocalStorage.GetProjectFolder(ResolveProjectFolderName(project));
        }

        public string GetSourceProjectFolder(Project project, string sessionId)
        {
            return m_LocalStorage.GetSourceProjectFolder(ResolveProjectFolderName(project), sessionId);
        }

        public IEnumerable<SourceProject> LoadProjectManifests(Project project)
        {
            return m_LocalStorage.LoadProjectManifests(ResolveProjectFolderName(project));
        }

        public IEnumerator DownloadProjectLocally(string serverProjectId, bool incremental)
        {
            if (!m_Projects.TryGetValue(serverProjectId, out var entry))
            {
                Debug.LogError($"Cannot find project '{serverProjectId}'");
                yield break;
            }

            var project = entry.project;

            if (project.channel == null)
            {
                Debug.LogError($"Cannot download project '{project.projectId}' from server.");
                yield break;
            }

            if (m_CurrentDownloadingProjectId.Contains(project.projectId))
            {
                Debug.LogError($"Already downloading project '{project.projectId}'");
                yield break;
            }

            try
            {
                m_CurrentDownloadingProjectId.Add(project.projectId);

                // Create and connect gRPC channel to SyncServer
                var client = Player.CreateClient(project.channel);
                client.Connect();

                var responses = client.GetManifests(project.projectId);

                var manifestEntries = responses.ToArray();

                progressChanged?.Invoke(0.0f, kDownloading);

                var total = manifestEntries.Length;
                var projectPercent = 1.0f / total;

                var localManifests = new Dictionary<string, SyncManifest>();

                if (incremental)
                {
                    var localSourceProjects = m_LocalStorage.LoadProjectManifests(ResolveProjectFolderName(project));

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
                            progressChanged?.Invoke(percent, kDownloading);
                        });
                }

                SaveProjectDataLocally(project);

                onProjectChanged?.Invoke(project);

                progressChanged?.Invoke(1.0f, kDownloading);
            }
            finally
            {
                m_CurrentDownloadingProjectId.Remove(project.projectId);

                taskCompleted?.Invoke();
            }
        }

        public IEnumerator DownloadSourceProjectLocally(Project project, string sessionId, SyncManifest oldManifest, SyncManifest newManifest, IPlayerClient client, Action<float> onProgress)
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

            var destinationFolder = m_LocalStorage.GetSourceProjectFolder(ResolveProjectFolderName(project), sessionId);

            var total = dstPaths.Count;

            for (int i = 0; i < total; ++i)
            {
                var dstPath = dstPaths[i];

                // TODO No need to deserialize then serialize back the SyncModel when all we need is to download the file locally
                var syncModel = client.GetSyncModel(project.projectId, sessionId, dstPath); // TODO var bitArray = client.GetSyncModelRaw(...) or client.Download(...)

                if (syncModel != null)
                {
                    var fullPath = Path.Combine(destinationFolder, dstPath);

                    var directory = Path.GetDirectoryName(fullPath);

                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);

                    // Replace model names with local model paths
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

        public void SetReferencedSyncModelPath(ISyncModel syncModel, SyncManifest manifest)
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

        void SetReferencedSyncModelPath(SyncPrefab syncPrefab, SyncManifest manifest)
        {
            foreach (var instance in syncPrefab.Instances)
            {
                SetReferencedSyncModelPath(instance, manifest);
            }
        }

        void SetReferencedSyncModelPath(SyncObjectInstance instance, SyncManifest manifest)
        {
            instance.Object = GetSyncModelLocalPath<SyncObject>(instance.Object, manifest);
        }

        void SetReferencedSyncModelPath(SyncObject syncObject, SyncManifest manifest)
        {
            syncObject.Mesh = GetSyncModelLocalPath<SyncMesh>(syncObject.Mesh, manifest);
            for (var i = 0; i < syncObject.Materials.Count; ++i)
            {
                syncObject.Materials[i] = GetSyncModelLocalPath<SyncMaterial>(syncObject.Materials[i], manifest);
            }
            foreach (var child in syncObject.Children)
            {
                SetReferencedSyncModelPath(child, manifest);
            }
        }

        void SetReferencedSyncModelPath(SyncMaterial material, SyncManifest manifest)
        {
            SetReferencedSyncModelPath(material.AlbedoMap, manifest);
            SetReferencedSyncModelPath(material.AlphaMap, manifest);
            SetReferencedSyncModelPath(material.NormalMap, manifest);
            SetReferencedSyncModelPath(material.CutoutMap, manifest);
            SetReferencedSyncModelPath(material.EmissionMap, manifest);
            SetReferencedSyncModelPath(material.GlossinessMap, manifest);
            SetReferencedSyncModelPath(material.MetallicMap, manifest);
        }

        void SetReferencedSyncModelPath(SyncMap map, SyncManifest manifest)
        {
            if (map?.Texture == null)
                return;

            map.Texture = GetSyncModelLocalPath<SyncTexture>(map.Texture, manifest);
        }

        string GetSyncModelLocalPath<T>(string modelName, SyncManifest manifest) where T : ISyncModel
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
            var projectFolderPath = m_LocalStorage.GetProjectFolder(ResolveProjectFolderName(project));
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
            var localProjectFolder = m_LocalStorage.GetProjectFolder(ResolveProjectFolderName(project));
            var projectDataFilePath = Path.Combine(localProjectFolder, k_ProjectDataFileName);
            var directory = Path.GetDirectoryName(projectDataFilePath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (System.IO.File.Exists(projectDataFilePath))
            {
                System.IO.File.Delete(projectDataFilePath);
            }

            XmlSerializer serializer = new XmlSerializer(typeof(Project));
            using (FileStream fileStream = new FileStream(projectDataFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (StreamWriter streamWriter = new StreamWriter(fileStream))
            {
                serializer.Serialize(streamWriter, project);
            }
        }

        IEnumerable<Project> GetLocalProjectsData(string storageRoot)
        {
            var projects = new List<Project>();

            if (!Directory.Exists(storageRoot))
                return projects;

            var projectsDataPath = Directory.EnumerateFiles(storageRoot, k_ProjectDataFileName, SearchOption.AllDirectories);

            XmlSerializer serializer = new XmlSerializer(typeof(Project));
            foreach (var path in projectsDataPath)
            {
                try
                {
                    using (FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        projects.Add((Project)serializer.Deserialize(fileStream));
                    }
                }
                catch (Exception)
                {
                    Debug.LogWarning($"Could not read xml file '{path}'");
                }
            }

            return projects;
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
            if (!project.IsValid())
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
