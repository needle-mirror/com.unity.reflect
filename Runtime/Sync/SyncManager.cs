// Prevent warnings for field not assigned to
#pragma warning disable 0649

using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Reflect.Data;
using UnityEngine;

namespace UnityEngine.Reflect.Services
{   
    public sealed class SyncManager : MonoBehaviour, ISyncTask, IProgressTask
    {
        [SerializeField]
        ProjectManager m_ProjectManager;

        [SerializeField]
        SyncTopMenu m_SyncMenu;

        [SerializeField]
        ProgressBar m_ProgressBar;

        [SerializeField]
        Transform m_SyncRoot;
        
        Transform m_SyncInstancesRoot;
        
        public IReadOnlyDictionary<string, SyncInstance> syncInstances => m_SyncInstances;

        Dictionary<string, SyncInstance> m_SyncInstances = new Dictionary<string, SyncInstance>();

        ServiceObserver m_Observer;

        bool m_ForceUpdate;

        bool m_SyncEnabled;
        
        Project m_SelectedProject = Project.Empty;

        public Project selectedProject => m_SelectedProject;

        static void OnLog(Unity.Reflect.Utils.Logger.Level level, string msg)
        {
            switch (level)
            {
                case Unity.Reflect.Utils.Logger.Level.Debug:
                case Unity.Reflect.Utils.Logger.Level.Info:
                    Debug.Log(msg);
                    break;
                
                case Unity.Reflect.Utils.Logger.Level.Warn:
                    Debug.LogWarning(msg);
                    break;
                
                case Unity.Reflect.Utils.Logger.Level.Error:
                case Unity.Reflect.Utils.Logger.Level.Fatal:
                    Debug.LogError(msg);
                    break;
                
                default:
                    Debug.Log(msg);
                    break;
            }
        }

        void OnDestroy()
        {
            Debug.Log("Releasing observer...");
            m_Observer?.ReleaseClient();
        }

        public IEnumerator Open(Project project)
        {
            if (m_SelectedProject.IsValid() && m_SelectedProject.serverProjectId.Equals(project.serverProjectId))
            {
                Debug.LogWarning($"Project is already opened '{m_SelectedProject.name}'");
                yield break;
            }
                        
            m_SelectedProject = project;
            
            m_ProjectManager.RegisterToProject(project.serverProjectId, OnProjectUpdated);
            
            m_ProgressBar.Register(this);

            ResetSyncRoot();

            const string kOpening = "Opening";
            
            progressChanged?.Invoke(0.0f, kOpening);
                        
            var sessions = m_ProjectManager.LoadProjectManifests(project);

            foreach (var session in sessions)
            {
                m_SyncInstances.TryGetValue(session.sessionId, out var syncInstance);
        
                if (syncInstance == null)
                {
                    var folder = m_ProjectManager.GetSessionFolder(project, session.sessionId);
        
                    m_SyncInstances[session.sessionId] = syncInstance = new SyncInstance(m_SyncInstancesRoot, folder);
                    
                    yield return syncInstance.ApplyModifications(session.manifest, progressChanged);
                }
            }
            
            m_SyncMenu.Register(this);

            if (m_SelectedProject.channel != null)
            {
                onSyncEnabled?.Invoke();

                yield return StartSyncInternal();
            }
            
            taskCompleted?.Invoke();
                        
            m_ProgressBar.UnRegister(this);
            
            RecenterSyncRoot();

            onProjectOpened?.Invoke();
        }

        void RecenterSyncRoot()
        {
            var renderers = m_SyncInstancesRoot.GetComponentsInChildren<Renderer>();
    
            var bounds = new Bounds();
            for (var i = 0; i < renderers.Length; ++i)
            {
                var b = renderers[i].bounds;
    
                if (i == 0)
                {
                    bounds = b;
                }
                else
                {
                    bounds.Encapsulate(b);
                }
            }
    
            var center = bounds.center - bounds.size.y * 0.5f * Vector3.up; // Middle-Bottom
    
            var offset = m_SyncInstancesRoot.position - center;
    
            m_SyncInstancesRoot.position = offset;
            m_SyncRoot.position = -offset;
        }

        void ResetSyncRoot()
        {
            foreach (Transform child in m_SyncRoot)
            {
                Destroy(child.gameObject);
            }
            
            m_SyncRoot.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            m_SyncRoot.localScale = Vector3.one;
            
            m_SyncInstancesRoot = new GameObject("Instances").transform;
            m_SyncInstancesRoot.parent = m_SyncRoot;
        }

        public bool IsProjectOpened(string serverProjectId)
        {
            return m_SelectedProject.serverProjectId.Equals(serverProjectId);
        }

        public void Close()
        {
            if (m_SelectedProject.IsValid())
            {            
                ResetSyncRoot();
                
                OnSyncStop();
                m_SyncInstances.Clear();
            
                m_SelectedProject = Project.Empty;
            
                onSyncDisabled?.Invoke();

                m_SyncMenu.UnRegister(this);

                onProjectClosed?.Invoke();
            }
        }

        public void OnSyncStart()
        {
            m_SyncEnabled = true;
            m_ForceUpdate = true;
        }
        
