using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Services;
using Unity.Reflect.Utils;
using File = Unity.Reflect.IO.File;

namespace UnityEngine.Reflect.Services
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

    public class ProjectManagerInternal : IProgressTask
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
        
        Dictionary<string, ProjectEntry> m_Projects = new Dictionary<string, ProjectEntry>();

        public event Action<Project> onProjectAdded;
        public event Action<Project> onProjectChanged;
        public event Action<Project> onProjectRemoved;
        
        struct ProjectEvent
        {
            public enum Type
            {
                Added,
                Removed
            }

            public Type type;
            public ProjectInfo info;
        }
        
        Queue<ProjectEvent> m_PendingProjectEvents = new Queue<ProjectEvent>();

        HashSet<string> m_CurrentDownloadingProjectId = new HashSet<string>();
        
        LocalStorage m_LocalStorage;
        ProjectDiscovery m_ProjectDiscovery;
        
        public IEnumerable<Project> Projects => m_Projects.Values.Select(v => v.project);

        readonly string k_ProjectDataFileName = "index.xml";

        bool m_UseProjectNameAsRootFolder;

        public ProjectManagerInternal(string storageRoot, bool useProjectNameAsRootFolder)
        {
            m_UseProjectNameAsRootFolder = useProjectNameAsRootFolder;
            Init(storageRoot);
        }
        
        public ProjectManagerInternal()
        {
            Init(Application.persistentDataPath);
        }

        void Init(string storageRoot)
        {
            m_ProjectDiscovery = new ProjectDiscovery(false);

            m_LocalStorage = new LocalStorage(storageRoot);
            
            // Check for local projects
            var projects = GetLocalProjectsData(storageRoot);
            foreach (var project in projects)
            {
                AddOrUpdateProjectInternal(project);
            }
        }

        string ResolveProjectFolderName(Project project)
        {
            return m_UseProjectNameAsRootFolder ? FileUtils.SanitizeName(project.name) : project.projectId;
        }
        
        public bool IsProjectAvailableOffline(Project project)
        {
            return m_LocalStorage.HasLocalData(ResolveProjectFolderName(project));
        }

        public bool IsProjectAvailableOnline(Project project)
        {
            if (m_Projects.TryGetValue(project.serverProjectId, out var entry))
            {
                return entry.project.channel != null;
            }
            return false;
        }
        
        public string GetProjectFolder(Project project)
        {
            return m_LocalStorage.GetProjectFolder(ResolveProjectFolderName(project));
        }
        
        public string GetSessionFolder(Project project, string sessionId)
        {
            return m_LocalStorage.GetSessionFolder(ResolveProjectFolderName(project), sessionId);
        }
        
        public IEnumerable<ProjectSession> LoadProjectManifests(Project project)
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

            if(m_CurrentDownloadingProjectId.Contains(project.projectId))
            {
                Debug.LogError($"Already downloading project '{project.projectId}'");
                yield break;
            }
            m_CurrentDownloadingProjectId.Add(project.projectId);

            // Create and connect gRPC channel to SyncServer
            var client = Player.CreateClient(project.channel);
            client.Connect();

            var responses = client.GetManifests(project.projectId);
            
            var manifestEntries = responses.ToArray();

            const string kDownloading = "Downloading";
            
            progressChanged?.Invoke(0.0f, kDownloading);

            var total = manifestEntries.Length;
            var projectPercent = 1.0f / total;

            var localManifests = new Dictionary<string, SyncManifest>();

            if (incremental)
            {
                var localProjectSessions = m_LocalStorage.LoadProjectManifests(ResolveProjectFolderName(project));

                foreach (var projectSession in localProjectSessions)
                {
                    localManifests.Add(projectSession.sessionId, projectSession.manifest);
                }
            }

            for (int i = 0; i < total; ++i)
            {
                var manifestEntry = manifestEntries[i];

                localManifests.TryGetValue(manifestEntry.SessionId, out var oldManifest);
                
                yield return DownloadProjectSessionLocally(project, manifestEntry.SessionId,
                    oldManifest, manifestEntry.Manifest,
                    client,
                    p =>
                    {
                        var percent = (i + p) * projectPercent;
                        progressChanged?.Invoke(percent, kDownloading);
                    });
            }

            SaveProjectDataLocally(project);
            m_CurrentDownloadingProjectId.Remove(project.projectId);
            
            onProjectChanged?.Invoke(project);
            
            progressChanged?.Invoke(1.0f, kDownloading);
            
            taskCompleted?.Invoke();
        }
        
        public IEnumerator DownloadProjectSessionLocally(Project project, string sessionId, SyncManifest oldManifest, SyncManifest newManifest, IPlayerClient client, Action<float> onProgress)
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

            var destinationFolder = m_LocalStorage.GetSessionFolder(ResolveProjectFolderName(project), sessionId); 
            
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

                    File.Save(syncModel, fullPath);
                }
                else
                {
                    Debug.LogError("Unable to get '" + dstPath + "'...");
                }
                
                onProgress?.Invoke((i + 1.0f)/total);

                yield return null;
            }
            
            // Don't forget the manifest itself
            newManifest.Save(destinationFolder);
        }

        public IEnumerator DeleteProjectLocally(Project project)
        {
            var projectFolderPath = m_LocalStorage.GetProjectFolder(ResolveProjectFolderName(project)); 
            if(!Directory.Exists(projectFolderPath))
            {
                Debug.LogWarning($"Cannot delete locally stored project '{project.projectId}'");
                yield break;
            }
            
            if(m_CurrentDownloadingProjectId.Contains(project.projectId))
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
            
            if (m_Projects.TryGetValue(project.serverProjectId, out var entry))
            {
                RemoveProjectInternal(entry.project);
            }

            taskCompleted?.Invoke();
        }
        
        void SaveProjectDataLocally(Project project)
        {
            var localProjectFolder = m_LocalStorage.GetProjectFolder(ResolveProjectFolderName(project));
            var projectDataFilePath = Path.Combine(localProjectFolder, k_ProjectDataFileName);
            var directory = Path.GetDirectoryName(projectDataFilePath);

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            
            if(System.IO.File.Exists(projectDataFilePath))
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

        public void StartDiscovery()
        {
            m_ProjectDiscovery?.Start();
        }

        public void StopDiscovery()
        {
            m_ProjectDiscovery?.Stop();
        }

        public void OnEnable()
        {
            m_ProjectDiscovery.OnProjectAdded += ProjectDiscoveryProjectAdded;
            m_ProjectDiscovery.OnProjectRemoved += ProjectDiscoveryProjectRemoved;
        }

        public void OnDisable()
        {
            m_ProjectDiscovery.Stop();
            
            m_ProjectDiscovery.OnProjectAdded -= ProjectDiscoveryProjectAdded;
            m_ProjectDiscovery.OnProjectRemoved -= ProjectDiscoveryProjectRemoved;
        }

        public void OnDestroy()
        {
            m_ProjectDiscovery.Destroy();
        }

        public void Update()
        {
            lock (m_PendingProjectEvents)
            {
                while (m_PendingProjectEvents.Count > 0)
                {
                    var projectEvent = m_PendingProjectEvents.Dequeue();

                    var projectInfo = projectEvent.info;
                    
                    if (projectEvent.type == ProjectEvent.Type.Removed)
                    {
                        projectInfo.ServiceChannel = null;
                    }
                    
                    var project = CreateProjectFromProjectInfo(projectInfo);

                    if (projectEvent.type == ProjectEvent.Type.Added)
                    {
                        AddOrUpdateProjectInternal(project);
                    }
                    else
                    {
                        RemoveProjectInternal(project);
                    }
                }
            }
        }

        void AddOrUpdateProjectInternal(Project project)
        {
            if(project.IsValid())
            {
                if (!m_Projects.TryGetValue(project.serverProjectId, out var entry))
                {
                    m_Projects[project.serverProjectId] = entry = new ProjectEntry(project);    
                }
                else
                {
                    entry.project = project;
                }

                entry.NotifyChange();
            
                onProjectAdded?.Invoke(project);
            }
        }
        
        void RemoveProjectInternal(Project project)
        {
            if (m_Projects.TryGetValue(project.serverProjectId, out var entry))
            {
                entry.project = project;
                entry.NotifyChange();
            }
            
            onProjectRemoved?.Invoke(project);
        }

        void ProjectDiscoveryProjectAdded(ProjectInfo projectInfo)
        {
            lock (m_PendingProjectEvents)
            {
                m_PendingProjectEvents.Enqueue(new ProjectEvent { type = ProjectEvent.Type.Added, info = projectInfo });
            }
        }
        
        void ProjectDiscoveryProjectRemoved(ProjectInfo projectInfo)
        {
            lock (m_PendingProjectEvents)
            {
                m_PendingProjectEvents.Enqueue(new ProjectEvent { type = ProjectEvent.Type.Removed, info = projectInfo });
            }
        }

        static Project CreateProjectFromProjectInfo(ProjectInfo projectInfo)
        {
            return new Project(projectInfo.ServerProjectId, projectInfo.ProjectId, projectInfo.Name, projectInfo.ServerName, projectInfo.ServiceChannel);
        }

        public void Cancel()
        {
            // TODO
        }

        public event Action<float, string> progressChanged;
        public event Action taskCompleted;
    }
}
