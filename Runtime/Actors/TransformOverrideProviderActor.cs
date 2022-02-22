using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Actors;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Viewer.Actors
{
    [Actor(guid:"845BAF17-8E67-49F9-82B2-CD1ECEDE8BAD")]
    public class TransformOverrideProviderActor
    {
        class Tracker
        {
            public HashSet<EntryData> entriesToProcess;

            public Tracker(IEnumerable<EntryData> entries)
            {
                entriesToProcess = new HashSet<EntryData>(entries);
            }
        }

        RpcOutput<AcquireResource> m_AcquireResourceOutput;
        NetOutput<ReleaseModelResource> m_ReleaseResourceOutput;

        NetOutput<TransformOverrideAdded> m_TransformOverrideAdded;
        NetOutput<TransformOverrideRemoved> m_TransformOverrideRemoved;

        [NetInput]
        void OnEntryDataChanged(NetContext<EntryDataChanged> ctx)
        {
            AddTransformations(new Tracker(ctx.Data.Delta.Added.Where(e => e.EntryType == typeof(SyncTransformOverride))));

            RemoveTransformations(new Tracker(ctx.Data.Delta.Removed.Where(e => e.EntryType == typeof(SyncTransformOverride))));
        }

        void AddTransformations(Tracker tracker)
        {
            foreach(var entryData in tracker.entriesToProcess)
            {
                var addAcquireRpc = m_AcquireResourceOutput.Call(this, tracker, entryData,  new AcquireResource(new StreamState(), entryData));
                addAcquireRpc.Success<SyncTransformOverride>((self, tracker, data, syncObject) =>
                {
                    var transformOverride = TransformOverride.FromSyncModel(syncObject);
                    self.m_ReleaseResourceOutput.Send(new ReleaseModelResource(syncObject));
                    self.m_TransformOverrideAdded.Send(new TransformOverrideAdded
                    {
                        TransformOverride = transformOverride
                    });
                });

                addAcquireRpc.Failure((self, ctx, userCtx, ex) =>
                {
                    Debug.LogException(ex);
                });
            }
        }

        void RemoveTransformations(Tracker tracker)
        {
            foreach(var entryData in tracker.entriesToProcess)
            {
                var addAcquireRpc = m_AcquireResourceOutput.Call(this, tracker, entryData,  new AcquireResource(new StreamState(), entryData));
                addAcquireRpc.Success<SyncTransformOverride>((self, tracker, data, syncObject) =>
                {
                    var transformation = TransformOverride.FromSyncModel(syncObject);
                    self.m_ReleaseResourceOutput.Send(new ReleaseModelResource(syncObject));
                    self.m_TransformOverrideRemoved.Send(new TransformOverrideRemoved
                    {
                        TransformOverride = transformation
                    });
                });

                addAcquireRpc.Failure((self, ctx, userCtx, ex) =>
                {
                    Debug.LogException(ex);
                });
            }
        }
    }

    public class TransformOverrideAdded
    {
        public TransformOverride TransformOverride;
    }
    public class TransformOverrideRemoved
    {
        public TransformOverride TransformOverride;
    }
}
