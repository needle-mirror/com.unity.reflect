using System;
using System.Collections.Generic;
using System.Linq;
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

        readonly List<object> m_PendingEvents = new List<object>();
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
            var pendingEvents = PopPendingEvents();

            var connectionStatusEvents = pendingEvents.OfType<ConnectionStatus>().ToList();
            if (connectionStatusEvents.Any())
            {
                var isConnected = connectionStatusEvents.Last() == ConnectionStatus.Connected;
                Debug.Log($"ServiceObserver.StreamEventNotify on projectId '{m_ObservedProjectId}', isConnected: {isConnected}");

                OnEventStreamUpdated?.Invoke(isConnected);
                if (!isConnected)
                {
                    Disconnect();
                    return;
                }
            }

            var manifestEvents = pendingEvents.OfType<ManifestUpdatedEventArgs>().ToList();
            foreach (var sourceId in manifestEvents.Select(e => e.SourceId).Distinct())
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
