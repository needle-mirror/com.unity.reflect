using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.Actor;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Streaming
{
    [Actor]
    public class GameObjectConverterActor
    {
#pragma warning disable 649
        NetOutput<ReleaseUnityResource> m_ReleaseUnityResourceOutput;
        NetOutput<ReleaseEntryData> m_ReleaseEntryDataOutput;

        RpcOutput<AcquireEntryDataFromModelData> m_AcquireEntryDataFromModelOutput;
        RpcOutput<AcquireUnityResource> m_AcquireUnityResourceOutput;
        RpcOutput<BuildGameObject> m_BuildGameObjectOutput;
#pragma warning restore 649

        [RpcInput]
        void OnConvertToGameObject(RpcContext<ConvertToGameObject> ctx)
        {
            if (ctx.Data.Stream.IsCancelled)
            {
                ctx.SendSuccess<GameObject>(null);
                return;
            }

            var (meshIds, materialIds) = FindResourceIds(ctx);

            var tracker = new Tracker
            {
                Ctx = ctx,
                Entries = new List<EntryData>(Enumerable.Repeat((EntryData)null, meshIds.Count + materialIds.Count)),
                Meshes = new List<Mesh>(Enumerable.Repeat((Mesh)null, meshIds.Count)),
                Materials = new List<Material>(Enumerable.Repeat((Material)null, materialIds.Count))
            };

            AcquireSyncObjectResources(tracker, meshIds, 0);
            AcquireSyncObjectResources(tracker, materialIds, meshIds.Count);
        }

        static (List<PersistentKey> meshIds, List<PersistentKey> materialIds) FindResourceIds(RpcContext<ConvertToGameObject> ctx)
        {
            var meshIds = new List<PersistentKey>();
            var materialIds = new List<PersistentKey>();
            var stack = new Stack<SyncObject>();
            stack.Push(ctx.Data.SyncObject);
            while (stack.Count > 0)
            {
                var syncObject = stack.Pop();
                if (syncObject.MeshId != SyncId.None)
                    meshIds.Add(new PersistentKey(typeof(SyncMesh), syncObject.MeshId.Value));

                if (syncObject.MaterialIds != null)
                    materialIds.AddRange(syncObject.MaterialIds.Where(x => x != SyncId.None).Select(x => new PersistentKey(typeof(SyncMaterial), x.Value)));

                foreach(var child in syncObject.Children)
                    stack.Push(child);
            }

            return (meshIds, materialIds);
        }

        void AcquireSyncObjectResources(Tracker tracker, List<PersistentKey> resourceIds, int startIndex)
        {
            // Acquire EntryData for each resource (meshes and materials)
            var count = resourceIds.Count;
            for (var i = 0; i < count; ++i)
            {
                ++tracker.NbPendingCalls;

                var instanceData = tracker.Ctx.Data.InstanceData;
                var waitAllTracker = new WaitAllTracker<object> { Tracker = tracker, Position = startIndex + i };

                var rpc = m_AcquireEntryDataFromModelOutput.Call(this, tracker.Ctx, waitAllTracker, new AcquireEntryDataFromModelData(instanceData.ManifestId, resourceIds[i]));
                rpc.Success<EntryData>((self, ctx, waitAllTracker, entry) =>
                {
                    var tracker = waitAllTracker.Tracker;
                    tracker.Entries[waitAllTracker.Position] = entry;
                    --tracker.NbPendingCalls;

                    if (tracker.NbPendingCalls == 0)
                    {
                        if (tracker.LatestException == null)
                            self.AcquireFinalResources(tracker);
                        else
                        {
                            self.ClearAcquiredResources(tracker);
                            ctx.SendFailure(tracker.LatestException);
                        }
                    }
                });
                rpc.Failure((self, ctx, waitAllTracker, ex) =>
                {
                    var tracker = waitAllTracker.Tracker;
                    tracker.LatestException = ex;
                    --tracker.NbPendingCalls;

                    if (tracker.NbPendingCalls == 0)
                    {
                        self.ClearAcquiredResources(tracker);
                        ctx.SendFailure(ex);
                    }
                });
            }
        }

        void AcquireFinalResources(Tracker tracker)
        {
            AcquireUnityResource(tracker, tracker.Meshes, 0);
            AcquireUnityResource(tracker, tracker.Materials, tracker.Meshes.Count);
        }

        void AcquireUnityResource<TUnityResource>(Tracker tracker, List<TUnityResource> results, int startIndex)
            where TUnityResource : class
        {
            var entries = tracker.Entries.GetRange(startIndex, results.Count);

            for (var i = 0; i < entries.Count; ++i)
            {
                var entry = entries[i];
                ++tracker.NbPendingCalls;
                var waitAllTracker = new WaitAllTracker<TUnityResource> { Tracker = tracker, Results = results, Position = i };
                
                var rpc = m_AcquireUnityResourceOutput.Call(this, tracker.Ctx, waitAllTracker, new AcquireUnityResource(tracker.Ctx.Data.Stream, entry));
                rpc.Success<TUnityResource>((self, ctx, waitAllTracker, mesh) =>
                {
                    var tracker = waitAllTracker.Tracker;
                    results[waitAllTracker.Position] = mesh;
                    --tracker.NbPendingCalls;

                    if (tracker.NbPendingCalls == 0)
                    {
                        if (tracker.LatestException == null)
                            self.BuildAndSendResponse(tracker);
                        else
                        {
                            self.ClearAcquiredResources(tracker);
                            ctx.SendFailure(tracker.LatestException);
                        }
                    }
                });
                rpc.Failure((self, ctx, rpcTracker, ex) =>
                {
                    var tracker = rpcTracker.Tracker;
                    tracker.LatestException = ex;
                    --tracker.NbPendingCalls;

                    if (tracker.NbPendingCalls == 0)
                    {
                        self.ClearAcquiredResources(tracker);
                        ctx.SendFailure(ex);
                    }
                });
            }
        }

        void BuildAndSendResponse(Tracker tracker)
        {
            if (tracker.Ctx.Data.Stream.IsCancelled)
            {
                ClearAcquiredResources(tracker);
                tracker.Ctx.SendSuccess<GameObject>(null);
                return;
            }

            var meshes = new Dictionary<StreamKey, Mesh>();
            for (var i = 0; i < tracker.Meshes.Count; ++i)
            {
                var entry = tracker.Entries[i];
                var streamKey = new StreamKey(entry.SourceId, new PersistentKey(entry.EntryType, entry.IdInSource));
                meshes[streamKey] = tracker.Meshes[i];
            }

            var materials = new Dictionary<StreamKey, Material>();
            for (var i = 0; i < tracker.Materials.Count; ++i)
            {
                var entry = tracker.Entries[tracker.Meshes.Count + i];
                var streamKey = new StreamKey(entry.SourceId, new PersistentKey(entry.EntryType, entry.IdInSource));
                materials[streamKey] = tracker.Materials[i];
            }

            var data = tracker.Ctx.Data;

            var buildGameObject = new BuildGameObject(data.Stream, tracker.Ctx.Data.InstanceData, data.SyncInstance, data.SyncObject, meshes, materials);
            var rpc = m_BuildGameObjectOutput.Call(this, tracker.Ctx, tracker, buildGameObject);

            rpc.Success<GameObject>((self, ctx, tracker, gameObject) =>
            {
                if (tracker.Ctx.Data.Stream.IsCancelled && gameObject == null)
                    ClearAcquiredResources(tracker);

                ctx.SendSuccess(gameObject);
            });

            rpc.Failure((self, ctx, tracker, ex) =>
            {
                ClearAcquiredResources(tracker);
                ctx.SendFailure(ex);
            });
        }

        void ClearAcquiredResources(Tracker tracker)
        {
            for (var i = 0; i < tracker.Meshes.Count; ++i)
            {
                if (tracker.Meshes[i] != null)
                    m_ReleaseUnityResourceOutput.Send(new ReleaseUnityResource(tracker.Entries[i].Id));
            }

            for (var i = 0; i < tracker.Materials.Count; ++i)
            {
                if (tracker.Materials[i] != null)
                    m_ReleaseUnityResourceOutput.Send(new ReleaseUnityResource(tracker.Entries[tracker.Meshes.Count + i].Id));
            }

            foreach (var entry in tracker.Entries)
            {
                if (entry != null)
                    m_ReleaseEntryDataOutput.Send(new ReleaseEntryData(entry.Id));
            }
        }

        class WaitAllTracker<T>
        {
            public Tracker Tracker;
            public List<T> Results;
            public int Position;
        }

        class Tracker
        {
            public RpcContext<ConvertToGameObject> Ctx;
            public List<EntryData> Entries;
            public List<Mesh> Meshes;
            public List<Material> Materials;
            public Exception LatestException;
            public int NbPendingCalls;
        }
    }
}