        public void OnSyncStop()
        {  
            m_SyncEnabled = false;
        }
        
        IEnumerator StartSyncInternal()
        {            
            if (m_SelectedProject.channel == null)
            {
                Debug.LogError($"Cannot start Sync on project {m_SelectedProject.name}. Not connected.");
                yield break;
            }

            if (m_SelectedProject.channel != null)
            {
                m_ForceUpdate = true;
            }
        }
        
        bool IsSyncing()
        {
            return m_SyncEnabled && m_Observer != null;
        }

        void OnManifestUpdated(string projectId, string sessionId, SyncManifest manifest)
        {
            StartCoroutine(ManifestUpdatedInternal(projectId, sessionId, manifest));
        }

        void OnEventStreamUpdated(string serverProjectId, bool connected)
        {
            Debug.Log($"OnEventStreamUpdated:{serverProjectId}");
            if(m_SelectedProject.IsValid() && m_SelectedProject.serverProjectId.Equals(serverProjectId) && m_SelectedProject.channel != null)
            {
                if(!connected)
                {
                    Debug.Log("Project lost event stream. Disabling sync and Releasing observer...");
                    m_SelectedProject.channel = null;
                    DisableSync();
                }
            }
        }

        void OnProjectUpdated(Project project)
        {
            Debug.Log($"OnProjectUpdated '{project.serverProjectId}', Channel exists: {project.channel != null}");
            if(m_SelectedProject.IsValid() && IsProjectOpened(project.serverProjectId) && m_SelectedProject.channel != null && project.channel == null)
            {
                m_SelectedProject = project;
                Debug.Log("Project lost connection. Disabling sync and Releasing observer...");
                DisableSync();
                m_Observer?.ReleaseClient();
            }
            else
            {
                m_SelectedProject = project;
                if (m_SelectedProject.channel != null)
                {
                    Debug.Log($"Project '{m_SelectedProject.name}' Connected.");
                    EnableSync();
                }
                else
                {
                    Debug.Log($"Project '{m_SelectedProject.name}' Disconnected.");
                    DisableSync();
                }
            }
        }

        void EnableSync()
        {
            if (IsSyncing())
            {
                m_ForceUpdate = true;
            }  
            onSyncEnabled?.Invoke();
        }

        void DisableSync()
        {
            if (IsSyncing())
            {
                OnSyncStop();
            }
            onSyncDisabled?.Invoke();
        }

        IEnumerator ManifestUpdatedInternal(string projectId, string sessionId, SyncManifest manifest)
        {
            if(!m_SelectedProject.projectId.Equals(projectId))
            {
                yield return null;
            }
            else
            {
                onSyncUpdateBegin?.Invoke();
            
                m_SyncInstances.TryGetValue(sessionId, out var syncInstance);
            
                if (syncInstance == null)
                {
                      var folder = m_ProjectManager.GetSessionFolder(m_SelectedProject, sessionId);
                      m_SyncInstances[sessionId] = syncInstance = new SyncInstance(m_SyncInstancesRoot, folder);
                }

                yield return m_ProjectManager.DownloadProjectSessionLocally(m_SelectedProject, sessionId, syncInstance.Manifest, manifest, m_Observer.Client, null);
            
                yield return syncInstance.ApplyModifications(manifest, null);
            
                onSyncUpdateEnd?.Invoke();
            }
        }

        void Awake()
        {
            Unity.Reflect.Utils.Logger.OnLog += OnLog;

            if (m_SyncRoot == null)
            {
                Debug.LogWarning("SyncRoot is null");
            }
        }

        // iOS/Android specific MonoBehaviour callback 
        // when app is either sent to background or revived from background
        void OnApplicationPause(bool isPaused)
        {
            Debug.Log($"OnApplicationPause: {isPaused}");
            if(m_SelectedProject.IsValid() && m_SelectedProject.channel != null)
            {
                if(isPaused)
                {
                    DisableSync();
                    m_Observer?.ReleaseClient();
                }
                else
                {
                    EnableSync();
                }
            }
        }

        void Update()
        {
            if (m_ForceUpdate)
            {
                if (m_SelectedProject.channel != null)
                {
                    if(m_Observer == null)
                    {
                        Debug.Log("First time creating ServiceObserver");
                        m_Observer = new ServiceObserver();
                        m_Observer.OnManifestUpdated += OnManifestUpdated;
                        m_Observer.OnEventStreamUpdated += OnEventStreamUpdated;
                    }
                    m_Observer.Connect(m_SelectedProject, m_SelectedProject.channel);
                    // Apply any modifications right Away
                    m_Observer.UpdateAllManifests();
                    m_Observer.ClearPendingEvents();
                }
                m_ForceUpdate = false;
            }
            else
            {
                if (IsSyncing())
                {
                    m_Observer.ProcessPendingEvents();
                }
            }
        }

        public event Action onSyncEnabled;
        public event Action onSyncDisabled;
        public event Action onSyncUpdateBegin;
        public event Action onSyncUpdateEnd;

        public event Action onProjectOpened;
        public event Action onProjectClosed;

        public void Cancel()
        {
            // TODO
        }

        public event Action<float, string> progressChanged;
        public event Action taskCompleted;
    }
}