using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Reflect.Actors
{
    /// <summary>
    ///     Load and cache unity materials.
    /// </summary>
    [Actor("d44a9ade-d0c8-431e-b652-f928dcdab3a5")]
    public class UnityMaterialActor
    {
#pragma warning disable 649
        RpcOutput<AcquireResource> m_AcquireResourceOutput;
        NetOutput<ReleaseModelResource> m_ReleaseModelResourceOutput;
        RpcOutput<ConvertSyncMaterial> m_ConvertSyncMaterialOutput;
        RpcOutput<AcquireEntryDataFromModelData> m_AcquireEntryDataFromModelDataOutput;
        RpcOutput<DelegateJob> m_DelegateJobOutput;
        NetOutput<ReleaseUnityTexture> m_ReleaseUnityTextureOutput;
        PipeOutput<MaterialCreating> m_MaterialCreatingOutput;
        PipeOutput<MaterialDestroying> m_MaterialDestroyingOutput;
        
        [RpcOutput(optional:true)]
        RpcOutput<UpdateEntryDependencies> m_UpdateEntryDependenciesOutput;
#pragma warning restore 649
        
        bool m_IsAutomaticCleaningEnabled;

        Dictionary<EntryGuid, Queue<Tracker>> m_MaterialWaiters = new Dictionary<EntryGuid, Queue<Tracker>>();
        Dictionary<EntryGuid, ResourceVersions> m_ResourceVersions = new Dictionary<EntryGuid, ResourceVersions>();
        Dictionary<Material, ResourceVersions> m_Materials = new Dictionary<Material, ResourceVersions>();

        public void Shutdown()
        {
            foreach (var resource in m_ResourceVersions.Values.SelectMany(x => x.Versions))
            {
                if (resource.MainResource != null)
                    Object.Destroy(resource.MainResource);
            }
        }

        [RpcInput]
        void OnAcquireUnityMaterial(RpcContext<AcquireUnityMaterial> ctx)
        {
            var trackers = GetOrCreateMaterialTrackers(ctx);
            trackers.Enqueue(new Tracker{ Ctx = ctx });
            AcquireMaterial(trackers, trackers.Count - 1);
        }

        [NetInput]
        void OnReleaseUnityMaterial(NetContext<ReleaseUnityMaterial> ctx)
        {
            ReleaseResources(ctx.Data.Material);
        }

        [NetInput]
        void OnMemoryStateChanged(NetContext<MemoryStateChanged> ctx)
        {
            if (ctx.Data.IsMemoryLevelTooHigh)
                DestroyResources();
        }

        [PipeInput]
        void OnCleanAfterCriticalMemory(PipeContext<CleanAfterCriticalMemory> ctx)
        {
            DestroyResources();
            ctx.Continue();
        }

        [NetInput]
        void OnSetAutomaticCacheCleaning(NetContext<SetAutomaticCacheCleaning> ctx)
        {
            m_IsAutomaticCleaningEnabled = ctx.Data.IsEnabled;
            if (m_IsAutomaticCleaningEnabled)
            {
                var resourceToDestroy = new Dictionary<EntryGuid, Resource>();
                var keyToRemove = new List<ResourceVersions>();
                foreach (var kv in m_ResourceVersions)
                {
                    var versions = kv.Value.Versions;
                    for (var i = versions.Count - 1; i >= 0; --i)
                    {
                        var version = versions[i];

                        if (version.Count == 0)
                            versions.RemoveAt(i);
                        
                        if (version.Count == 0 &&
                            versions
                                .Where(x => x.DependencyManifestId == version.DependencyManifestId)
                                .Sum(x => x.Count) == 0)
                        {
                            var original = version;
                            for (var j = i - 1; j >= 0; --j)
                            {
                                var sibling = versions[j];
                                if (sibling.DependencyManifestId == version.DependencyManifestId)
                                {
                                    if (sibling.IsOriginal)
                                        original = sibling;

                                    versions.RemoveAt(j);
                                    --i;
                                }
                            }

                            resourceToDestroy.Add(kv.Key, original);
                        }
                    }

                    if (versions.Count == 0)
                        keyToRemove.Add(kv.Value);
                }
                
                foreach (var rv in keyToRemove)
                {
                    m_ResourceVersions.Remove(rv.Id);

                    foreach (var version in rv.Versions)
                        m_Materials.Remove(version.MainResource);
                }

                DestroyResources(resourceToDestroy);
            }
        }

        void DestroyResources(Dictionary<EntryGuid, Resource> resources)
        {
            var counterDown = new CountDownTracker<Resource>(0, resources);
            foreach (var kv in counterDown.DestroyingResources)
            {
                var resource = kv.Value;
                ++counterDown.NbRemainingRequests;
                var pipe = m_MaterialDestroyingOutput.Push(this, (object)null, counterDown, new MaterialDestroying(kv.Key, resource.ManifestId, resource.MainResource));
                pipe.Success((self, ctx, countDown, msg) => TryCompleteDestroyingResources(countDown));
                pipe.Failure((self, ctx, countDown, ex) => TryCompleteDestroyingResources(countDown));
            }
        }

        void DestroyResources()
        {
            var countDown = new CountDownTracker<ResourceVersions>(0, m_ResourceVersions);
            m_ResourceVersions = new Dictionary<EntryGuid, ResourceVersions>();
            m_Materials = new Dictionary<Material, ResourceVersions>();

            var hash = new HashSet<ManifestGuid>();
            foreach (var kv in countDown.DestroyingResources)
            {
                hash.Clear();
                foreach (var resource in kv.Value.Versions)
                {
                    if (!hash.Add(resource.DependencyManifestId))
                        continue;

                    ++countDown.NbRemainingRequests;
                    var pipe = m_MaterialDestroyingOutput.Push(this, (object)null, countDown, new MaterialDestroying(kv.Key, resource.ManifestId, resource.MainResource));
                    pipe.Success((self, ctx, countDown, msg) => TryCompleteDestroyingResources(countDown));
                    pipe.Failure((self, ctx, countDown, ex) => TryCompleteDestroyingResources(countDown));
                }
            }
        }

        void CompleteDestroyingResource(Resource resource)
        {
            ReleaseAttachedTextures(resource);

            var jobInput = new DestroyJobInput<Resource>(resource);
            var rpc = m_DelegateJobOutput.Call(this, (object)null, jobInput, new DelegateJob(jobInput, (ctx, input) =>
            {
                var resource = ((DestroyJobInput<Resource>)input).Data;
                Object.Destroy(resource.MainResource);
                ctx.SendSuccess(NullData.Null);
            }));
            rpc.Success<NullData>((self, ctx, jobInput, _) => {});
            rpc.Failure((self, ctx, jobInput, ex) =>
            {
                if (ex is OperationCanceledException)
                    return;
                Debug.LogException(ex);
            });
        }

        void TryCompleteDestroyingResources(CountDownTracker<Resource> countDown)
        {
            --countDown.NbRemainingRequests;
            if (countDown.NbRemainingRequests > 0)
                return;

            foreach (var kv in countDown.DestroyingResources)
                ReleaseAttachedTextures(kv.Value);

            var jobInput = new DestroyJobInputDic<Resource>(countDown.DestroyingResources);
            var rpc = m_DelegateJobOutput.Call(this, (object)null, jobInput, new DelegateJob(jobInput, (c, i) => DestroyJobSingle(c, i)));
            rpc.Success<NullData>((self, ctx, jobInput, _) => {});
            rpc.Failure((self, ctx, jobInput, ex) => Debug.LogException(ex));
        }

        void TryCompleteDestroyingResources(CountDownTracker<ResourceVersions> countDown)
        {
            --countDown.NbRemainingRequests;
            if (countDown.NbRemainingRequests > 0)
                return;

            var hash = new HashSet<ManifestGuid>();
            foreach (var kv in countDown.DestroyingResources)
            {
                hash.Clear();
                foreach (var resource in kv.Value.Versions)
                {
                    if (!hash.Add(resource.DependencyManifestId))
                        continue;
                
                    ReleaseAttachedTextures(resource);
                }
            }

            var jobInput = new DestroyJobInputDic<ResourceVersions>(countDown.DestroyingResources);
            var rpc = m_DelegateJobOutput.Call(this, (object)null, jobInput, new DelegateJob(jobInput, (c, i) => DestroyJobMany(c, i)));
            rpc.Success<NullData>((self, ctx, jobInput, _) => {});
            rpc.Failure((self, ctx, jobInput, ex) => Debug.LogException(ex));
        }

        static void DestroyJobSingle(RpcContext<DelegateJob> ctx, object input)
        {
            var data = ((DestroyJobInputDic<Resource>)input).Data;
            foreach (var resource in data.Select(x => x.Value))
            {
                if (resource.MainResource != null)
                    Object.Destroy(resource.MainResource);
            }
            ctx.SendSuccess(NullData.Null);
        }

        void ReleaseAttachedTextures(Resource resource)
        {
            foreach (var texture in resource.Textures)
                m_ReleaseUnityTextureOutput.Send(new ReleaseUnityTexture(texture));
        }

        static void DestroyJobMany(RpcContext<DelegateJob> ctx, object input)
        {
            var data = ((DestroyJobInputDic<ResourceVersions>)input).Data;
            foreach (var resource in data.SelectMany(x => x.Value.Versions))
            {
                if (resource.MainResource != null)
                    Object.Destroy(resource.MainResource);
            }
            ctx.SendSuccess(NullData.Null);
        }

        Queue<Tracker> GetOrCreateMaterialTrackers(RpcContext<AcquireUnityMaterial> ctx)
        {
            if (!m_MaterialWaiters.TryGetValue(ctx.Data.ResourceData.Id, out var trackers))
            {
                trackers = new Queue<Tracker>();
                m_MaterialWaiters.Add(ctx.Data.ResourceData.Id, trackers);
            }

            return trackers;
        }

        void AcquireMaterial(Queue<Tracker> trackers, int index)
        {
            if (index != 0)
                return;
            
            while (true)
            {
                var tracker = trackers.Peek();
                var ctx = tracker.Ctx;
                
                if (CompleteIfStreamCanceled(ctx))
                {
                    if (AdjustQueueAndContinue(trackers))
                        continue;
                    return;
                }

                if (TryGetResourceFromCacheForCurrentManifest(ctx, out var resource))
                {
                    ctx.SendSuccess(resource.MainResource);

                    if (AdjustQueueAndContinue(trackers))
                        continue;
                    return;
                }

                var rpc = m_AcquireResourceOutput.Call(this, ctx, tracker, new AcquireResource(new StreamState(), ctx.Data.ResourceData));
                rpc.Success<SyncMaterial>((self, ctx, tracker, material) =>
                {
                    self.ProcessMaterialRequest(ctx, tracker, material);
                });

                rpc.Failure((self, ctx, tracker, ex) =>
                {
                    var (entry, trackers) = GetMaterialCommonData(ctx);

                    foreach (var t in trackers)
                        t.Ctx.SendFailure(ex);

                    m_MaterialWaiters.Remove(entry.Id);
                });
                break;
            }
        }

        bool TryGetResourceFromCacheForCurrentManifest(RpcContext<AcquireUnityMaterial> ctx, out Resource resourceInCache)
        {
            resourceInCache = null;
            if (m_ResourceVersions.TryGetValue(ctx.Data.ResourceData.Id, out var rv))
            {
                // Shortcut, when there's no dependency, no other versions can be generated for the same EntryGuid
                if (rv.Versions[0].Dependencies.Count == 0)
                {
                    resourceInCache = rv.Versions[0];
                    AdjustRefCount(ctx.Data.ResourceData.Id, resourceInCache, 1, rv.Versions);
                    return true;
                }

                resourceInCache = rv.Versions.FirstOrDefault(x => x.ManifestId == ctx.Data.ManifestId);
                if (resourceInCache != null)
                {
                    AdjustRefCount(ctx.Data.ResourceData.Id, resourceInCache, 1, rv.Versions);
                    return true;
                }
            }

            return false;
        }

        void AdjustRefCount(EntryGuid id, Resource version, int toAdd, List<Resource> versions)
        {
            var prevVal = version.Count;
            version.Count += toAdd;

            if (!m_IsAutomaticCleaningEnabled)
                return;
            
            if (prevVal >= 1 && version.Count == 0)
                TryDestroyResource(id, version, versions);
        }

        void TryDestroyResource(EntryGuid id, Resource version, List<Resource> versions)
        {
            if (versions
                .Where(x => x.DependencyManifestId == version.DependencyManifestId)
                .Sum(x => x.Count) == 0)
            {
                var original = versions.First(x => x.IsOriginal && x.DependencyManifestId == version.DependencyManifestId);
                versions.RemoveAll(x => x.DependencyManifestId == version.DependencyManifestId);
                m_Materials.Remove(original.MainResource);

                if (versions.Count == 0)
                    m_ResourceVersions.Remove(id);

                var pipe = m_MaterialDestroyingOutput.Push(this, (object)null, original, new MaterialDestroying(id, original.ManifestId, original.MainResource));
                pipe.Success((self, ctx, original, msg) => CompleteDestroyingResource(original));
                pipe.Failure((self, ctx, original, ex) => CompleteDestroyingResource(original));
            }
        }

        bool AdjustQueueAndContinue(Queue<Tracker> trackers)
        {
            var lastProcessed = trackers.Dequeue();
            if (trackers.Count == 0)
            {
                m_MaterialWaiters.Remove(lastProcessed.Ctx.Data.ResourceData.Id);
                return false;
            }
            
            return true;
        }

        void ProcessRemainingRequests(Queue<Tracker> trackers)
        {
            if (AdjustQueueAndContinue(trackers))
                AcquireMaterial(trackers, 0);
        }

        void ProcessMaterialRequest(RpcContext<AcquireUnityMaterial> ctx, Tracker tracker, SyncMaterial material)
        {
            var textureIds = GetTextureIds(material);
            tracker.SyncMaterial = material;
            tracker.TextureEntries = new List<EntryData>(Enumerable.Repeat((EntryData)null, textureIds.Count));
            tracker.NbRemaining = textureIds.Count;

            if (tracker.NbRemaining == 0)
            {
                LoadMaterial(ctx, tracker);
                return;
            }
            
            for (var i = 0; i < textureIds.Count; ++i)
            {
                var waitAllTracker = new WaitAllTracker { Tracker = tracker, Position = i };

                var rpc = m_AcquireEntryDataFromModelDataOutput.Call(this, ctx, waitAllTracker, new AcquireEntryDataFromModelData(ctx.Data.ManifestId, new PersistentKey(typeof(SyncTexture), textureIds[i])));
                rpc.Success<EntryData>((self, ctx, waitAllTracker, textureEntry) =>
                {
                    var tracker = waitAllTracker.Tracker;
                    tracker.TextureEntries[waitAllTracker.Position] = textureEntry;
                    --tracker.NbRemaining;

                    if (tracker.NbRemaining == 0)
                    {
                        if (tracker.LatestException == null)
                            self.LoadMaterial(ctx, tracker);
                        else
                            self.CompleteMaterialAsFailure(ctx);
                    }
                });
                rpc.Failure((self, ctx, waitAllTracker, ex) =>
                {
                    --waitAllTracker.Tracker.NbRemaining;
                    waitAllTracker.Tracker.LatestException = ex;

                    if (waitAllTracker.Tracker.NbRemaining == 0)
                        self.CompleteMaterialAsFailure(ctx);
                });
            }
        }

        void LoadMaterial(RpcContext<AcquireUnityMaterial> ctx, Tracker tracker)
        {
            var (entry, trackers) = GetMaterialCommonData(ctx);
            var dependencies = tracker.TextureEntries.Select(x => x.Id).ToList();

            if (m_ResourceVersions.TryGetValue(entry.Id, out var rv))
            {
                var resource = rv.Versions.FirstOrDefault(x => x.Dependencies.SequenceEqual(dependencies));
                if (resource != null)
                {
                    var newResource = new Resource(resource.MainResource, ctx.Data.ManifestId, resource.DependencyManifestId, 1, false, resource.Textures, resource.Dependencies);
                    rv.Versions.Add(newResource);
                    ClearTemporaryResources(tracker);
                    ctx.SendSuccess(newResource.MainResource);
                    ProcessRemainingRequests(trackers);
                    return;
                }
            }
            
            ProcessMaterialConversion(ctx, tracker);
        }

        void CompleteMaterialAsFailure(RpcContext<AcquireUnityMaterial> ctx)
        {
            var trackers = GetMaterialCommonData(ctx).waiters;
            var tracker = trackers.Peek();

            ctx.SendFailure(tracker.LatestException);
            ClearTemporaryResources(tracker);
            ProcessRemainingRequests(trackers);
        }

        void ClearTemporaryResources(Tracker tracker)
        {
            if (tracker.SyncMaterial != null)
            {
                m_ReleaseModelResourceOutput.Send(new ReleaseModelResource(tracker.SyncMaterial));
                tracker.SyncMaterial = null;
            }
        }

        void ProcessMaterialConversion(RpcContext<AcquireUnityMaterial> ctx, Tracker tracker)
        {
            var rpc = m_ConvertSyncMaterialOutput.Call(this, ctx, tracker, new ConvertSyncMaterial(tracker.Ctx.Data.ResourceData, tracker.SyncMaterial, tracker.TextureEntries));
            rpc.Success<ConvertedResource<Texture2D>>((self, ctx, tracker, convertedResource) =>
            {
                var (entry, trackers) = self.GetMaterialCommonData(ctx);

                var resource = new Resource(convertedResource.MainResource, ctx.Data.ManifestId, ctx.Data.ManifestId, 1, true, convertedResource.Dependencies, tracker.TextureEntries.Select(x => x.Id).ToList());

                if (!self.m_ResourceVersions.TryGetValue(entry.Id, out var rv))
                {
                    rv = new ResourceVersions(entry.Id, new List<Resource>());
                    self.m_ResourceVersions.Add(entry.Id, rv);
                }

                rv.Versions.Add(resource);
                self.m_Materials.Add(resource.MainResource, rv);
                
                var pipe = self.m_MaterialCreatingOutput.Push(self, (object)null, new PendingContext(ctx, tracker, resource, trackers), new MaterialCreating(entry.Id, ctx.Data.ManifestId, resource.MainResource));
                pipe.Success((self, ctx, pendingCtx, data) =>
                {
                    var depRpc = self.m_UpdateEntryDependenciesOutput.Call(self, pendingCtx.Ctx, pendingCtx, new UpdateEntryDependencies(pendingCtx.Ctx.Data.ResourceData.Id, pendingCtx.Ctx.Data.ManifestId, pendingCtx.Resource.Dependencies));
                    depRpc.Success<NullData>((self, ctx, pendingCtx, _) =>
                    {
                        ctx.SendSuccess(pendingCtx.Resource.MainResource);
                        self.ClearTemporaryResources(pendingCtx.Tracker);
                        self.ProcessRemainingRequests(pendingCtx.Trackers);
                    });
                    depRpc.Failure((self, ctx, pendingCtx, ex) =>
                    {
                        ctx.SendSuccess(pendingCtx.Resource.MainResource);
                        self.ClearTemporaryResources(pendingCtx.Tracker);
                        self.ProcessRemainingRequests(pendingCtx.Trackers);
                        Debug.LogException(ex);
                    });
                });
                pipe.Failure((self, ctx, userCtx, ex) => Debug.LogException(ex));
            });

            rpc.Failure((self, ctx, tracker, ex) =>
            {
                var (entry, trackers) = self.GetMaterialCommonData(ctx);

                foreach (var t in trackers)
                    t.Ctx.SendFailure(ex);
                
                self.ClearTemporaryResources(tracker);
                self.m_MaterialWaiters.Remove(entry.Id);
            });
        }

        void ReleaseResources(Material material)
        {
            if (!m_Materials.TryGetValue(material, out var rv))
                return;

            // Use first item as if no other versions are loaded, it means that there will be only a single version
            var resource = rv.Versions.FirstOrDefault(x => ReferenceEquals(x.MainResource, material));
            AdjustRefCount(rv.Id, resource, -1, rv.Versions);
        }

        (EntryData entry, Queue<Tracker> waiters) GetMaterialCommonData(RpcContext<AcquireUnityMaterial> ctx)
        {
            return (ctx.Data.ResourceData, m_MaterialWaiters[ctx.Data.ResourceData.Id]);
        }

        bool CompleteIfStreamCanceled(RpcContext<AcquireUnityMaterial> ctx)
        {
            if (ctx.Data.Stream.IsCancelled)
            {
                ctx.SendSuccess(NullData.Null);
                return true;
            }

            return false;
        }

        static List<string> GetTextureIds(SyncMaterial syncMaterial)
        {
            var textureIds = new List<string>();

            GetTextureId(syncMaterial.AlbedoMap.TextureId.Value, textureIds);
            GetTextureId(syncMaterial.AlphaMap.TextureId.Value, textureIds);
            GetTextureId(syncMaterial.NormalMap.TextureId.Value, textureIds);
            GetTextureId(syncMaterial.CutoutMap.TextureId.Value, textureIds);
            GetTextureId(syncMaterial.GlossinessMap.TextureId.Value, textureIds);
            GetTextureId(syncMaterial.MetallicMap.TextureId.Value, textureIds);
            GetTextureId(syncMaterial.EmissionMap.TextureId.Value, textureIds);

            return textureIds;
        }

        static void GetTextureId(SyncId textureId, List<string> results)
        {
            if (textureId != SyncId.None)
                results.Add(textureId.Value);
        }

        class WaitAllTracker
        {
            public Tracker Tracker;
            public int Position;
        }

        class Tracker
        {
            public RpcContext<AcquireUnityMaterial> Ctx;
            public SyncMaterial SyncMaterial;
            public Exception LatestException;
            public int NbRemaining;
            public List<EntryData> TextureEntries;
        }

        class CountDownTracker<T>
        {
            public int NbRemainingRequests;
            public Dictionary<EntryGuid, T> DestroyingResources;

            public CountDownTracker(int nbRemainingRequests, Dictionary<EntryGuid, T> destroyingResources)
            {
                NbRemainingRequests = nbRemainingRequests;
                DestroyingResources = destroyingResources;
            }
        }

        class ResourceVersions
        {
            public EntryGuid Id;
            public List<Resource> Versions;

            public ResourceVersions(EntryGuid id, List<Resource> versions)
            {
                Id = id;
                Versions = versions;
            }
        }

        class Resource
        {
            public Material MainResource;
            public ManifestGuid ManifestId;
            public ManifestGuid DependencyManifestId;
            public int Count;
            public bool IsOriginal;
            public List<Texture2D> Textures;
            public List<EntryGuid> Dependencies;

            public Resource(Object mainResource, ManifestGuid manifestId, ManifestGuid dependencyManifestId, int count, bool isOriginal, List<Texture2D> textures, List<EntryGuid> dependencies)
            {
                MainResource = (Material)mainResource;
                ManifestId = manifestId;
                DependencyManifestId = dependencyManifestId;
                Count = count;
                IsOriginal = isOriginal;
                Textures = textures;
                Dependencies = dependencies;
            }
        }

        class PendingContext
        {
            public RpcContext<AcquireUnityMaterial> Ctx;
            public Tracker Tracker;
            public Resource Resource;
            public Queue<Tracker> Trackers;

            public PendingContext(RpcContext<AcquireUnityMaterial> ctx, Tracker tracker, Resource resource, Queue<Tracker> trackers)
            {
                Ctx = ctx;
                Tracker = tracker;
                Resource = resource;
                Trackers = trackers;
            }
        }

        class DestroyJobInputDic<T> : DestroyJobInput<Dictionary<EntryGuid, T>>
        {
            public DestroyJobInputDic(Dictionary<EntryGuid, T> data)
                : base(data)
            {
            }
        }

        class DestroyJobInput<TData>
        {
            public TData Data;

            public DestroyJobInput(TData data)
            {
                Data = data;
            }
        }
    }
}
