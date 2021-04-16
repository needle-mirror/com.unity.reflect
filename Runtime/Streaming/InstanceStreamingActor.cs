using System;
using System.Collections.Generic;
using Unity.Reflect.Actor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.Streaming
{
    public class StreamState
    {
        public bool IsCancelled;

        public void Cancel()
        {
            IsCancelled = true;
        }
    }

    [Actor]
    public class InstanceStreamingActor
    {
#pragma warning disable 649
        Settings m_Settings;
        NetOutput<GameObjectCreated> m_GameObjectCreatedOutput;
        RpcOutput<CreateGameObject> m_CreateGameObjectOutput;
#pragma warning restore 649

        List<Guid> m_QueuedInstances = new List<Guid>();
        Dictionary<Guid, LoadingState> m_LoadingInstances = new Dictionary<Guid, LoadingState>();
        Dictionary<Guid, GameObject> m_LoadedInstances = new Dictionary<Guid, GameObject>();
        HashSet<Guid> m_LoadingFailures = new HashSet<Guid>();
        
        int m_CurrentQueueIndex;

        [NetInput]
        void OnUpdateStreaming(NetContext<UpdateStreaming> ctx)
        {
            UpdateWaitingInstances(ctx);
            CancelStreamingOfHiddenInstances(ctx);
            SendBatch();
        }

        void UpdateWaitingInstances(NetContext<UpdateStreaming> ctx)
        {
            m_CurrentQueueIndex = 0;
            m_QueuedInstances.Clear();
            foreach (var entryId in ctx.Data.AddedInstances)
            {
                if (m_LoadingInstances.TryGetValue(entryId, out var state))
                    state.Retry = state.Stream.IsCancelled;
                else if (!m_LoadedInstances.ContainsKey(entryId) && !m_LoadingFailures.Contains(entryId))
                    m_QueuedInstances.Add(entryId);
            }
        }

        void CancelStreamingOfHiddenInstances(NetContext<UpdateStreaming> ctx)
        {
            foreach (var entryId in ctx.Data.RemovedInstances)
            {
                if (m_LoadingInstances.TryGetValue(entryId, out var state))
                    state.Stream.Cancel();
            }
        }

        void SendBatch()
        {
            var nbToPush = GetNbItemsAbleToSend();
            if (nbToPush < 0)
                return;

            var requests = new List<LoadRequest>();

            var i = 0;
            for(; i < nbToPush; ++i)
            {
                var entry = m_QueuedInstances[i + m_CurrentQueueIndex];
                var stream = new StreamState();

                requests.Add(new LoadRequest(entry, stream));
                m_LoadingInstances.Add(entry, new LoadingState { Stream = stream });
            }
            
            m_CurrentQueueIndex += nbToPush;

            BeginInstantiation(requests);
        }

        void BeginInstantiation(List<LoadRequest> requests)
        {
            foreach (var request in requests)
            {
                var rpc = m_CreateGameObjectOutput.Call(this, (object)null, request, new CreateGameObject(request.StreamState, request.InstanceId));
                rpc.Success<GameObject>((self, ctx, request, gameObject) =>
                {
                    if (gameObject == null && request.StreamState.IsCancelled)
                    {
                        var loadingState = m_LoadingInstances[request.InstanceId];
                        if (loadingState.Retry)
                        {
                            request.StreamState.IsCancelled = false;
                            BeginInstantiation(new List<LoadRequest>{ request });
                            return;
                        }
                    }

                    var hasFailed = gameObject == null && !request.StreamState.IsCancelled;

                    if (hasFailed)
                        Debug.LogError($"Instance ({request.InstanceId}) loading succeeded, but gameObject is null.");

                    CompleteInstanceRequest(request.InstanceId, gameObject, hasFailed);
                });

                rpc.Failure((self, ctx, request, ex) =>
                {
                    CompleteInstanceRequest(request.InstanceId, null, true);
                    Debug.LogError($"Instance ({request.InstanceId}) failed to load: {ex}");
                });
            }
        }

        void CompleteInstanceRequest(Guid instanceId, GameObject gameObject, bool hasFailed)
        {
            m_LoadingInstances.Remove(instanceId);

            if (hasFailed)
                m_LoadingFailures.Add(instanceId);
            else
                m_LoadedInstances.Add(instanceId, gameObject);
            
            if (m_LoadingInstances.Count < m_Settings.MaxBatchSize / 2)
                SendBatch();
            
            if (!hasFailed)
                m_GameObjectCreatedOutput.Send(new GameObjectCreated(instanceId, gameObject));
        }

        int GetNbItemsAbleToSend()
        {
            return Math.Min(m_Settings.MaxBatchSize - m_LoadingInstances.Count, m_QueuedInstances.Count - m_CurrentQueueIndex);
        }

        class LoadingState
        {
            public StreamState Stream;
            public bool Retry;
        }

        class LoadRequest
        {
            public Guid InstanceId;
            public StreamState StreamState;

            public LoadRequest(Guid instanceId, StreamState streamState)
            {
                InstanceId = instanceId;
                StreamState = streamState;
            }
        }

        [Serializable]
        public class Settings : ActorSettings
        {
            public int MaxBatchSize = 128;
            
            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
