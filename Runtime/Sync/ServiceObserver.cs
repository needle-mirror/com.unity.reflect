using System;
using System.Collections.Generic;
using System.Threading;
using Grpc.Core;
using Unity.Reflect;
using Unity.Reflect.Data;

namespace UnityEngine.Reflect.Services
{    
    class ServiceObserver
    {       
        public delegate void ManifestUpdated(string projectId, string sessionId, SyncManifest newManifest);
        public event ManifestUpdated OnManifestUpdated;

        public delegate void EventStreamUpdated(string serverProjectId, bool connected);
        public event EventStreamUpdated OnEventStreamUpdated;

        public IPlayerClient Client => m_Client;
        
        IPlayerClient m_Client;

        bool m_ObservingManifest = false;
        
        HashSet<string> m_UpdatedManifests = new HashSet<string>();
        string m_ObservedProjectId;
        string m_ObservedServerProjectId;

        Dictionary<TargetChannel, string> m_ChannelStreams = new Dictionary<TargetChannel, string>();
        
        public TargetChannel observingChannel => m_ServerChannel;
        TargetChannel m_ServerChannel = null;

        public void Connect(Project project, TargetChannel serverChannel)
        {
            m_ObservedProjectId = project.projectId;
            m_ObservedServerProjectId = project.serverProjectId;
            // If active channel is same as requestedChannel
            if(m_ServerChannel != null && m_ServerChannel.Target.Equals(serverChannel.Target))
            {
                Debug.Log($"Connect to same channel:{m_Client != null}:{m_ObservingManifest}");
                m_ServerChannel = serverChannel;
                if (!m_ObservingManifest)
                {
                    if(m_ChannelStreams.ContainsKey(m_ServerChannel))
                    {
                        m_ChannelStreams.Remove(m_ServerChannel);
                    }
                    if(m_Client != null)
                    {
                        try
                        {
                            m_Client = Player.CreateClient(serverChannel);
                            m_Client.OnStreamEvent += StreamEventNotify;
                            Debug.Log($"Reconnect client to previously connected channel");
                        }
                        catch(Exception ex)
                        {
                            Debug.Log($"Exception occured on recreating m_Client: {ex}");
                        }
                    }
                    ObserveClient();
                }
            }
            else
            {
                // Disconnect previous channel events
                if(m_ServerChannel != null)
                {
                    Debug.Log($"Disconnect from previous channel");
                    ReleaseClient();
                }
                // Create new client from channel and start observation 
                m_ServerChannel = serverChannel;
                if(m_ChannelStreams.ContainsKey(m_ServerChannel))
                {
                    m_ChannelStreams.Remove(m_ServerChannel);
                }
                m_Client = Player.CreateClient(serverChannel);
                m_Client.OnStreamEvent += StreamEventNotify;
                Debug.Log($"Connect to new channel");
                ObserveClient();
            }

            lock (m_UpdatedManifests)
            {
                m_UpdatedManifests.Clear();
            }
        }

        internal void ProcessPendingEvents()
        {
            lock (m_UpdatedManifests)
            {
                foreach (var sessionId in m_UpdatedManifests)
                {
                    var newManifest = m_Client.GetManifest(m_ObservedProjectId, sessionId);
                    OnManifestUpdated?.Invoke(m_ObservedProjectId, sessionId, newManifest);
                }
                m_UpdatedManifests.Clear();
            }
        }

        public void UpdateAllManifests()
        {
            var response = m_Client.GetManifests(m_ObservedProjectId);

            foreach (var responseManifest in response)
            {
                OnManifestUpdated?.Invoke(m_ObservedProjectId, responseManifest.SessionId, responseManifest.Manifest);    
            }
        }

        public void ClearPendingEvents()
        {
            lock (m_UpdatedManifests)
            {
                m_UpdatedManifests.Clear();
            }
        }

        void ObserveClient()
        {
            m_Client.OnManifestUpdate += OnManifestUpdate;
            m_Client.ObserveManifestUpdate();
            m_ObservingManifest = true;
            Debug.Log($"Starting Observing service.");
        }
       
        public void ReleaseClient()
        {
            if (m_ObservingManifest)
            {
                try
                {
                    Debug.Log($"Releasing observation of service.");
                    m_Client.ReleaseManifestUpdate();
                    m_Client.OnManifestUpdate -= OnManifestUpdate;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"An error occured releasing m_Client: {ex.Message}");
                }
                m_ObservingManifest = false;
            }
        }

        void OnManifestUpdate(string key)
        {
            lock (m_UpdatedManifests)
            {
                if (!m_UpdatedManifests.Contains(key))
                {
                    m_UpdatedManifests.Add(key);
                }
            }
        }

        void StreamEventNotify(StreamStatus status, string id)
        {
            Debug.Log($"ServiceObserver.StreamEventNotify on projectId '{m_ObservedProjectId}':{id}, {status}");
            var isConnected = status.Equals(StreamStatus.Connected);
            if(!m_ChannelStreams.ContainsKey(m_ServerChannel) && isConnected)
            {
                m_ChannelStreams.Add(m_ServerChannel, id);
                OnEventStreamUpdated?.Invoke(m_ObservedServerProjectId, isConnected);
            }
            else
            {
                if(m_ChannelStreams[m_ServerChannel].Equals(id) && !isConnected)
                {
                    m_ChannelStreams.Remove(m_ServerChannel);
                    OnEventStreamUpdated?.Invoke(m_ObservedServerProjectId, isConnected);
                }
            }
        }

    }
}
