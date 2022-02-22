using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    [Serializable]
    public class SyncObjectInstanceProviderNode : ReflectNode<SyncObjectInstanceProvider>
    {
        public StreamAssetInput input = new StreamAssetInput();
        public StreamInstanceOutput output = new StreamInstanceOutput();
        
        protected override SyncObjectInstanceProvider Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var node = new SyncObjectInstanceProvider(hook.Services.EventHub, provider, output);

            input.streamBegin = node.OnStreamInstanceBegin;
            input.streamEvent = node.OnStreamInstanceEvent;
            input.streamEnd = node.OnStreamInstanceEnd;
            
            node.Run();
            
            return node;
        }
    }

    public class SyncObjectInstanceProvider : ReflectTaskNodeProcessor
    {
        struct DownloadSyncModelResult
        {
            public readonly StreamAsset streamAsset;
            public readonly ISyncModel streamData;
            public Exception exception;

            public DownloadSyncModelResult(StreamAsset streamAsset, ISyncModel streamData, Exception exception)
            {
                this.streamAsset = streamAsset;
                this.streamData = streamData;
                this.exception = exception;
            }
        }

        EventHub m_Hub;

        AsyncAutoResetEvent m_DownloadRequestEvent = new AsyncAutoResetEvent();

        readonly ConcurrentQueue<StreamAsset> m_DownloadRequests;
        readonly ConcurrentQueue<DownloadSyncModelResult> m_DownloadSyncModelResults;

        readonly Dictionary<StreamKey, HashSet<StreamAsset>> m_Instances;
        readonly Dictionary<StreamKey, StreamInstance> m_Cache;

        readonly HashSet<StreamKey> m_DirtySyncObject;

        readonly ISyncModelProvider m_Client;

        readonly DataOutput<StreamInstance> m_InstanceDataOutput;

#if UNITY_ANDROID && !UNITY_EDITOR
        static readonly int k_MaxTaskSize = 5;
#else
        static readonly int k_MaxTaskSize = 10;
#endif

        public SyncObjectInstanceProvider(EventHub hub, ISyncModelProvider client, DataOutput<StreamInstance> instanceDataOutput)
        {
            m_Hub = hub;
            m_Client = client;

            m_InstanceDataOutput = instanceDataOutput;
            
            m_DownloadRequests = new ConcurrentQueue<StreamAsset>();
            m_DownloadSyncModelResults = new ConcurrentQueue<DownloadSyncModelResult>();

            m_Cache = new Dictionary<StreamKey, StreamInstance>();
            m_DirtySyncObject = new HashSet<StreamKey>();
            m_Instances = new Dictionary<StreamKey, HashSet<StreamAsset>>();
        }

        protected enum State
        {
            Idle,
            Processing,
            WaitingToFinish
        }

        protected State m_State = State.Idle;
        
        public void OnStreamInstanceBegin()
        {
            m_State = State.Processing;
            m_InstanceDataOutput.SendBegin();
        }
        
        public void OnStreamInstanceEvent(SyncedData<StreamAsset> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                if (!stream.key.key.IsRootAsset)
                    return;
                EnqueueDownloadRequest(stream.data);
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                if (stream.key.key.IsRootAsset)
                {
                    EnqueueDownloadRequest(stream.data);
                }
                else if (PersistentKey.IsKeyFor<SyncObject>(stream.key.key))
                {
                    var instances = m_Instances[stream.key];

                    foreach (var instance in instances)
                    {
                        m_DirtySyncObject.Add(instance.key);
                        EnqueueDownloadRequest(instance);
                    }
                }
            }
            else if (streamEvent == StreamEvent.Removed)
            {

                if (!stream.key.key.IsRootAsset)
                    return;

                if (PersistentKey.IsKeyFor<SyncObjectInstance>(stream.key.key))
                {
                    var key = stream.key;
                    if (m_Cache.TryGetValue(key, out var value))
                    {
                        m_Cache.Remove(key);

                        // Removing the deleted instance from m_Instances
                        var persistentKey = PersistentKey.GetKey<SyncObject>(value.instance.ObjectId);
                        var objectKey = new StreamKey(key.source, persistentKey);
                        var instances = m_Instances[objectKey];
                        instances.Remove(stream.data);

                        m_InstanceDataOutput.SendStreamRemoved(new SyncedData<StreamInstance>(key, value));
                    }
                }
                else if (PersistentKey.IsKeyFor<SyncTransformOverride>(stream.key.key)) 
                {
                    DataSourceProvider<TransformOverride>.Remove(stream.key);
                }
                else if (PersistentKey.IsKeyFor<SyncProjectTask>(stream.key.key)) 
                {
                    DataSourceProvider<ProjectTask>.Remove(stream.key);
                }
                else if (PersistentKey.IsKeyFor<SyncMarker>(stream.key.key)) 
                {
                    DataSourceProvider<SyncMarker>.Remove(stream.key);
                }
            }
        }

        public void OnStreamInstanceEnd()
        {
            m_State = State.WaitingToFinish;
        }
        
        protected override Task RunInternal(CancellationToken token)
        {
            return DownloadTask(token);
        }

        protected override void UpdateInternal(float unscaledDeltaTime)
        {

            while (m_DownloadSyncModelResults.TryDequeue(out var result))
            {
                if (result.exception != null)
                {
                    m_Hub.Broadcast(new StreamingErrorEvent(result.streamAsset.key, result.streamAsset.boundingBox, result.exception));
                    continue;
                }
                
                if (PersistentKey.IsKeyFor<SyncObjectInstance>(result.streamAsset.key.key))
                {
                    var streamObjectInstance = (SyncObjectInstance)result.streamData;

                    var streamInstance = new StreamInstance(result.streamAsset.key, streamObjectInstance, result.streamAsset.boundingBox);

                    var key = result.streamAsset.key;

                    if (m_Cache.TryGetValue(key, out var previousStreamInstance))
                    {
                        if (previousStreamInstance.instance.ObjectId != streamObjectInstance.ObjectId)
                        {
                            m_InstanceDataOutput.SendStreamRemoved(new SyncedData<StreamInstance>(key, previousStreamInstance));
                            m_InstanceDataOutput.SendStreamAdded(new SyncedData<StreamInstance>(key, streamInstance));
                        }
                        else
                        {
                            if (m_DirtySyncObject.Contains(key))
                            {
                                m_DirtySyncObject.Remove(key);
                                m_InstanceDataOutput.SendStreamRemoved(new SyncedData<StreamInstance>(key, previousStreamInstance));
                                m_InstanceDataOutput.SendStreamAdded(new SyncedData<StreamInstance>(key, streamInstance));
                            }
                            else
                            {
                                m_InstanceDataOutput.SendStreamChanged(new SyncedData<StreamInstance>(key, streamInstance));
                            }

                        }
                    }
                    else
                    {
                        m_InstanceDataOutput.SendStreamAdded(new SyncedData<StreamInstance>(key, streamInstance));
                    }

                    m_Cache[key] = streamInstance;

                    var syncObjectKey = new StreamKey(streamInstance.key.source, PersistentKey.GetKey<SyncObject>(streamInstance.instance.ObjectId));
                    if (!m_Instances.TryGetValue(syncObjectKey, out var instances))
                    {
                        m_Instances[syncObjectKey] = instances = new HashSet<StreamAsset>();
                    }

                    instances.Add(result.streamAsset);
                }
                else if (PersistentKey.IsKeyFor<SyncTransformOverride>(result.streamAsset.key.key))
                {
                    DataSourceProvider<SyncTransformOverride>.Update(result.streamAsset.key, TransformOverride.FromSyncModel((SyncTransformOverride)result.streamData));
                }
                else if (PersistentKey.IsKeyFor<SyncProjectTask>(result.streamAsset.key.key))
                {
                    DataSourceProvider<SyncProjectTask>.Update(result.streamAsset.key, ProjectTask.FromSyncModel((SyncProjectTask)result.streamData));
                }
                else if (PersistentKey.IsKeyFor<SyncMarker>(result.streamAsset.key.key))
                {
                    DataSourceProvider<SyncMarker>.Update(result.streamAsset.key, ProjectMarker.FromSyncModel(result.streamData));
                }
            }

            if (m_State == State.WaitingToFinish && m_DownloadRequests.IsEmpty)
            {
                m_InstanceDataOutput.SendEnd();
                m_State = State.Idle;
            }
        }
        void EnqueueDownloadRequest(StreamAsset item)
        {
            m_DownloadRequests.Enqueue(item);
            m_DownloadRequestEvent.Set();
        }

        async Task DownloadTask(CancellationToken token)
        {
            var tasks = new List<Task>();
            
            while (!token.IsCancellationRequested)
            {
                while (!token.IsCancellationRequested && m_DownloadRequests.TryDequeue(out var request))
                {
                    tasks.Add(DownloadData(request, token));
                    if (tasks.Count >= k_MaxTaskSize)
                    {
                        break;
                    }
                }

                if (tasks.Count > 0)
                {
                    Task task = null;
                    try
                    {
                        task = await Task.WhenAny(tasks);
                    }
                    catch (Exception)
                    {
                        // Do nothing, the instance query will manage the errors
                    }
                    finally
                    {
                        tasks.Remove(task);
                    }
                }
                else
                    await m_DownloadRequestEvent.WaitAsync(token);
            }

            await Task.WhenAll(tasks);
        }

        async Task DownloadData(StreamAsset request, CancellationToken token)
        {
            DownloadSyncModelResult result;
            try
            {
                var data = await m_Client.GetSyncModelAsync(request.key, request.hash, token);
                result = new DownloadSyncModelResult(request, data, null);
            }
            catch (Exception ex)
            {
                result = new DownloadSyncModelResult(request, null, ex);
            }

            m_DownloadSyncModelResults.Enqueue(result);
        }

        public override void OnPipelineInitialized()
        {
            // TODO
        }
    }
}
