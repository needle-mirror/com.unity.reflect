using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Reflect.Actor;
using Unity.Reflect.Data;
using Unity.Reflect.Geometry;
using Unity.Reflect.Model;

namespace Unity.Reflect.Streaming
{
    [Actor]
    public class ManifestActor
    {
#pragma warning disable 649
        RpcOutput<GetManifests> m_GetManifestsOutput;
        NetOutput<EntryDataChanged> m_EntryDataChangedOutput;
        NetOutput<SpatialDataChanged> m_SpatialDataChangedOutput;
#pragma warning restore 649

        CancellationToken m_Token;

        List<UpdateTracker> m_UpdateTrackers = new List<UpdateTracker>();
        
        Dictionary<Guid, Dictionary<PersistentKey, EntryData>> m_LoadedManifests = new Dictionary<Guid, Dictionary<PersistentKey, EntryData>>();
        Dictionary<Guid, EntryData> m_EntryIdToInfos = new Dictionary<Guid, EntryData>();
        Dictionary<string, Type> m_SyncModelTypes = new Dictionary<string, Type>
        {
            { "SyncObject", typeof(SyncObject) },
            { "SyncObjectInstance", typeof(SyncObjectInstance) },
            { "SyncTexture", typeof(SyncTexture) },
            { "SyncMaterial", typeof(SyncMaterial) },
            { "SyncMesh", typeof(SyncMesh) },
        };

        [RpcInput]
        void OnUpdateManifests(RpcContext<UpdateManifests> ctx)
        {
            m_UpdateTrackers.Add(new UpdateTracker{ Ctx = ctx });

            if (m_UpdateTrackers.Count > 1)
                return;

            var rpc = m_GetManifestsOutput.Call(this, ctx, (object)null, new GetManifests());
            rpc.Success<List<SyncManifest>>((self, ctx, userCtx, manifests) =>
            {
                var addedEntries = new List<EntryData>(manifests.Sum(x => x.Content.Count));
                var addedSpatialEntries = new List<EntryData>();
                foreach (var manifest in manifests)
                {
                    self.m_Token.ThrowIfCancellationRequested();

                    var manifestId = Guid.NewGuid();
                    var newManifest = new Dictionary<PersistentKey, EntryData>(manifest.Content.Count);
                    self.m_LoadedManifests.Add(manifestId, newManifest);

                    foreach (var kv in manifest.Content)
                    {
                        var entryId = Guid.NewGuid();
                        var type = m_SyncModelTypes[kv.Key.TypeName];
                        var entryInfo = new EntryData(entryId, manifest.SourceId, manifestId, kv.Value.Hash, type, kv.Key.Name);

                        var box = kv.Value.BoundingBox;
                        if (box.initialized && type == typeof(SyncObjectInstance))
                        {
                            entryInfo.Spatial = new SpatialData(new AABB(box.Min, box.Max));
                            addedSpatialEntries.Add(entryInfo);
                        }

                        self.m_EntryIdToInfos.Add(entryId, entryInfo);
                        newManifest.Add(kv.Key, entryInfo);
                        addedEntries.Add(entryInfo);
                    }
                }

                self.m_EntryDataChangedOutput.Send(new EntryDataChanged(addedEntries, new List<EntryData>(), new List<EntryDataChanged.ModifiedEntry>()));
                self.m_SpatialDataChangedOutput.Send(new SpatialDataChanged(addedSpatialEntries, new List<EntryData>(), new List<SpatialDataChanged.ModifiedEntry>()));

                // Copy the dictionary so there is no race condition on future accesses
                foreach (var tracker in self.m_UpdateTrackers)
                    tracker.Ctx.SendSuccess(NullData.Null);
                self.m_UpdateTrackers.Clear();
            });

            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                foreach (var tracker in self.m_UpdateTrackers)
                    tracker.Ctx.SendFailure(ex);
                self.m_UpdateTrackers.Clear();
            });
        }
        
        [RpcInput]
        void OnAcquireEntryData(RpcContext<AcquireEntryData> ctx)
        {
            ctx.SendSuccess(m_EntryIdToInfos[ctx.Data.EntryId]);
        }
        
        [RpcInput]
        void OnAcquireEntryDataFromModelData(RpcContext<AcquireEntryDataFromModelData> ctx)
        {
            var manifest = m_LoadedManifests[ctx.Data.ManifestId];
            var entry = manifest[ctx.Data.IdInSource];
            ctx.SendSuccess(entry);
        }
        
        [NetInput]
        void OnReleaseEntryData(NetContext<ReleaseEntryData> ctx)
        {
            // Todo: When we will track with reference counting the EntryData to be able to flush them from memory (partial manifest in memory)
        }

        struct UpdateTracker
        {
            public RpcContext<UpdateManifests> Ctx;
        }
    }

    public class EntryData
    {
        public Guid Id;
        public string SourceId;
        public Guid ManifestId;
        public string Hash;
        public Type EntryType;
        public string IdInSource;

        public SpatialData Spatial;

        public EntryData(Guid id, string sourceId, Guid manifestId, string hash, Type entryType, string idInSource)
        {
            Id = id;
            SourceId = sourceId;
            ManifestId = manifestId;
            Hash = hash;
            EntryType = entryType;
            IdInSource = idInSource;
        }
    }

    public class SpatialData
    {
        public AABB Box;

        public SpatialData(AABB box)
        {
            Box = box;
        }
    }
}
