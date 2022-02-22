using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Data;
using Unity.Reflect.Geometry;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Actors
{
    [Actor("82b2c3f3-df19-4b3f-96b5-4a2ff73dce47")]
    public class ManifestActor
    {
#pragma warning disable 649
        EventOutput<GlobalBoundsUpdated> m_GlobalBoundsUpdatedOutput;
        
        NetOutput<EntryDataChanged> m_EntryDataChangedOutput;
        NetOutput<PreShutdown> m_PreShutdownOutput;
        NetOutput<SyncNodesAdded> m_SyncNodesAddedOutput;
        NetOutput<DynamicEntryChanged> m_DynamicEntryChangedOutput;
        
        RpcOutput<GetManifests> m_GetManifestsOutput;
        [RpcOutput(optional:true)]
        RpcOutput<PatchEntryIds> m_PatchEntryIdsOutput;
        [RpcOutput(optional:true)]
        RpcOutput<GetSpatialManifest, SpatialManifest> m_GetSpatialManifestOutput;
        
#pragma warning restore 649

        List<UpdateTracker> m_UpdateTrackers = new List<UpdateTracker>();

        Dictionary<string, Manifest> m_SourceIdToManifests = new Dictionary<string, Manifest>();
        Dictionary<ManifestGuid, Manifest> m_IdToManifests = new Dictionary<ManifestGuid, Manifest>();
        Dictionary<EntryGuid, EntryData> m_EntryIdToInfos = new Dictionary<EntryGuid, EntryData>();
        
        DynamicEntryHandler m_DynamicEntryHandler = new DynamicEntryHandler();
        
        Bounds m_GlobalBounds;

        public static Dictionary<string, Type> SyncModelTypes = new Dictionary<string, Type>
        {
            { "SyncProjectTask", typeof(SyncProjectTask) },
            { "SyncObject", typeof(SyncObject) },
            { "SyncObjectInstance", typeof(SyncObjectInstance) },
            { "SyncTexture", typeof(SyncTexture) },
            { "SyncMaterial", typeof(SyncMaterial) },
            { "SyncMesh", typeof(SyncMesh) },
            { "SyncNode", typeof(SyncNode) },
            { "SyncMarker", typeof(SyncMarker) },
            { "SyncTransformOverride", typeof(SyncTransformOverride) }
        };

        public static HashSet<Type> SpatializedSyncModelTypes = new HashSet<Type>
        {
            typeof(SyncObject),
            typeof(SyncObjectInstance),
            typeof(SyncTexture),
            typeof(SyncMaterial),
            typeof(SyncMesh),
            typeof(SyncNode)
        };

        public void Inject(ActorHandle selfHandle)
        {
            m_GlobalBounds.size = Vector3.zero;
        }

        [NetInput]
        void OnPreShutdown(NetContext<PreShutdown> _)
        {
            m_PreShutdownOutput.Send(new PreShutdown());
        }

        [RpcInput]
        void OnGetManifests(RpcContext<GetManifests> ctx)
        {
            m_UpdateTrackers.Add(new UpdateTracker{ Ctx = ctx });

            if (m_UpdateTrackers.Count > 1)
                return;

            GetManifests();
        }
        
        [RpcInput]
        void OnUpdateEntryDependencies(RpcContext<UpdateEntryDependencies> ctx)
        {
            m_DynamicEntryHandler.OnUpdateEntryDependencies(ctx, m_DynamicEntryChangedOutput);
        }

        [NetInput]
        void OnEntryDataChanged(NetContext<EntryDataChanged> ctx)
        {
            m_DynamicEntryHandler.OnEntryDataChanged(ctx, m_DynamicEntryChangedOutput);
        }
        
        [RpcInput]
        void OnAcquireDynamicEntry(RpcContext<AcquireDynamicEntry> ctx)
        {
            m_DynamicEntryHandler.OnAcquireDynamicEntry(ctx);
        }

        [RpcInput]
        void OnGetDynamicIds(RpcContext<GetDynamicIds> ctx)
        {
            m_DynamicEntryHandler.OnGetDynamicIds(ctx);
        }

        [RpcInput]
        void OnGetStableId(RpcContext<GetStableId<DynamicGuid>> ctx)
        {
            m_DynamicEntryHandler.OnGetStableId(ctx);
        }

        
        [RpcInput]
        void OnAcquireEntryData(RpcContext<AcquireEntryData> ctx)
        {
            ctx.SendSuccess(m_EntryIdToInfos[ctx.Data.EntryId]);
        }
        
        [RpcInput]
        void OnAcquireEntryDataFromModelData(RpcContext<AcquireEntryDataFromModelData> ctx)
        {
            var manifest = m_IdToManifests[ctx.Data.ManifestId];

            var index = manifest.VersionIds.IndexOf(ctx.Data.ManifestId);

            for (var i = index; i >= 0; --i)
            {
                var delta = manifest.Deltas[i];
                if (TryGetEntryData(delta, ctx.Data.IdInSource, out var entry))
                {
                    ctx.SendSuccess(entry);
                    return;
                }
            }

            throw new Exception($"Entry {ctx.Data.IdInSource} does not exist.");
        }

        [RpcInput]
        void OnGetStableId(RpcContext<GetStableId<StreamKey>> ctx)
        {
            var key = ctx.Data.Key;
            var manifest = m_SourceIdToManifests[key.source];
            var entry = manifest.Current[key.key];
            ctx.SendSuccess(new Boxed<EntryStableGuid>(entry.StableId));
        }


        [NetInput]
        void OnRemoveAllManifestEntries(NetContext<RemoveAllManifestEntries> ctx)
        {
            var delta = new Delta<EntryData>();
            delta.Removed.AddRange(m_SourceIdToManifests.Values
                .SelectMany(x => x.Current)
                .Select(x => x.Value));

            m_EntryDataChangedOutput.Send(new EntryDataChanged(delta, new List<NewManifest>()));
        }

        [PipeInput]
        void OnClearBeforeSyncUpdate(PipeContext<ClearBeforeSyncUpdate> ctx)
        {
            m_SourceIdToManifests.Clear();
            m_IdToManifests.Clear();
            m_EntryIdToInfos.Clear();
            m_GlobalBounds.size = Vector3.zero;

            ctx.Continue();
        }

        [RpcInput]
        void OnGetSpatialManifest(RpcContext<GetSpatialManifest, string> ctx)
        {
            var rpc = m_GetSpatialManifestOutput.Call(this, ctx, (object) null, new GetSpatialManifest(ctx.Data.GetNodesOptions, ctx.Data.NodeIds));
            rpc.Success((self, ctx, tracker, spatialManifest) =>
            {
                var mergedDelta = new Delta<EntryData>();
                var newManifests = new List<NewManifest>();
                var hasSyncNodes = spatialManifest.SyncNodes != null && spatialManifest.SyncNodes.Count > 0;

                // process nodes
                var newSyncNodes = new List<SyncNode>();
                if (hasSyncNodes)
                {
                    if (!self.m_SourceIdToManifests.TryGetValue(spatialManifest.HlodSourceId, out var syncNodeManifest))
                    {
                        syncNodeManifest = new Manifest();
                        self.m_SourceIdToManifests.Add(spatialManifest.HlodSourceId, syncNodeManifest);
                    }

                    var newSyncNodeManifestId = ManifestGuid.NewGuid();
                    newManifests.Add(new NewManifest(syncNodeManifest.StableId, newSyncNodeManifestId));

                    var delta = new Delta<EntryData>();
                    foreach (var syncNode in spatialManifest.SyncNodes)
                    {
                        var key = PersistentKey.GetKey<SyncNode>(syncNode.Id);

                        if (syncNodeManifest.Current.ContainsKey(key))
                            continue;

                        newSyncNodes.Add(syncNode);
                            
                        // TODO: include real hash?
                        var entry = new EntryData(EntryGuid.NewGuid(), EntryStableGuid.NewGuid(), spatialManifest.HlodSourceId, newSyncNodeManifestId, syncNodeManifest.StableId, "", typeof(SyncNode), key, null);

                        if (syncNode.BoundingBox.initialized && 
                            syncNode.BoundingBox.Min != syncNode.BoundingBox.Max)
                        {
                            entry.Spatial = new SpatialData(new Aabb(syncNode.BoundingBox.Min, syncNode.BoundingBox.Max, Aabb.FromMinMaxTag));
                            EncapsulateGlobalBounds(syncNode.BoundingBox.Min.ToUnity(), syncNode.BoundingBox.Max.ToUnity());
                        }
                    
                        delta.Added.Add(entry);
                    }

                    self.m_IdToManifests.Add(newSyncNodeManifestId, syncNodeManifest);
                    syncNodeManifest.VersionIds.Add(newSyncNodeManifestId);
                    syncNodeManifest.Deltas.Add(new HashDelta<PersistentKey, EntryData>
                    {
                        Added = delta.Added.ToDictionary(x => x.IdInSource, x => x), 
                        Removed = delta.Removed.ToDictionary(x => x.IdInSource, x => x), 
                        Changed = new Dictionary<PersistentKey, (EntryData Prev, EntryData Next)>()
                    });
                    mergedDelta.Added.AddRange(delta.Added);
                    mergedDelta.Removed.AddRange(delta.Removed);
                    MergeLatestDeltaInCurrent(syncNodeManifest);
                }
                
                // process manifests
                foreach (var manifest in spatialManifest.SyncManifests)
                {
                    if (!self.m_SourceIdToManifests.TryGetValue(manifest.SourceId, out var currentManifest))
                    {
                        currentManifest = new Manifest();
                        self.m_SourceIdToManifests.Add(manifest.SourceId, currentManifest);
                    }

                    var newManifestId = ManifestGuid.NewGuid();
                    newManifests.Add(new NewManifest(currentManifest.StableId, newManifestId));

                    var delta = new Delta<EntryData>();
                    foreach (var kv in manifest.Content)
                    {
                        var persistentKey = kv.Key;

                        if (currentManifest.Current.ContainsKey(persistentKey)) 
                            continue;
                        
                        var entry = new EntryData(EntryGuid.NewGuid(), EntryStableGuid.NewGuid(), manifest.SourceId, newManifestId, currentManifest.StableId, kv.Value.Hash, SyncModelTypes[persistentKey.TypeName], new PersistentKey(SyncModelTypes[persistentKey.TypeName], persistentKey.Name), null);

                        if (kv.Value.BoundingBox.initialized &&
                            entry.EntryType == typeof(SyncObjectInstance) &&
                            kv.Value.BoundingBox.Min != kv.Value.BoundingBox.Max)
                        {
                            entry.Spatial = new SpatialData(new Aabb(kv.Value.BoundingBox.Min, kv.Value.BoundingBox.Max, Aabb.FromMinMaxTag));
                            EncapsulateGlobalBounds(kv.Value.BoundingBox.Min.ToUnity(), kv.Value.BoundingBox.Max.ToUnity());
                        }

                        delta.Added.Add(entry);
                    }

                    if (delta.IsEmpty())
                        continue;

                    self.m_IdToManifests.Add(newManifestId, currentManifest);
                    currentManifest.VersionIds.Add(newManifestId);
                    currentManifest.Deltas.Add(new HashDelta<PersistentKey, EntryData>
                    {
                        Added = delta.Added.ToDictionary(x => x.IdInSource, x => x), 
                        Removed = delta.Removed.ToDictionary(x => x.IdInSource, x => x), 
                        Changed = new Dictionary<PersistentKey, (EntryData Prev, EntryData Next)>()
                    });
                    mergedDelta.Added.AddRange(delta.Added);
                    mergedDelta.Removed.AddRange(delta.Removed);
                    MergeLatestDeltaInCurrent(currentManifest);
                }

                var patchRpc = m_PatchEntryIdsOutput.Call(this, ctx, mergedDelta, new PatchEntryIds(mergedDelta));
                patchRpc.Success((self, ctx, mergedDelta, res) =>
                {
                    self.UpdateEntryIdToInfos(mergedDelta);
                
                    self.m_GlobalBoundsUpdatedOutput.Broadcast(new GlobalBoundsUpdated(self.m_GlobalBounds));
                    self.m_EntryDataChangedOutput.Send(new EntryDataChanged(mergedDelta, newManifests));
                    self.m_SyncNodesAddedOutput.Send(new SyncNodesAdded(newSyncNodes, spatialManifest.HlodSourceId));
                
                    ctx.SendSuccess(hasSyncNodes ? spatialManifest.VersionId : "");
                });
                patchRpc.Failure((self, ctx, userCtx, ex) => ctx.SendFailure(ex));

            });
            rpc.Failure((self, ctx, tracker, ex) => ctx.SendFailure(ex));
        }

        void GetManifests()
        {
            var ctx = m_UpdateTrackers[0].Ctx;

            var rpc = m_GetManifestsOutput.Call(this, ctx, (object)null, ctx.Data);
            rpc.Success<List<SyncManifest>>((self, ctx, userCtx, manifests) =>
            {
                var mergedDelta = new Delta<EntryData>();
                var newManifests = new List<NewManifest>();
                var hasSyncNodes = false;
                foreach (var manifest in manifests)
                {
                    if (!self.m_SourceIdToManifests.TryGetValue(manifest.SourceId, out var currentManifest))
                    {
                        currentManifest = new Manifest();
                        self.m_SourceIdToManifests.Add(manifest.SourceId, currentManifest);
                    }

                    var newManifestId = ManifestGuid.NewGuid();
                    newManifests.Add(new NewManifest(currentManifest.StableId, newManifestId));
                    var delta = self.ComputeDelta(manifest.SourceId, newManifestId, currentManifest, manifest.Content.ToDictionary(x => x.Key, x => x.Value), ctx.Data.GetManifestOptions.IncludeSpatializedModels);

                    if (delta.IsEmpty())
                        continue;

                    self.m_IdToManifests.Add(newManifestId, currentManifest);

                    currentManifest.VersionIds.Add(newManifestId);
                    currentManifest.Deltas.Add(new HashDelta<PersistentKey, EntryData>
                    {
                        Added = delta.Added.ToDictionary(x => x.IdInSource, x => x),
                        Removed = delta.Removed.ToDictionary(x => x.IdInSource, x => x),
                        Changed = delta.Changed.ToDictionary(x => x.Next.IdInSource, x => (x.Prev, x.Next))
                    });

                    mergedDelta.Added.AddRange(delta.Added);
                    mergedDelta.Removed.AddRange(delta.Removed);
                    mergedDelta.Changed.AddRange(delta.Changed);

                    MergeLatestDeltaInCurrent(currentManifest);

                    hasSyncNodes |= currentManifest.Current.Values.Any(x => x.EntryType == typeof(SyncNode));
                }

                var patchRpc = m_PatchEntryIdsOutput.Call(this, ctx, mergedDelta, new PatchEntryIds(mergedDelta));
                patchRpc.Success((self, ctx, mergedDelta, res) =>
                {
                    self.UpdateEntryIdToInfos(mergedDelta);

                    self.m_GlobalBoundsUpdatedOutput.Broadcast(new GlobalBoundsUpdated(self.m_GlobalBounds));
                    
                    var data = new EntryDataChanged(mergedDelta, newManifests);
                    self.m_EntryDataChangedOutput.Send(data);
                    self.m_DynamicEntryHandler.OnEntryDataChanged(data, self.m_DynamicEntryChangedOutput);

                    self.m_UpdateTrackers[0].Ctx.SendSuccess(NullData.Null);
                    self.m_UpdateTrackers.RemoveAt(0);

                    if (self.m_UpdateTrackers.Count > 0)
                        self.GetManifests();
                });
                patchRpc.Failure((self, ctx, mergedDelta, ex) =>
                {
                    self.m_UpdateTrackers[0].Ctx.SendFailure(ex);
                    self.m_UpdateTrackers.RemoveAt(0);

                    if (self.m_UpdateTrackers.Count > 0)
                        self.GetManifests();
                });
            });

            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                self.m_UpdateTrackers[0].Ctx.SendFailure(ex);
                self.m_UpdateTrackers.RemoveAt(0);

                if (self.m_UpdateTrackers.Count > 0)
                    self.GetManifests();
            });
        }

        Delta<EntryData> ComputeDelta(string sourceId, ManifestGuid newManifestId, Manifest currentManifest, Dictionary<PersistentKey, ManifestEntry> newManifest, bool isSpatializedDelta)
        {
            var delta = new Delta<EntryData>();
            foreach (var kv in currentManifest.Current)
            {
                var persistentKey = kv.Key;

                if (newManifest.TryGetValue(persistentKey, out var newData))
                {
                    if (!Compare(kv.Value, newData))
                    {
                        var entry = new EntryData(EntryGuid.NewGuid(), kv.Value.StableId, sourceId, newManifestId, currentManifest.StableId, newData.Hash, kv.Value.EntryType, kv.Value.IdInSource, null);

                        if (newData.BoundingBox.initialized &&
                            (entry.EntryType == typeof(SyncObjectInstance) || 
                            entry.EntryType == typeof(SyncNode)) &&
                            newData.BoundingBox.Min != newData.BoundingBox.Max)
                        {
                            entry.Spatial = new SpatialData(new Aabb(newData.BoundingBox.Min, newData.BoundingBox.Max, Aabb.FromMinMaxTag));
                            EncapsulateGlobalBounds(newData.BoundingBox.Min.ToUnity(), newData.BoundingBox.Max.ToUnity());
                        }

                        delta.Changed.Add((kv.Value, entry));
                    }
                }
                else
                {
                    if (isSpatializedDelta || !SpatializedSyncModelTypes.Contains(kv.Value.EntryType))
                        delta.Removed.Add(kv.Value);
                }
            }

            foreach (var kv in newManifest)
            {
                var persistentKey = kv.Key;

                if (!currentManifest.Current.ContainsKey(persistentKey))
                {
                    var entry = new EntryData(EntryGuid.NewGuid(), EntryStableGuid.NewGuid(), sourceId, newManifestId, currentManifest.StableId, kv.Value.Hash, SyncModelTypes[persistentKey.TypeName], new PersistentKey(SyncModelTypes[persistentKey.TypeName], persistentKey.Name), null);

                    if (kv.Value.BoundingBox.initialized &&
                        (entry.EntryType == typeof(SyncObjectInstance) || 
                         entry.EntryType == typeof(SyncNode)) &&
                        kv.Value.BoundingBox.Min != kv.Value.BoundingBox.Max)
                    {
                        entry.Spatial = new SpatialData(new Aabb(kv.Value.BoundingBox.Min, kv.Value.BoundingBox.Max, Aabb.FromMinMaxTag));
                        EncapsulateGlobalBounds(kv.Value.BoundingBox.Min.ToUnity(), kv.Value.BoundingBox.Max.ToUnity());
                    }

                    delta.Added.Add(entry);
                }
            }

            return delta;
        }

        void UpdateEntryIdToInfos(Delta<EntryData> delta)
        {
            foreach (var added in delta.Added)
                m_EntryIdToInfos.Add(added.Id, added);

            foreach (var changed in delta.Changed)
                m_EntryIdToInfos.Add(changed.Next.Id, changed.Next);
        }

        static bool TryGetEntryData(HashDelta<PersistentKey, EntryData> delta, PersistentKey key, out EntryData entry)
        {
            entry = null;

            if (delta.Added.TryGetValue(key, out var added))
            {
                entry = added;
                return true;
            }

            if (delta.Changed.TryGetValue(key, out var changed))
            {
                entry = changed.Next;
                return true;
            }

            if (delta.Removed.TryGetValue(key, out _))
                throw new Exception($"Requested {nameof(EntryData)} has been removed.");

            return false;
        }

        static bool Compare(EntryData prev, ManifestEntry next)
        {
            var box = new Aabb(next.BoundingBox.Min, next.BoundingBox.Max, Aabb.FromMinMaxTag);
            return prev.Hash == next.Hash && (
                prev.Spatial == null && next.BoundingBox.initialized == false ||
                prev.Spatial != null && prev.Spatial.Box == box);
        }

        static void MergeLatestDeltaInCurrent(Manifest manifest)
        {
            var index = manifest.Deltas.Count - 1;

            foreach (var kv in manifest.Deltas[index].Added)
                manifest.Current.Add(kv.Key, kv.Value);

            foreach (var kv in manifest.Deltas[index].Removed)
                manifest.Current.Remove(kv.Key);

            foreach (var kv in manifest.Deltas[index].Changed)
                manifest.Current[kv.Key] = kv.Value.Next;
        }

        void EncapsulateGlobalBounds(Vector3 min, Vector3 max)
        {
            if (min.Equals(max))
                return;

            if (!m_GlobalBounds.size.Equals(Vector3.zero))
            {
                m_GlobalBounds.Encapsulate(min);
                m_GlobalBounds.Encapsulate(max);
                return;
            }

            m_GlobalBounds.SetMinMax(min, max);
        }

        struct UpdateTracker
        {
            public RpcContext<GetManifests> Ctx;
        }

        class Manifest
        {
            public ManifestStableGuid StableId = ManifestStableGuid.NewGuid();
            public List<ManifestGuid> VersionIds = new List<ManifestGuid>();
            public List<HashDelta<PersistentKey, EntryData>> Deltas = new List<HashDelta<PersistentKey, EntryData>>();
            public Dictionary<PersistentKey, EntryData> Current = new Dictionary<PersistentKey, EntryData>();
        }
    }

    public class EntryData
    {
        public EntryGuid Id;
        public EntryStableGuid StableId;
        public string SourceId;
        public ManifestGuid ManifestId;
        public ManifestStableGuid ManifestStableId;
        public string Hash;
        public Type EntryType;
        public PersistentKey IdInSource;

        public SpatialData Spatial;

        public EntryData(EntryGuid id, EntryStableGuid stableId, string sourceId, ManifestGuid manifestId, ManifestStableGuid manifestStableId, string hash, Type entryType, PersistentKey idInSource, SpatialData spatial)
        {
            Id = id;
            StableId = stableId;
            SourceId = sourceId;
            ManifestId = manifestId;
            ManifestStableId = manifestStableId;
            Hash = hash;
            EntryType = entryType;
            IdInSource = idInSource;
            Spatial = spatial;
        }
    }

    public class SpatialData
    {
        public Aabb Box;

        public SpatialData(Aabb box)
        {
            Box = box;
        }
    }
}
