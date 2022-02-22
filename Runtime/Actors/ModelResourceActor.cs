using System;
using System.Collections.Generic;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Model;

namespace Unity.Reflect.Actors
{
    [Actor("352ecc82-63da-4426-8c41-55a3df06948e")]
    public class ModelResourceActor
    {
#pragma warning disable 649
        RpcOutput<GetSyncModel> m_GetSyncModelOutput;
#pragma warning restore 649

        bool m_IsAutomaticCacheCleaningEnabled;
        
        Dictionary<EntryGuid, List<Tracker>> m_Waiters = new Dictionary<EntryGuid, List<Tracker>>();
        Dictionary<EntryGuid, Resource> m_LoadedResources = new Dictionary<EntryGuid, Resource>();
        Dictionary<ISyncModel, Resource> m_SyncModels = new Dictionary<ISyncModel, Resource>();

        [RpcInput]
        void OnAcquireResource(RpcContext<AcquireResource> ctx)
        {
            if (ctx.Data.Stream.IsCancelled)
            {
                ctx.SendSuccess(NullData.Null);
                return;
            }

            var resourceId = ctx.Data.ResourceData.Id;
            if (m_LoadedResources.TryGetValue(resourceId, out var resource))
            {
                ++resource.RefCount;
                ctx.SendSuccess(resource.Model);
                return;
            }

            var tracker = new Tracker { Ctx = ctx };
            if (!m_Waiters.TryGetValue(resourceId, out var trackers))
            {
                trackers = new List<Tracker>();
                m_Waiters.Add(resourceId, trackers);
            }

            trackers.Add(tracker);
            if (trackers.Count > 1)
                return;

            var rpc = m_GetSyncModelOutput.Call(this, ctx, tracker, new GetSyncModel(tracker.Ctx.Data.ResourceData));
            rpc.Success<ISyncModel>((self, ctx, tracker, syncModel) =>
            {
                var resource = new Resource(tracker.Ctx.Data.ResourceData.Id, syncModel, 0);
                m_LoadedResources.Add(tracker.Ctx.Data.ResourceData.Id, resource);
                m_SyncModels.Add(syncModel, resource);

                var trackers = m_Waiters[tracker.Ctx.Data.ResourceData.Id];

                foreach (var t in trackers)
                {
                    t.Ctx.SendSuccess(syncModel);
                    ++resource.RefCount;
                }

                m_Waiters.Remove(tracker.Ctx.Data.ResourceData.Id);
            });

            rpc.Failure((self, ctx, tracker, ex) =>
            {
                var trackers = m_Waiters[tracker.Ctx.Data.ResourceData.Id];
                
                foreach (var t in trackers)
                    t.Ctx.SendFailure(ex);

                m_Waiters.Remove(tracker.Ctx.Data.ResourceData.Id);
            });
        }

        [NetInput]
        void OnReleaseResource(NetContext<ReleaseModelResource> ctx)
        {
            if (!m_SyncModels.TryGetValue(ctx.Data.Model, out var resource))
                return;

            --resource.RefCount;

            if (m_IsAutomaticCacheCleaningEnabled && resource.RefCount == 0)
            {
                m_LoadedResources.Remove(resource.Id);
                m_SyncModels.Remove(ctx.Data.Model);
            }
        }

        [NetInput]
        void OnMemoryStateChanged(NetContext<MemoryStateChanged> ctx)
        {
            if (ctx.Data.IsMemoryLevelTooHigh)
            {
                m_LoadedResources.Clear();
                m_SyncModels.Clear();
                GC.Collect(2, GCCollectionMode.Forced);
            }
        }

        [NetInput]
        void OnSetAutomaticCacheCleaning(NetContext<SetAutomaticCacheCleaning> ctx)
        {
            m_IsAutomaticCacheCleaningEnabled = ctx.Data.IsEnabled;

            if (!m_IsAutomaticCacheCleaningEnabled)
                return;

            var toRemove = new List<Resource>();
            foreach (var kv in m_LoadedResources)
            {
                if (kv.Value.RefCount == 0)
                    toRemove.Add(kv.Value);
            }

            foreach (var res in toRemove)
            {
                m_LoadedResources.Remove(res.Id);
                m_SyncModels.Remove(res.Model);
            }
        }

        class Resource
        {
            public EntryGuid Id;
            public ISyncModel Model;
            public int RefCount;

            public Resource(EntryGuid id, ISyncModel model, int refCount)
            {
                Id = id;
                Model = model;
                RefCount = refCount;
            }
        }

        class Tracker
        {
            public RpcContext<AcquireResource> Ctx;
        }
    }
}
