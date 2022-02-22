using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    [Actor("64f6f0fd-8102-4ee6-8267-ced07a7a5d10")]
    public class GameObjectConverterActor
    {
#pragma warning disable 649
        NetOutput<ReleaseUnityMesh> m_ReleaseUnityMeshOutput;
        NetOutput<ReleaseUnityMaterial> m_ReleaseUnityMaterialOutput;
        NetOutput<SetGameObjectDependencies> m_SetGameObjectDependenciesOutput;

        RpcOutput<GetProjectWideDefaultMaterial> m_GetProjectWideDefaultMaterialOutput;
        RpcOutput<AcquireEntryDataFromModelData> m_AcquireEntryDataFromModelOutput;
        RpcOutput<AcquireUnityMesh> m_AcquireUnityMeshOutput;
        RpcOutput<AcquireUnityMaterial> m_AcquireUnityMaterialOutput;
        RpcOutput<BuildGameObject> m_BuildGameObjectOutput;
        [RpcOutput(optional:true)]
        RpcOutput<UpdateEntryDependencies> m_UpdateEntryDependenciesOutput;
#pragma warning restore 649

        bool m_IsWaitingForMaterial = true;
        Material m_DefaultMaterial;
        List<object> m_Pendings = new List<object>();

        [RpcInput]
        void OnConvertToGameObject(RpcContext<ConvertToGameObject> ctx)
        {
            if (m_IsWaitingForMaterial)
            {
                m_Pendings.Add(ctx);
                if (m_Pendings.Count > 1)
                    return;

                var rpc = m_GetProjectWideDefaultMaterialOutput.Call(this, ctx, (object)null, new GetProjectWideDefaultMaterial());
                rpc.Success<Material>((self, ctx, userCtx, mat) => self.CompleteDefaultMaterialAcquisition(ctx, mat));
                rpc.Failure((self, ctx, userCtx, ex) =>
                {
                    self.CompleteDefaultMaterialAcquisition(ctx, null);
                    Debug.LogException(ex);
                });

                return;
            }

            ConvertGameObjectInternal(ctx);
        }

        void CompleteDefaultMaterialAcquisition(RpcContext<ConvertToGameObject> ctx, Material material)
        {
            m_IsWaitingForMaterial = false;
            m_DefaultMaterial = material;
            foreach (var pending in m_Pendings)
                ConvertGameObjectInternal(Unsafe.As<RpcContext<ConvertToGameObject>>(pending));
            m_Pendings.Clear();
        }
        
        void ConvertGameObjectInternal(RpcContext<ConvertToGameObject> ctx)
        {
            if (ctx.Data.Stream.IsCancelled)
            {
                ctx.SendSuccess(NullData.Null);
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
                
                var waitAllTracker = new WaitAllTracker<object> { Tracker = tracker, Position = startIndex + i };
                
                var rpc = m_AcquireEntryDataFromModelOutput.Call(this, tracker.Ctx, waitAllTracker, new AcquireEntryDataFromModelData(tracker.Ctx.Data.ManifestId, resourceIds[i]));
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
            AcquireUnityResource(tracker, tracker.Meshes, 0, m_AcquireUnityMeshOutput,
                (state, guid, entry) => new AcquireUnityMesh(state, entry));

            AcquireUnityResource(tracker, tracker.Materials, tracker.Meshes.Count, m_AcquireUnityMaterialOutput,
                (state, guid, entry) => new AcquireUnityMaterial(state, guid, entry));
        }

        void AcquireUnityResource<TUnityResource, TMessage>(Tracker tracker, List<TUnityResource> results, int startIndex,
            RpcOutput<TMessage> output, Func<StreamState, ManifestGuid, EntryData, TMessage> createMessageFunc)
            where TUnityResource : class
            where TMessage : class
        {
            var entries = tracker.Entries.GetRange(startIndex, results.Count);

            for (var i = 0; i < entries.Count; ++i)
            {
                var entry = entries[i];
                ++tracker.NbPendingCalls;
                var waitAllTracker = new WaitAllTracker<TUnityResource> { Tracker = tracker, Results = results, Position = i };
                
                var rpc = output.Call(this, tracker.Ctx, waitAllTracker, createMessageFunc(tracker.Ctx.Data.Stream, tracker.Ctx.Data.ManifestId, entry));
                rpc.Success<TUnityResource>((self, ctx, waitAllTracker, resource) =>
                {
                    var tracker = waitAllTracker.Tracker;
                    var results = waitAllTracker.Results;
                    results[waitAllTracker.Position] = resource;
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
                tracker.Ctx.SendSuccess(NullData.Null);
                return;
            }

            var meshes = new Dictionary<StreamKey, Mesh>();
            for (var i = 0; i < tracker.Meshes.Count; ++i)
            {
                var entry = tracker.Entries[i];
                var streamKey = new StreamKey(entry.SourceId, entry.IdInSource);
                meshes[streamKey] = tracker.Meshes[i];
            }

            var materials = new Dictionary<StreamKey, Material>();
            for (var i = 0; i < tracker.Materials.Count; ++i)
            {
                var entry = tracker.Entries[tracker.Meshes.Count + i];
                var streamKey = new StreamKey(entry.SourceId, entry.IdInSource);
                materials[streamKey] = tracker.Materials[i];
            }

            var data = tracker.Ctx.Data;
            var buildGameObject = new BuildGameObject(data.Stream, tracker.Ctx.Data.InstanceData, data.SyncInstance, data.SyncObject, m_DefaultMaterial, meshes, materials);
            var rpc = m_BuildGameObjectOutput.Call(this, tracker.Ctx, tracker, buildGameObject);

            rpc.Success<GameObject>((self, ctx, tracker, gameObject) =>
            {
                tracker.GameObject = gameObject;

                if (tracker.Ctx.Data.Stream.IsCancelled && gameObject == null)
                    ClearAcquiredResources(tracker);

                m_SetGameObjectDependenciesOutput.Send(new SetGameObjectDependencies(gameObject, tracker.Meshes, tracker.Materials));

                var rpc = m_UpdateEntryDependenciesOutput.Call(self, ctx, tracker, new UpdateEntryDependencies(tracker.Ctx.Data.ObjectData.Id, tracker.Ctx.Data.ManifestId, tracker.Entries.Select(x => x.Id).ToList()));
                rpc.Success<NullData>((self, ctx, tracker, _) =>
                {
                    ctx.SendSuccess(tracker.GameObject);
                });
                rpc.Failure((self, ctx, tracker, ex) =>
                {
                    ctx.SendSuccess(tracker.GameObject);
                    Debug.LogException(ex);
                });
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
                if (!ReferenceEquals(tracker.Meshes[i], null))
                    m_ReleaseUnityMeshOutput.Send(new ReleaseUnityMesh(tracker.Meshes[i]));
            }

            for (var i = 0; i < tracker.Materials.Count; ++i)
            {
                if (!ReferenceEquals(tracker.Materials[i], null))
                    m_ReleaseUnityMaterialOutput.Send(new ReleaseUnityMaterial(tracker.Materials[i]));
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
            public GameObject GameObject;
        }
    }
}
