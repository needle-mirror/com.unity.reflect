using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using UnityEngine.Events;

[assembly: InternalsVisibleTo("Unity.Reflect.Editor")]
namespace UnityEngine.Reflect
{
    class ProjectManager : MonoBehaviour, IProgressTask
    {
        ProjectManagerWithProjectServerInternal m_ProjectManagerInternal;

        public event Action<Project> onProjectAdded;
        public event Action<Project> onProjectChanged;

        string m_ProjectServerAccessToken = string.Empty;

        public UnityEvent onAuthenticationFailure;

        Coroutine m_RefreshProjectsCoroutine;

        public void Cancel()
        {
            // TODO
        }

        public event Action<float, string> progressChanged;
        public event Action taskCompleted;

        public IEnumerable<Project> Projects => m_ProjectManagerInternal?.Projects ?? Enumerable.Empty<Project>();

        public bool IsProjectAvailableOffline(Project project)
        {
            return m_ProjectManagerInternal.IsProjectAvailableOffline(project);
        }

        public bool IsProjectAvailableOnline(Project project)
        {
            return m_ProjectManagerInternal.IsProjectAvailableOnline(project);
        }

        public string GetSourceProjectFolder(Project project, string sessionId)
        {
            return m_ProjectManagerInternal.GetSourceProjectFolder(project, sessionId);
        }

        public IEnumerable<SourceProject> LoadProjectManifests(Project project)
        {
            return m_ProjectManagerInternal.LoadProjectManifests(project);
        }

        public IEnumerator DownloadProjectLocally(string serverProjectId, bool incremental)
        {
            yield return m_ProjectManagerInternal.DownloadProjectLocally(serverProjectId, incremental);
        }

        public IEnumerator DownloadSourceProjectLocally(Project project, string sessionId, SyncManifest oldManifest, SyncManifest newManifest, IPlayerClient client, Action<float> onProgress)
        {
            yield return m_ProjectManagerInternal.DownloadSourceProjectLocally(project, sessionId, oldManifest, newManifest, client, onProgress);
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
            if (m_RefreshProjectsCoroutine != null)
            {
                StopCoroutine(m_RefreshProjectsCoroutine);
            }
            m_RefreshProjectsCoroutine = StartCoroutine(m_ProjectManagerInternal.RefreshProjectListCoroutine(m_ProjectServerAccessToken));
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

            m_ProjectManagerInternal = new ProjectManagerWithProjectServerInternal();
            m_ProjectManagerInternal.onAuthenticationFailure += () => onAuthenticationFailure?.Invoke();

            m_ProjectManagerInternal.onProjectAdded += project => onProjectAdded?.Invoke(project);
            m_ProjectManagerInternal.onProjectChanged += project => onProjectChanged?.Invoke(project);

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

        public void SetAccessToken(string token)
        {
            Debug.Log($"ProjectManager now using token:'{token}'.");
            m_ProjectServerAccessToken = token;
        }
    }
}
