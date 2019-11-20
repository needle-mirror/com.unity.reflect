using System;
using System.Collections.Generic;
using Unity.Reflect;
using Unity.Reflect.Data;

namespace UnityEngine.Reflect
{
    class ServiceObserver
    {
        public delegate void ManifestUpdated(string projectId, string sessionId, SyncManifest newManifest);
        public event ManifestUpdated OnManifestUpdated;

        public delegate void EventStreamUpdated(bool connected);
        public event EventStreamUpdated OnEventStreamUpdated;

        public IPlayerClient Client { get; private set; }

        readonly HashSet<string> m_UpdatedManifests = new HashSet<string>();
        string m_ObservedProjectId;

        public void Connect(Project project)
        {
            Disconnect();
            m_ObservedProjectId = project.projectId;

            // Create new client from channel and start observation
            Debug.Log($"Connect to new channel");
            Client = Player.CreateClient(project, ProjectServerEnvironment.UnityUser, ProjectServerEnvironment.Client);            
            Client.ConnectionStatusChanged += ConnectionStatusChanged;
        }

        public void StartSync()
        {
            Client.ManifestUpdated += OnManifestUpdate;
            UpdateAllManifests();
        }

        public void StopSync()
        {
            Client.ManifestUpdated -= OnManifestUpdate;
            PopPendingEvents();
        }

        public void ProcessPendingEvents()
        {
            foreach (var sourceId in PopPendingEvents())
            {
                var newManifest = Client.GetManifest(sourceId);
                OnManifestUpdated?.Invoke(m_ObservedProjectId, sourceId, newManifest.Manifest);
            }
        }

        void UpdateAllManifests()
        {
            var response = Client.GetManifests();

            foreach (var responseManifest in response)
            {
                OnManifestUpdated?.Invoke(m_ObservedProjectId, responseManifest.SourceId, responseManifest.Manifest);
            }
        }

        public void Disconnect()
        {
            if (Client == null)
            {
                return;
            }

            StopSync();            
            Client.ConnectionStatusChanged -= ConnectionStatusChanged;
            try
            {
                Debug.Log($"Releasing observation of service.");
                Client.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogError($"An error occured releasing m_Client: {ex.Message}");
            }
            Client = null;
        }

        IEnumerable<string> PopPendingEvents()
        {
            lock (m_UpdatedManifests)
            {
                var pendingEvents = new HashSet<string>(m_UpdatedManifests);
                m_UpdatedManifests.Clear();
                return pendingEvents;
            }
        }

        void OnManifestUpdate(object sender, ManifestUpdatedEventArgs e)
        {
            lock (m_UpdatedManifests)
            {
                m_UpdatedManifests.Add(e.SourceId);
            }
        }

        void ConnectionStatusChanged(ConnectionStatus status)
        {
            Debug.Log($"ServiceObserver.StreamEventNotify on projectId '{m_ObservedProjectId}', {status}");
            var isConnected = status.Equals(ConnectionStatus.Connected);
            if (!isConnected)
            {
                Disconnect();
            }

            OnEventStreamUpdated?.Invoke(isConnected);
        }
    }
}
