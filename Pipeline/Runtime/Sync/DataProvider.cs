using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        
        protected override DataProvider Create(ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var p = new DataProvider(provider, hashCacheParam.value,
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
    
    public class DataProvider : ReflectTaskNodeProcessor
    {
        struct DownloadResult
        {
            public StreamInstanceData instanceData;
            public List<AssetEntry<ISyncModel>> downloadedDependencies; // TODO Fix order mismatch that might happen with Texture / Material

            public IEnumerable<AssetEntry<SyncMesh>> downloadedMeshes =>
                downloadedDependencies.Where(d => d.asset is SyncMesh).Select(d => new AssetEntry<SyncMesh>(d.key, (SyncMesh)d.asset));
            
            public IEnumerable<AssetEntry<SyncTexture>> downloadedTextures =>
                downloadedDependencies.Where(d => d.asset is SyncTexture).Select(d => new AssetEntry<SyncTexture>(d.key, (SyncTexture)d.asset));
            
            public IEnumerable<AssetEntry<SyncMaterial>> downloadedMaterials =>
                downloadedDependencies.Where(d => d.asset is SyncMaterial).Select(d => new AssetEntry<SyncMaterial>(d.key, (SyncMaterial)d.asset));
        }

        class StreamCache
        {
            public readonly StreamKey streamKey;

            public ISyncModel model;
            public Task<ISyncModel> task;
            public string hash;

            public StreamCache(StreamKey streamKey)
            {
                this.streamKey = streamKey;
            }
        }

        readonly ConcurrentQueue<IStream> m_DownloadRequests;
        readonly ConcurrentQueue<DownloadResult> m_DownloadedInstances;
        readonly ConcurrentQueue<AssetEntry<ISyncModel>> m_DownloadedModels;

        readonly Dictionary<StreamKey, StreamCache> m_StreamCaches;
        readonly Dictionary<StreamKey, StreamInstanceData> m_InstanceCache;

        readonly HashSet<StreamKey> m_AddedModels;

        readonly ISyncModelProvider m_Client;
        readonly IHashProvider m_HashProvider;

        readonly DataOutput<SyncMesh> m_SyncMeshOutput;
        readonly DataOutput<SyncMaterial> m_SyncMaterialOutput;
        readonly DataOutput<SyncTexture> m_SyncTextureOutput;
        readonly DataOutput<StreamInstanceData> m_InstanceDataOutput;
        
        static readonly int k_MaxTaskSize = 10;

        public DataProvider(ISyncModelProvider client, 
            IHashProvider hashProvider,
            DataOutput<SyncMesh> syncMeshOutput, 
            DataOutput<SyncMaterial> syncMaterialOutput, 
            DataOutput<SyncTexture> syncTextureOutput, 
            DataOutput<StreamInstanceData> instanceDataOutput)
        {
            m_Client = client;
            m_HashProvider = hashProvider;

            m_SyncMaterialOutput = syncMaterialOutput;
            m_SyncMeshOutput = syncMeshOutput;
            m_SyncTextureOutput = syncTextureOutput;
            m_InstanceDataOutput = instanceDataOutput;
            
            m_DownloadRequests = new ConcurrentQueue<IStream>();
            m_DownloadedInstances = new ConcurrentQueue<DownloadResult>();
            m_DownloadedModels = new ConcurrentQueue<AssetEntry<ISyncModel>>();
            
            m_StreamCaches = new Dictionary<StreamKey, StreamCache>();
            
            m_InstanceCache = new Dictionary<StreamKey, StreamInstanceData>();
            
            m_AddedModels = new HashSet<StreamKey>();
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
                m_DownloadRequests.Enqueue(stream.data);
            }
            else if (streamEvent == StreamEvent.Changed)
            {
                m_DownloadRequests.Enqueue(stream.data);
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

            m_DownloadRequests.Enqueue(stream.data);
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
            while (m_DownloadedInstances.TryDequeue(out var result))
            {
                Trace(">>>>>> SENDING " + result.instanceData.instance.instance.Name);

                // TODO Have type dependencies inside the result
                var meshes = result.downloadedMeshes;
                var textures = result.downloadedTextures;
                var materials = result.downloadedMaterials;

                foreach (var asset in meshes)
                {
                    if (m_AddedModels.Add(asset.key))
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
                TrySendAddedOrChanged(result, m_SyncTextureOutput);
                TrySendAddedOrChanged(result, m_SyncMeshOutput);
                TrySendAddedOrChanged(result, m_SyncMaterialOutput);
            }

            if (m_State == State.WaitingToFinish && m_DownloadRequests.IsEmpty && m_DownloadedModels.IsEmpty)
            {
                m_InstanceDataOutput.SendEnd();
                m_State = State.Idle;
            }
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
                tasks.Clear();
                
                while (!token.IsCancellationRequested && m_DownloadRequests.TryDequeue(out var request))
                {
                    if (request is StreamInstance instance)
                    {
                        tasks.Add(DownloadSyncObjectInstance(instance, token));
                    }
                    else
                    {
                        tasks.Add(DownloadSyncModel((StreamAsset)request, token));
                    }

                    if (tasks.Count > k_MaxTaskSize)
                    {
                        break;
                    }
                }

                await Task.WhenAll(tasks);
            }
        }

        Dictionary<SyncId, Task<DownloadResult>> m_Tasks = new Dictionary<SyncId, Task<DownloadResult>>();

        async Task DownloadSyncModel(StreamAsset streamAsset, CancellationToken token)
        {
            var syncModel = await DownloadSyncModel(streamAsset.key, streamAsset.hash, token);

            var tasks = new List<Task<AssetEntry<ISyncModel>>>();
            if (syncModel.asset is SyncMaterial syncMaterial)
            {
                DownloadTextures(streamAsset.key.source, syncMaterial, ref tasks, token);
            }

            await Task.WhenAll(tasks);
            
            foreach (var task in tasks)
            {
                m_DownloadedModels.Enqueue(task.Result);
            }

            m_DownloadedModels.Enqueue(syncModel);
        }
        
        async Task DownloadSyncObjectInstance(StreamInstance streamInstance, CancellationToken token)
        {
            DownloadResult downloadResult;
            Task<DownloadResult> task;

            lock (m_Tasks)
            {
                m_Tasks.TryGetValue(streamInstance.instance.ObjectId, out task);
            }
            
            if (task != null && !task.IsCompleted)
            {
                var result = await task;

                downloadResult = new DownloadResult
                {
                    instanceData = new StreamInstanceData(streamInstance, result.instanceData.syncObject),
                    downloadedDependencies = result.downloadedDependencies
                };
            }
            else
            {
                lock (m_Tasks)
                {
                    task = DownloadSyncInstanceDependencies(streamInstance, token);

                    m_Tasks[streamInstance.instance.ObjectId] = task;
                }
                
                downloadResult = await task;
            }

            m_DownloadedInstances.Enqueue(downloadResult);
        }

        async Task<DownloadResult> DownloadSyncInstanceDependencies(StreamInstance streamInstance, CancellationToken token)
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

        async Task<AssetEntry<ISyncModel>> DownloadSyncModel(StreamKey streamKey, string hash, CancellationToken token)
        {
            StreamCache cache;

            lock (m_StreamCaches)
            {
                if (!m_StreamCaches.TryGetValue(streamKey, out cache))
                {
                    m_StreamCaches[streamKey] = cache = new StreamCache(streamKey);
                }
            }
            
            Task<ISyncModel> task;
            var isTaskOwner = false;

            lock (cache)
            {
                if (cache.model != null && cache.hash == hash)
                {
                    return new AssetEntry<ISyncModel>(streamKey, cache.model);
                }
                
                if (cache.task != null)
                {
                    task = cache.task;
                }
                else
                {
                    isTaskOwner = true;
                    cache.model = null;
                    cache.hash = hash;
                    cache.task = task = m_Client.GetSyncModelAsync(cache.streamKey, cache.hash); // TODO Cancellation Token
                }
            }

            token.ThrowIfCancellationRequested();
            
            var syncModel = await task;

            if (isTaskOwner)
            {
                lock (cache)
                {
                    cache.model = syncModel;
                    cache.task = null;
                }
            }

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
            base.OnPipelineShutdown();
        }
    }
}
