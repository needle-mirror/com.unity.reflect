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
        
        protected override SyncObjectInstanceProvider Create(ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var node = new SyncObjectInstanceProvider(provider, output);

            input.streamBegin = node.OnStreamInstanceBegin;
            input.streamEvent = node.OnStreamInstanceEvent;
            input.streamEnd = node.OnStreamInstanceEnd;
            
            node.Run();
            
            return node;
        }
    }

    public struct StreamKey
    {
        public readonly string source;
        public readonly PersistentKey key;

        public StreamKey(string sourceId, PersistentKey key)
        {
            this.key = key;
            source = sourceId;
        }
    }
    
    public class SyncObjectInstanceProvider : ReflectTaskNodeProcessor
    {
        struct DownloadResult
        {
            public readonly StreamAsset streamAsset;
            public readonly SyncObjectInstance streamInstance;

            public DownloadResult(StreamAsset streamAsset, SyncObjectInstance streamInstance)
            {
                this.streamAsset = streamAsset;
                this.streamInstance = streamInstance;
            }
        }

        readonly ConcurrentQueue<StreamAsset> m_DownloadRequests;
        readonly ConcurrentQueue<DownloadResult> m_DownloadResults;

        readonly Dictionary<StreamKey, HashSet<StreamAsset>> m_Instances;
        readonly Dictionary<StreamKey, StreamInstance> m_Cache;

        readonly HashSet<StreamKey> m_DirtySyncObject;

        readonly ISyncModelProvider m_Client;

        readonly DataOutput<StreamInstance> m_InstanceDataOutput;
        
        static readonly int k_MaxTaskSize = 10;

        public SyncObjectInstanceProvider(ISyncModelProvider client, DataOutput<StreamInstance> instanceDataOutput)
        {
            m_Client = client;

            m_InstanceDataOutput = instanceDataOutput;
            
            m_DownloadRequests = new ConcurrentQueue<StreamAsset>();
            m_DownloadResults = new ConcurrentQueue<DownloadResult>();
            
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
                if (!PersistentKey.IsKeyFor<SyncObjectInstance>(stream.key.key))
                    return;
                
                m_DownloadRequests.Enqueue(stream.data);
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                if (PersistentKey.IsKeyFor<SyncObjectInstance>(stream.key.key))
                {
                    m_DownloadRequests.Enqueue(stream.data);
                }
                else if (PersistentKey.IsKeyFor<SyncObject>(stream.key.key))
                {
                    var instances = m_Instances[stream.key];

                    foreach (var instance in instances)
                    {
                        m_DirtySyncObject.Add(instance.key);
                        m_DownloadRequests.Enqueue(instance);
                    }
                }
            }
            else if (streamEvent == StreamEvent.Removed)
            {
                if (!PersistentKey.IsKeyFor<SyncObjectInstance>(stream.key.key))
                    return;

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
            while (m_DownloadResults.TryDequeue(out var result))
            {
                var streamInstance = new StreamInstance(result.streamAsset.key,
                    result.streamInstance, result.streamAsset.boundingBox);

                var key = result.streamAsset.key;
                
                if (m_Cache.TryGetValue(key, out var previousStreamInstance))
                {
                    if (previousStreamInstance.instance.ObjectId != result.streamInstance.ObjectId)
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

            if (m_State == State.WaitingToFinish && m_DownloadRequests.IsEmpty)
            {
                m_InstanceDataOutput.SendEnd();
                m_State = State.Idle;
            }
        }

        async Task DownloadTask(CancellationToken token)
        {
            var tasks = new List<Task>();
            
            while (!token.IsCancellationRequested)
            {
                tasks.Clear();
                
                while (!token.IsCancellationRequested && m_DownloadRequests.TryDequeue(out var request))
                {
                    tasks.Add(DownloadInstance(request, token));

                    if (tasks.Count > k_MaxTaskSize)
                    {
                        break;
                    }
                }

                await Task.WhenAll(tasks);
            }
        }

        async Task DownloadInstance(StreamAsset request, CancellationToken token)
        {
            var instance = (SyncObjectInstance) await m_Client.GetSyncModelAsync(request.key, request.hash); // TODO Cancellation Token

            var result = new DownloadResult(request, instance);

            m_DownloadResults.Enqueue(result);
        }

        public override void OnPipelineInitialized()
        {
            // TODO
        }
    }
}