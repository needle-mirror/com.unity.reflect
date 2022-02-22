using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    [Serializable]
    public class DataProviderNode : ReflectNode<DataProvider>
    {
        public HashCacheParam hashCacheParam = new HashCacheParam();

        public StreamAssetInput assetInput = new StreamAssetInput();
        public StreamInstanceInput instanceInput = new StreamInstanceInput();

        public SyncMeshOutput syncMeshOutput = new SyncMeshOutput();
        public SyncMaterialOutput syncMaterialOutput = new SyncMaterialOutput();
        public SyncTextureOutput syncTextureOutput = new SyncTextureOutput();
        public StreamInstanceDataOutput instanceDataOutput = new StreamInstanceDataOutput();
        
        protected override DataProvider Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var p = new DataProvider(hook.Services.EventHub, hook.Services.MemoryTracker, provider, hashCacheParam.value,
                syncMeshOutput, syncMaterialOutput, syncTextureOutput, instanceDataOutput);

            instanceInput.streamBegin = p.OnStreamInstanceBegin;
            instanceInput.streamEvent = p.OnStreamInstanceEvent;
            instanceInput.streamEnd = p.OnStreamInstanceEnd;

            assetInput.streamEvent = p.OnStreamAssetEvent;
            
            p.Run();
            
            return p;
        }
    }

    class AssetEntry<T>
    {
        public readonly StreamKey key;
        public readonly T asset;
        
        public AssetEntry(StreamKey key, T asset)
        {
            this.key = key;
            this.asset = asset;
        }
    }
    
    
    struct StreamHash : IEquatable<StreamHash>
    {
        public readonly StreamKey streamKey;
        public readonly string hash;
        public StreamHash(StreamKey streamKey, string hash)
        {
            this.streamKey = streamKey;
            this.hash = hash;
        }

        public bool Equals(StreamHash other)
        {
            return streamKey.Equals(other.streamKey) && hash == other.hash;
        }

        public override bool Equals(object obj)
        {
            return obj is StreamHash other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (streamKey.GetHashCode() * 397) ^ (hash != null ? hash.GetHashCode() : 0);
            }
        }
    }

    public class DataProvider : ReflectTaskNodeProcessor
    {
        struct DownloadResult
        {
            public Exception exception;
            public StreamInstanceData instanceData;
            public List<AssetEntry<ISyncModel>> downloadedDependencies; // TODO Fix order mismatch that might happen with Texture / Material

            public IEnumerable<AssetEntry<SyncMesh>> downloadedMeshes =>
                downloadedDependencies.Where(d => d.asset is SyncMesh).Select(d => new AssetEntry<SyncMesh>(d.key, (SyncMesh)d.asset));
            
            public IEnumerable<AssetEntry<SyncTexture>> downloadedTextures =>
                downloadedDependencies.Where(d => d.asset is SyncTexture).Select(d => new AssetEntry<SyncTexture>(d.key, (SyncTexture)d.asset));
            
            public IEnumerable<AssetEntry<SyncMaterial>> downloadedMaterials =>
                downloadedDependencies.Where(d => d.asset is SyncMaterial).Select(d => new AssetEntry<SyncMaterial>(d.key, (SyncMaterial)d.asset));
        }

        struct ObjectDownloadResult
        {
            public AssetEntry<ISyncModel> assetEntry;
            public Exception exception;
        }

        readonly EventHub m_Hub;
        readonly MemoryTracker m_MemTracker;

        readonly ConcurrentQueue<IStream> m_DownloadRequests;
        readonly ConcurrentQueue<DownloadResult> m_DownloadedInstances;
        readonly ConcurrentQueue<ObjectDownloadResult> m_DownloadedModels;

        readonly Dictionary<StreamHash, Task<DownloadResult>> m_SyncTasks;
        readonly Dictionary<StreamHash, Task<ISyncModel>> m_StreamTasks;
        readonly Dictionary<StreamKey, StreamInstanceData> m_InstanceCache;

        readonly HashSet<StreamKey> m_AddedModels;

        readonly ISyncModelProvider m_Client;
        readonly IHashProvider m_HashProvider;

        readonly DataOutput<SyncMesh> m_SyncMeshOutput;
        readonly DataOutput<SyncMaterial> m_SyncMaterialOutput;
        readonly DataOutput<SyncTexture> m_SyncTextureOutput;
        readonly DataOutput<StreamInstanceData> m_InstanceDataOutput;

#if UNITY_ANDROID && !UNITY_EDITOR
        static readonly int k_MaxTaskSize = 5;
#else
        static readonly int k_MaxTaskSize = 10;
#endif

        EventHub.Handle m_HubHandle;
        MemoryTracker.Handle<StreamKey, Mesh> m_MeshesHandle;
        AsyncAutoResetEvent m_DownloadRequestEvent = new AsyncAutoResetEvent();

        public DataProvider(EventHub hub,
            MemoryTracker memTracker,
            ISyncModelProvider client, 
            IHashProvider hashProvider,
            DataOutput<SyncMesh> syncMeshOutput, 
            DataOutput<SyncMaterial> syncMaterialOutput, 
            DataOutput<SyncTexture> syncTextureOutput, 
            DataOutput<StreamInstanceData> instanceDataOutput)
        {
            m_Hub = hub;
            m_MemTracker = memTracker;
            m_Client = client;
            m_HashProvider = hashProvider;

            m_SyncMaterialOutput = syncMaterialOutput;
            m_SyncMeshOutput = syncMeshOutput;
            m_SyncTextureOutput = syncTextureOutput;
            m_InstanceDataOutput = instanceDataOutput;
            
            m_DownloadRequests = new ConcurrentQueue<IStream>();
            m_DownloadedInstances = new ConcurrentQueue<DownloadResult>();
            m_DownloadedModels = new ConcurrentQueue<ObjectDownloadResult>();
            
            m_SyncTasks = new Dictionary<StreamHash, Task<DownloadResult>>();
            m_StreamTasks = new Dictionary<StreamHash, Task<ISyncModel>>();
            m_InstanceCache = new Dictionary<StreamKey, StreamInstanceData>();
            
            m_AddedModels = new HashSet<StreamKey>();

            m_HubHandle = m_Hub.Subscribe<MemoryTrackerCacheCreatedEvent<Mesh>>(e => m_MeshesHandle = e.handle);
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
        
        // TODO Add event for modified / removed asset
        
        public void OnStreamInstanceEvent(SyncedData<StreamInstance> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                EnqueueDownloadRequest(stream.data);
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                EnqueueDownloadRequest(stream.data);
            }
            else if (streamEvent == StreamEvent.Removed)
            {
                var key = stream.key;

                if (m_InstanceCache.TryGetValue(key, out var instanceData))
                {
                    m_InstanceCache.Remove(key);
                    m_InstanceDataOutput.SendStreamRemoved(new SyncedData<StreamInstanceData>(key, instanceData));
                }
            }
        }
        
        public void OnStreamAssetEvent(SyncedData<StreamAsset> stream, StreamEvent eventType)
        {
            if (eventType != StreamEvent.Changed)
                return;

            if (PersistentKey.IsKeyFor<SyncObjectInstance>(stream.key.key))
                return;

            var key = stream.key;
            if (!m_AddedModels.Contains(key)) // Asset was not downloaded and is not used by any instance. Skip.
                return;

            EnqueueDownloadRequest(stream.data);
        }

        public void OnStreamInstanceEnd()
        {
            m_State = State.WaitingToFinish;
        }
        
        protected override Task RunInternal(CancellationToken token)
        {
            return DownloadTask(token);
        }

        static void Trace(string msg)
        {
            //Debug.Log(msg);
        }
        
        protected override void UpdateInternal(float unscaledDeltaTime)
        {
            // Postponing instances if there are available models (in order to update model caches first)
            while (m_DownloadedModels.Count == 0 && m_DownloadedInstances.TryDequeue(out var result))
            {
                if (result.exception != null)
                {
                    m_Hub.Broadcast(new StreamingErrorEvent(result.instanceData.instance.key, result.instanceData.instance.boundingBox, result.exception));
                    continue;
                }

                Trace(">>>>>> SENDING " + result.instanceData.instance.instance.Name);
                
                // TODO Have type dependencies inside the result
                var meshes = result.downloadedMeshes;
                var textures = result.downloadedTextures;
                var materials = result.downloadedMaterials;

                foreach (var asset in meshes)
                {
                    m_AddedModels.Add(asset.key);
                    if (!m_MemTracker.ContainsKey(m_MeshesHandle, asset.key))
                    {
                        Trace("        >> Sending " + asset.GetType().Name + " " + asset.asset.Name);
                        m_SyncMeshOutput.SendStreamAdded(new SyncedData<SyncMesh>(asset.key, asset.asset));
                    }
                }

                foreach (var asset in textures)
                {
                    if (m_AddedModels.Add(asset.key))
                    {
                        Trace("        >> Sending " + asset.GetType().Name + " " + asset.asset.Name);
                        m_SyncTextureOutput.SendStreamAdded(new SyncedData<SyncTexture>(asset.key, asset.asset));
                    }
                }
                
                foreach (var asset in materials)
                {
                    if (m_AddedModels.Add(asset.key))
                    {
                        Trace("        >> Sending " + asset.GetType().Name + " " + asset.asset.Name);
                        m_SyncMaterialOutput.SendStreamAdded(new SyncedData<SyncMaterial>(asset.key, asset.asset));
                    }
                }

                var key = result.instanceData.instance.key;

                if (m_InstanceCache.ContainsKey(key))
                {
                    m_InstanceDataOutput.SendStreamChanged(new SyncedData<StreamInstanceData>(key, result.instanceData));
                }
                else
                {
                    m_InstanceDataOutput.SendStreamAdded(new SyncedData<StreamInstanceData>(key, result.instanceData));
                }

                m_InstanceCache[key] = result.instanceData;

                Trace(">> DONE " + result.instanceData.instance.instance.Name);
            }

            while (m_DownloadedModels.TryDequeue(out var result))
            {
                // Skip without broadcasting error. Errors are broadcasted for each SyncInstance, not for SyncObject or each sub-resource
                if (result.exception != null)
                    continue;

                TrySendAddedOrChanged(result.assetEntry, m_SyncTextureOutput);
                TrySendAddedOrChanged(result.assetEntry, m_SyncMeshOutput);
                TrySendAddedOrChanged(result.assetEntry, m_SyncMaterialOutput);
            }

            if (m_State == State.WaitingToFinish && m_DownloadRequests.IsEmpty && m_DownloadedModels.IsEmpty)
            {
                m_InstanceDataOutput.SendEnd();
                m_State = State.Idle;
            }
        }

        void EnqueueDownloadRequest(IStream item)
        {
            m_DownloadRequests.Enqueue(item);
            m_DownloadRequestEvent.Set();
        }

        void TrySendAddedOrChanged<T>(AssetEntry<ISyncModel> entry, IOutput<SyncedData<T>> output)
        {
            if (entry.asset is T value)
            {
                if (m_AddedModels.Add(entry.key))
                {
                    output.SendStreamAdded(new SyncedData<T>(entry.key, value));
                }
                else
                {
                    output.SendStreamChanged(new SyncedData<T>(entry.key, value));
                }
            }
        }

        async Task DownloadTask(CancellationToken token)
        {
            var tasks = new List<Task>();
            
            while (!token.IsCancellationRequested)
            {
                while (!token.IsCancellationRequested && m_DownloadRequests.TryDequeue(out var request))
                {
                    if (request is StreamInstance instance)
                    {
                        tasks.Add(DownloadSyncObjectInstance(instance, token)); 
                    }
                    else if(request is StreamAsset asset)
                    {
                        tasks.Add(DownloadSyncModel(asset, token));
                    }

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


        async Task DownloadSyncModel(StreamAsset streamAsset, CancellationToken token)
        {
            var result = new ObjectDownloadResult();
            try
            {
                result.assetEntry = await DownloadSyncModel(streamAsset.key, streamAsset.hash, token);
            }
            catch (Exception ex)
            {
                result.exception = ex;
                m_DownloadedModels.Enqueue(result);
                return;
            }
            
            var tasks = new List<Task<AssetEntry<ISyncModel>>>();
            if (result.assetEntry.asset is SyncMaterial syncMaterial)
            {
                DownloadTextures(streamAsset.key.source, syncMaterial, ref tasks, token);
            }

            try
            {
                await Task.WhenAll(tasks);

                foreach (var task in tasks)
                    m_DownloadedModels.Enqueue(new ObjectDownloadResult { assetEntry = task.Result });

                m_DownloadedModels.Enqueue(new ObjectDownloadResult { assetEntry = result.assetEntry });
            }
            catch (Exception ex)
            {
                foreach(var task in tasks)
                    m_DownloadedModels.Enqueue(new ObjectDownloadResult { exception = task.Exception });

                result.exception = ex;
                m_DownloadedModels.Enqueue(result);
            }
        }
        
        async Task DownloadSyncObjectInstance(StreamInstance streamInstance, CancellationToken token)
        {
            DownloadResult downloadResult;
            Task<DownloadResult> task;
            
            var key = new StreamKey(streamInstance.key.source, PersistentKey.GetKey<SyncObject>(streamInstance.instance.ObjectId.Value));
            var hash = m_HashProvider.GetHash(key);

            var streamHash = new StreamHash(key, hash);

            lock (m_SyncTasks)
            {
                m_SyncTasks.TryGetValue(streamHash, out task);
            }
            
            if (task != null)
            {
                var result = await task;

                downloadResult = new DownloadResult
                {
                    exception = result.exception,
                    instanceData = new StreamInstanceData(streamInstance, result.instanceData.syncObject),
                    downloadedDependencies = result.downloadedDependencies
                };
            }
            else
            {
                lock (m_SyncTasks)
                {
                    task = DownloadSyncInstanceDependencies(streamInstance, token);

                    m_SyncTasks[streamHash] = task;
                }
                
                downloadResult = await task;
            }

            m_DownloadedInstances.Enqueue(downloadResult);
        }


        async Task<DownloadResult> DownloadSyncInstanceDependencies(StreamInstance streamInstance, CancellationToken token)
        {
            try
            {
                var sourceId = streamInstance.key.source;

                var result = await DownloadSyncModel<SyncObject>(sourceId, streamInstance.instance.ObjectId, token);

                var syncObject = (SyncObject)result.asset;

                var tasks = new List<Task<AssetEntry<ISyncModel>>>();

                DownloadMeshes(sourceId, syncObject, ref tasks, token);

                DownloadMaterials(sourceId, syncObject, ref tasks, token);

                token.ThrowIfCancellationRequested();

                await Task.WhenAll(tasks);

                var downloadResult = new DownloadResult
                {
                    instanceData = new StreamInstanceData(streamInstance, syncObject),
                    downloadedDependencies = tasks.Select(r => r.Result).ToList()
                };

                return downloadResult;
            }
            catch (Exception ex)
            {
                return new DownloadResult
                {
                    exception = ex,
                    instanceData = new StreamInstanceData(streamInstance, null)
                };
            }
        }

        async Task<AssetEntry<ISyncModel>> DownloadSyncModel(StreamKey streamKey, string hash, CancellationToken token)
        {
            Task<ISyncModel> task;

            var streamHash = new StreamHash(streamKey, hash);
            
            lock (m_StreamTasks)
            {
                m_StreamTasks.TryGetValue(streamHash, out task);
            }

            if (task == null)
            {
                lock (m_StreamTasks)
                {
                    task = m_Client.GetSyncModelAsync(streamKey, hash, token);
                    
                    m_StreamTasks[streamHash] = task;
                }
            }
            
            token.ThrowIfCancellationRequested();
            
            var syncModel = await task;
            return new AssetEntry<ISyncModel>(streamKey, syncModel);
        }
        
        async Task<AssetEntry<ISyncModel>> DownloadSyncModel<T>(string sourceId, SyncId id, CancellationToken token) where T : ISyncModel
        {
            var key = new StreamKey(sourceId, PersistentKey.GetKey<T>(id.Value));
            var hash = m_HashProvider.GetHash(key);
            
            var syncModel = await DownloadSyncModel(key, hash, token);
            
            return syncModel;
        }

        void DownloadMeshes(string sourceId, SyncObject syncObject, ref List<Task<AssetEntry<ISyncModel>>> tasks, CancellationToken token)
        {
            if (syncObject.MeshId != SyncId.None)
            {
                tasks.Add(DownloadSyncModel<SyncMesh>(sourceId, syncObject.MeshId, token));
            }

            foreach (var child in syncObject.Children)
            {
                DownloadMeshes(sourceId, child, ref tasks, token);
            }
        }

        void DownloadMaterials(string sourceId, SyncObject syncObject, ref List<Task<AssetEntry<ISyncModel>>> tasks, CancellationToken token)
        {
            foreach (var materialId in syncObject.MaterialIds)
            {
                if (materialId != SyncId.None)
                {
                    tasks.AddRange(DownloadMaterial(sourceId, materialId, token).Result);
                }
            }
            
            foreach (var child in syncObject.Children)
            {
                DownloadMaterials(sourceId, child, ref tasks, token);
            }
        }

        async Task<List<Task<AssetEntry<ISyncModel>>>> DownloadMaterial(string sourceId, SyncId materialId, CancellationToken token)
        {
            var tasks = new List<Task<AssetEntry<ISyncModel>>>();
            
            var materialTask = DownloadSyncModel<SyncMaterial>(sourceId, materialId, token);

            var materialResult = await materialTask;
            
            tasks.Add(materialTask);
            DownloadTextures(sourceId, (SyncMaterial)materialResult.asset, ref tasks, token);

            return tasks;
        }
        
        void DownloadTextures(string sourceId, SyncMaterial syncMaterial, ref List<Task<AssetEntry<ISyncModel>>> tasks, CancellationToken token)
        {
            var textureId = syncMaterial.AlbedoMap.TextureId;
            if (textureId != SyncId.None)
            {
                tasks.Add(DownloadSyncModel<SyncTexture>(sourceId, textureId, token));
            }

            textureId = syncMaterial.NormalMap.TextureId;
            if (textureId != SyncId.None)
            {
                tasks.Add(DownloadSyncModel<SyncTexture>(sourceId, textureId, token));
            }

            textureId = syncMaterial.AlphaMap.TextureId;
            if (textureId != SyncId.None)
            {
                tasks.Add(DownloadSyncModel<SyncTexture>(sourceId, textureId, token));
            }

            textureId = syncMaterial.CutoutMap.TextureId;
            if (textureId != SyncId.None)
            {
                tasks.Add(DownloadSyncModel<SyncTexture>(sourceId, textureId, token));
            }

            textureId = syncMaterial.MetallicMap.TextureId;
            if (textureId != SyncId.None)
            {
                tasks.Add(DownloadSyncModel<SyncTexture>(sourceId, textureId, token));
            }

            textureId = syncMaterial.GlossinessMap.TextureId;
            if (textureId != SyncId.None)
            {
                tasks.Add(DownloadSyncModel<SyncTexture>(sourceId, textureId, token));
            }

            textureId = syncMaterial.EmissionMap.TextureId;
            if (textureId != SyncId.None)
            {
                tasks.Add(DownloadSyncModel<SyncTexture>(sourceId, textureId, token));
            }
        }

        public override void OnPipelineInitialized()
        {
            // TODO
        }

        public override void OnPipelineShutdown()
        {
            m_AddedModels.Clear();
            m_Hub.Unsubscribe(m_HubHandle);
            base.OnPipelineShutdown();
        }
    }
}
