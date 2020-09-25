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
    public class ProjectManager : MonoBehaviour, IProgressTask
    {
        ProjectManagerInternal m_ProjectManagerInternal;

        public event Action onProjectsRefreshBegin;
        public event Action onProjectsRefreshEnd;

        public event Action<Project> onProjectAdded;
        public event Action<Project> onProjectChanged;
        public event Action<Project> onProjectRemoved;

        public event Action<Exception> onError;

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

        public IEnumerator DownloadProjectLocally(Project project, bool incremental, Action<Exception> errorHandler)
        {
            yield return m_ProjectManagerInternal.DownloadProjectLocally(project, incremental, errorHandler);
        }

        public IEnumerator DownloadSourceProjectLocally(Project project, string sessionId, SyncManifest oldManifest, SyncManifest newManifest, IPlayerClient client, Action<float> onProgress)
        {
            yield return m_ProjectManagerInternal.DownloadSourceProjectLocally(project, sessionId, oldManifest, newManifest, client, onProgress, null);
        }

        public IEnumerator DeleteProjectLocally(Project project)
        {
            yield return m_ProjectManagerInternal.DeleteProjectLocally(project); // TODO Pass the onException
        }

        public void StartDiscovery()
        {
            if (m_RefreshProjectsCoroutine != null)
            {
                StopCoroutine(m_RefreshProjectsCoroutine);
            }
            m_RefreshProjectsCoroutine = StartCoroutine(m_ProjectManagerInternal.RefreshProjectListCoroutine());
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
            m_ProjectManagerInternal.onAuthenticationFailure += () => onAuthenticationFailure?.Invoke();

            m_ProjectManagerInternal.onProjectsRefreshBegin += () => onProjectsRefreshBegin?.Invoke();
            m_ProjectManagerInternal.onProjectsRefreshEnd += () => onProjectsRefreshEnd?.Invoke();

            m_ProjectManagerInternal.onProjectAdded += project => onProjectAdded?.Invoke(project);
            m_ProjectManagerInternal.onProjectChanged += project => onProjectChanged?.Invoke(project);
            m_ProjectManagerInternal.onProjectRemoved += project => onProjectRemoved?.Invoke(project);
            m_ProjectManagerInternal.onError += error => onError?.Invoke(error); // TODO Stop coroutines?

            m_ProjectManagerInternal.progressChanged += (f, s) => progressChanged?.Invoke(f, s);
            m_ProjectManagerInternal.taskCompleted += () => taskCompleted?.Invoke();
        }

        public void SetUnityUser(UnityUser unityUser = null)
        {
            Debug.Log($"ProjectManager.SetUnityUser: {unityUser != null}");
            ProjectServer.UnityUser = unityUser;
            StartDiscovery();
        }
    }
}
