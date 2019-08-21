using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;

namespace UnityEngine.Reflect.Services
{
    public class ProjectManager : MonoBehaviour, IProgressTask
    {
        ProjectManagerInternal m_ProjectManagerInternal;

        public event Action<Project> onProjectAdded;
        public event Action<Project> onProjectChanged;
        public event Action<Project> onProjectRemoved;
        
        public void Cancel()
        {
            // TODO
        }

        public event Action<float, string> progressChanged;
        public event Action taskCompleted;

        public IEnumerable<Project> Projects => m_ProjectManagerInternal != null ? m_ProjectManagerInternal.Projects : new Project[] { };

        public bool IsProjectAvailableOffline(Project project)
        {
            return m_ProjectManagerInternal.IsProjectAvailableOffline(project);
        }

        public bool IsProjectAvailableOnline(Project project)
        {
            return m_ProjectManagerInternal.IsProjectAvailableOnline(project);
        }
        
        public string GetSessionFolder(Project project, string sessionId)
        {
            return m_ProjectManagerInternal.GetSessionFolder(project, sessionId);
        }
        
        
        public IEnumerable<ProjectSession> LoadProjectManifests(Project project)
        {
            return m_ProjectManagerInternal.LoadProjectManifests(project);
        }
        
        public IEnumerator DownloadProjectLocally(string serverProjectId, bool incremental)
        {
            yield return m_ProjectManagerInternal.DownloadProjectLocally(serverProjectId, incremental);
        }
        
        public IEnumerator DownloadProjectSessionLocally(Project project, string sessionId, SyncManifest oldManifest, SyncManifest newManifest, IPlayerClient client, Action<float> onProgress)
        {
            yield return m_ProjectManagerInternal.DownloadProjectSessionLocally(project, sessionId, oldManifest, newManifest, client, onProgress);
        }

        public IEnumerator DeleteProjectLocally(Project project)
        {
            yield return m_ProjectManagerInternal.DeleteProjectLocally(project);
        }

        public void RegisterToProject(string serverProjectId, Action<Project> callback)
        {
            m_ProjectManagerInternal.RegisterToProject(serverProjectId, callback);
        }

        public void StartDiscovery()
        {
            m_ProjectManagerInternal.StartDiscovery();
        }

        public void StopDiscovery()
        {
            m_ProjectManagerInternal.StopDiscovery();
        }

        void Awake()
        {
            InitProjectManagerInternal();
        }

        void InitProjectManagerInternal()
        {
            if (m_ProjectManagerInternal != null)
                return;
            
            m_ProjectManagerInternal = new ProjectManagerInternal();
            
            m_ProjectManagerInternal.onProjectAdded += project => onProjectAdded?.Invoke(project);
            m_ProjectManagerInternal.onProjectChanged += project => onProjectChanged?.Invoke(project);
            m_ProjectManagerInternal.onProjectRemoved += project => onProjectRemoved?.Invoke(project);
            
            m_ProjectManagerInternal.progressChanged += (f, s) => progressChanged?.Invoke(f, s);
            m_ProjectManagerInternal.taskCompleted += () => taskCompleted?.Invoke();
        }

        void OnEnable()
        {
            m_ProjectManagerInternal.OnEnable();
        }
        
        void OnDisable()
        {
            m_ProjectManagerInternal.OnDisable();
        }

        void Update()
        {
            m_ProjectManagerInternal.Update();
        }
    }
}
