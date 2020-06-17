using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect;
using Unity.Reflect.Data;

namespace UnityEngine.Reflect
{
    class ServiceObserver
    {        
        public delegate void ManifestUpdated(string projectId, IPlayerClient playerClient, bool allManifests, string[] sourceIds);        
        public event ManifestUpdated onManifestsUpdated;

        bool m_SyncStarted;
        public event Action onSyncEnabled;
        public event Action onSyncDisabled;
        public event Action onSyncStarted;
        public event Action onSyncStopped;

        IPlayerClient m_Client;

        readonly List<object> m_PendingEvents = new List<object>();        
        string m_ObservedProjectId;

        public ServiceObserver(IUpdateDelegate updateDelegate)
        {
            if (updateDelegate != null)
            {
                updateDelegate.onUpdate += Update;
            }
        }

        public void BindProject(Project project)
        {
            Disconnect();
            m_ObservedProjectId = project.projectId;
            
            if (project == Project.Empty)
            {
                onSyncDisabled?.Invoke();
                return;
            }            

            try
            {
                m_Client = Player.CreateClient(project, ProjectServerEnvironment.UnityUser, ProjectServerEnvironment.Client);
                m_Client.ConnectionStatusChanged += ConnectionStatusChanged;
                onSyncEnabled?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogError($"An error occured connecting to PlayerClient: {e}");
                
                // A limitation here is we can't recover a connection if the initial connection fails for now
                BindProject(Project.Empty);
            }
        }

        public void StartSync()
        {
            m_SyncStarted = true;
            m_Client.ManifestUpdated += OnManifestUpdate;
            onSyncStarted?.Invoke();
        }

        public void StopSync()
        {
            m_SyncStarted = false;
            m_Client.ManifestUpdated -= OnManifestUpdate;
            PopPendingEvents(); 
            onSyncStopped?.Invoke();
        }

        void Update(float unscaledDeltaTime)
        {
            ProcessPendingEvents();
        }

        public void ProcessPendingEvents()
        {
            var pendingEvents = PopPendingEvents();
            var updatedManifests = new List<SyncManifest>();            
            
            var connectionStatusEvents = pendingEvents.OfType<ConnectionStatus>().ToList();
            if (connectionStatusEvents.Any())
            {
                if (connectionStatusEvents.Last() != ConnectionStatus.Connected)
                {
                    Debug.Log($"ServiceObserver disconnected from stream on projectId '{m_ObservedProjectId}'");
                    onSyncDisabled?.Invoke();
                    return;
                }
                
                Debug.Log($"ServiceObserver connected to stream on projectId '{m_ObservedProjectId}'");
                onSyncEnabled?.Invoke();
                
                if (m_SyncStarted)
                {
                    onManifestsUpdated?.Invoke(m_ObservedProjectId, m_Client, true, Array.Empty<string>());
                }
            }
            else if (m_SyncStarted)
            {
                var manifestEvents = pendingEvents.OfType<ManifestUpdatedEventArgs>().ToList();
                if (manifestEvents.Any())
                {
                    var sourceIds = manifestEvents.Select(e => e.SourceId).Distinct().ToArray();
                    onManifestsUpdated?.Invoke(m_ObservedProjectId, m_Client, false, sourceIds);
                }                
            }
        }
        
        void Disconnect()
        {
            if (m_Client == null)
            {
                return;
            }

            StopSync();
            m_Client.ConnectionStatusChanged -= ConnectionStatusChanged;

            try
            {
                Debug.Log($"Releasing observation of service.");
                m_Client.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occured releasing m_Client: {ex.Message}");
            }
            m_Client = null;
        }

        object[] PopPendingEvents()
        {
            lock (m_PendingEvents)
            {
                var pendingEvents = m_PendingEvents.ToArray();
                m_PendingEvents.Clear();
                return pendingEvents;
            }
        }

        void OnManifestUpdate(object sender, ManifestUpdatedEventArgs e)
        {
            AddEvent(e);
        }

        void ConnectionStatusChanged(ConnectionStatus status)
        {
            AddEvent(status);
        }

        void AddEvent(object e)
        {
            lock (m_PendingEvents)
            {
                m_PendingEvents.Add(e);
            }
        }
    }
}
