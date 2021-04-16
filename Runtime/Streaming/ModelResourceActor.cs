using System;
using System.Collections.Generic;
using Unity.Reflect.Actor;
using Unity.Reflect.Model;

namespace Unity.Reflect.Streaming
{
    [Actor]
    public class ModelResourceActor
    {
#pragma warning disable 649
        RpcOutput<GetSyncModel> m_GetSyncModelOutput;
#pragma warning restore 649
        
        Dictionary<Guid, List<Tracker>> m_Waiters = new Dictionary<Guid, List<Tracker>>();
        Dictionary<Guid, (ISyncModel Object, int Count)> m_LoadedResources = new Dictionary<Guid, (ISyncModel Object, int Count)>();

        [RpcInput]
        void OnAcquireResource(RpcContext<AcquireResource> ctx)
        {
            if (ctx.Data.Stream.IsCancelled)
            {
                ctx.SendSuccess<ISyncModel>(null);
                return;
            }

            var resourceId = ctx.Data.ResourceData.Id;
            if (m_LoadedResources.TryGetValue(resourceId, out var pair))
            {
                ++pair.Count;
                m_LoadedResources[resourceId] = pair;
                ctx.SendSuccess(pair.Object);
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
                m_LoadedResources.Add(tracker.Ctx.Data.ResourceData.Id, (syncModel, 0));

                var trackers = m_Waiters[tracker.Ctx.Data.ResourceData.Id];
                var refCount = 0;

                foreach (var t in trackers)
                {
                    t.Ctx.SendSuccess(syncModel);
                    ++refCount;
                }

                m_LoadedResources[tracker.Ctx.Data.ResourceData.Id] = (syncModel, refCount);
                trackers.Clear();
            });

            rpc.Failure((self, ctx, tracker, ex) =>
            {
                var trackers = m_Waiters[tracker.Ctx.Data.ResourceData.Id];
                
                foreach (var t in trackers)
                    t.Ctx.SendFailure(ex);
                trackers.Clear();
            });
        }

        [NetInput]
        void OnReleaseResource(NetContext<ReleaseResource> ctx)
        {
            var pair = m_LoadedResources[ctx.Data.ResourceId];
            --pair.Count;
            m_LoadedResources[ctx.Data.ResourceId] = pair;
        }

        class Tracker
        {
            public RpcContext<AcquireResource> Ctx;
        }
    }
}
