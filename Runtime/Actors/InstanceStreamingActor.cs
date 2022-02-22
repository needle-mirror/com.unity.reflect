using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Reflect.ActorFramework;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.Actors
{
    public class StreamState
    {
        public bool IsCancelled;

        public void Cancel()
        {
            IsCancelled = true;
        }
    }

    [Actor("6c68488b-03a7-4d63-ab43-be732273ff16")]
    public class InstanceStreamingActor
    {
#pragma warning disable 649
        Settings m_Settings;
        NetOutput<PreShutdown> m_PreShutdownOutput;
        NetOutput<CreateGameObjectLifecycle> m_CreateGameObjectLifecycleOutput;
        RpcOutput<CreateGameObject> m_CreateGameObjectOutput;
        EventOutput<StreamingError> m_StreamingErrorOutput;
#pragma warning restore 649

        List<DynamicGuid> m_QueuedInstances = new List<DynamicGuid>();
        Dictionary<DynamicGuid, LoadingState> m_LoadingInstances = new Dictionary<DynamicGuid, LoadingState>();
        HashSet<DynamicGuid> m_LoadedInstances = new HashSet<DynamicGuid>();
        HashSet<DynamicGuid> m_LoadingFailures = new HashSet<DynamicGuid>();
        HashSet<DynamicGuid> m_NotEnoughMemoryFailures = new HashSet<DynamicGuid>();
        TimeSpan m_LastNotEnoughMemoryRetry;
        
        int m_CurrentQueueIndex;
        bool m_IsPreShutdown;
        bool m_IsStopped;
        bool m_IsMemoryUsageTooHigh;
        int m_MaxNbLoadedGameObjects = int.MaxValue;
        PipeContext<CleanAfterCriticalMemory> m_Ctx;
        RpcContext<StopStreaming> m_StopStreamingCtx;

        [NetInput]
        void OnUpdateStreaming(NetContext<UpdateStreaming> ctx)
        {
            if (m_IsPreShutdown || m_IsStopped || m_IsMemoryUsageTooHigh)
                return;

            UpdateWaitingInstances(ctx.Data.VisibleInstances);
            CancelStreamingOfHiddenInstances(ctx.Data.HiddenInstancesSinceLastUpdate);
            SendBatch();
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
                m_LoadedInstances.Remove(go.Id);
                
            ctx.Continue();
        }

        [NetInput]
        void OnMemoryStateChanged(NetContext<MemoryStateChanged> ctx)
        {
            m_IsMemoryUsageTooHigh = ctx.Data.TotalAppMemory > ctx.Data.CriticalThreshold;
            
            if (m_IsMemoryUsageTooHigh)
                CancelAllLoadingInstances();
        }

        [PipeInput]
        void OnCleanAfterCriticalMemory(PipeContext<CleanAfterCriticalMemory> ctx)
        {
            if (m_LoadingInstances.Count == 0)
                ctx.Continue();
            else
                m_Ctx = ctx;
        }

        [EventInput]
        void OnMaxLoadedGameObjectsChanged(EventContext<MaxLoadedGameObjectsChanged> ctx)
        {
            m_MaxNbLoadedGameObjects = ctx.Data.MaxNbLoadedGameObjects;
        }

        [NetInput]
        void OnPreShutdown(NetContext<PreShutdown> _)
        {
            m_IsPreShutdown = true;
            Clear();
            OnLoadingInstancesChanged();
        }

        [RpcInput]
        void OnStopStreaming(RpcContext<StopStreaming> ctx)
        {
            m_IsStopped = true;
            m_StopStreamingCtx = ctx;

            Clear();
            OnLoadingInstancesChanged();
        }

        [RpcInput]
        void OnRestartStreaming(RpcContext<RestartStreaming> ctx)
        {
            m_IsStopped = false;
            ctx.SendSuccess(null);
        }

        void OnLoadingInstancesChanged()
        {
            if (m_IsPreShutdown && m_LoadingInstances.Count == 0)
                m_PreShutdownOutput.Send(new PreShutdown());

            if (m_IsStopped && m_LoadingInstances.Count == 0)
                m_StopStreamingCtx.SendSuccess(null);
        }

        void Clear()
        {
            m_CurrentQueueIndex = 0;
            m_QueuedInstances.Clear();
            foreach (var loading in m_LoadingInstances)
                loading.Value.Stream.Cancel();
        }

        void UpdateWaitingInstances(List<DynamicGuid> visibleInstances)
        {
            var curTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            if (curTime > m_LastNotEnoughMemoryRetry + TimeSpan.FromSeconds(5))
            {
                m_LastNotEnoughMemoryRetry = curTime;
                m_NotEnoughMemoryFailures.Clear();
            }

            m_CurrentQueueIndex = 0;
            m_QueuedInstances.Clear();
            foreach (var id in visibleInstances)
            {
                if (m_LoadingInstances.TryGetValue(id, out var state))
                    state.Retry = state.Stream.IsCancelled;
                else if (!m_LoadedInstances.Contains(id) && !m_LoadingFailures.Contains(id) && !m_NotEnoughMemoryFailures.Contains(id))
                    m_QueuedInstances.Add(id);
            }
        }

        void CancelStreamingOfHiddenInstances(List<DynamicGuid> hiddenInstancesSinceLastUpdate)
        {
            foreach (var instanceId in hiddenInstancesSinceLastUpdate)
            {
                if (m_LoadingInstances.TryGetValue(instanceId, out var state))
                    state.Stream.Cancel();
            }
        }

        void CancelAllLoadingInstances()
        {
            foreach (var loading in m_LoadingInstances)
            {
                loading.Value.DiscardRequest = true;
                loading.Value.Stream.Cancel();
            }
        }

        void SendBatch()
        {
            var nbToPush = GetNbItemsAbleToSend();
            if (nbToPush <= 0)
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
                    var loadingState = m_LoadingInstances[request.InstanceId];

                    if (gameObject == null && request.StreamState.IsCancelled &&
                        loadingState.Retry && !loadingState.DiscardRequest)
                    {
                        request.StreamState.IsCancelled = false;
                        BeginInstantiation(new List<LoadRequest>{ request });
                        return;
                    }

                    var hasFailed = gameObject == null && !request.StreamState.IsCancelled;

                    if (hasFailed)
                        Debug.LogError($"Instance ({request.InstanceId}) loading succeeded, but gameObject is null.");

                    CompleteInstanceRequest(request, gameObject, hasFailed, false);
                });

                rpc.Failure((self, ctx, request, ex) =>
                {
                    CompleteInstanceRequest(request, null, true, ex is InsufficientMemoryException);
                    m_StreamingErrorOutput.Broadcast(new StreamingError(request.InstanceId, ex));
                });
            }
        }

        void CompleteInstanceRequest(LoadRequest request, GameObject gameObject, bool hasFailed, bool isNotEnoughMemory)
        {
            var instanceId = request.InstanceId;
            var loadingState = m_LoadingInstances[instanceId];
            m_LoadingInstances.Remove(instanceId);

            if (isNotEnoughMemory)
                m_NotEnoughMemoryFailures.Add(instanceId);
            else if (hasFailed)
                m_LoadingFailures.Add(instanceId);
            else if (gameObject != null)
                m_LoadedInstances.Add(instanceId);

            if (m_LoadingInstances.Count == 0 && m_Ctx != null)
            {
                m_Ctx.Continue();
                m_Ctx = null;
            }
            
            if (m_LoadingInstances.Count < m_Settings.MaxBatchSize && !loadingState.DiscardRequest)
                SendBatch();

            if (!hasFailed && gameObject != null)
                m_CreateGameObjectLifecycleOutput.Send(new CreateGameObjectLifecycle(instanceId, gameObject));
            
            OnLoadingInstancesChanged();
        }

        int GetNbItemsAbleToSend()
        {
            var nbItems = Math.Min(m_Settings.MaxBatchSize - m_LoadingInstances.Count, m_QueuedInstances.Count - m_CurrentQueueIndex);
            return Math.Min(nbItems, m_MaxNbLoadedGameObjects - m_LoadedInstances.Count - m_LoadingInstances.Count);
        }

        class LoadingState
        {
            public StreamState Stream;
            public bool Retry;
            public bool DiscardRequest;
        }

        class LoadRequest
        {
            public DynamicGuid InstanceId;
            public StreamState StreamState;

            public LoadRequest(DynamicGuid instanceId, StreamState streamState)
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
