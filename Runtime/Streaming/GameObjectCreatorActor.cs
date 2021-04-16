using System;
using Unity.Reflect.Actor;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Streaming
{
    [Actor]
    public class GameObjectCreatorActor
    {
#pragma warning disable 649
        NetOutput<ReleaseEntryData> m_ReleaseEntryDataOutput;
        NetOutput<ReleaseResource> m_ReleaseResourceOutput;

        RpcOutput<AcquireEntryData> m_AcquireEntryDataOutput;
        RpcOutput<AcquireResource> m_AcquireResourceOutput;
        RpcOutput<AcquireEntryDataFromModelData> m_AcquireEntryDataFromModelDataOutput;
        RpcOutput<ConvertToGameObject> m_ConvertToGameObjectOutput;
#pragma warning restore 649

        [RpcInput]
        void OnCreateGameObject(RpcContext<CreateGameObject> ctx)
        {
            var tracker = new Tracker();
            tracker.InstanceId = ctx.Data.InstanceId;

            if (CompleteIfCanceled(ctx, tracker))
                return;

            var instanceEntryRpc = m_AcquireEntryDataOutput.Call(this, ctx, tracker, new AcquireEntryData(ctx.Data.InstanceId));
            instanceEntryRpc.Success<EntryData>((self, ctx, tracker, instanceData) =>
            {
                tracker.InstanceData = instanceData;

                var instanceRpc = self.m_AcquireResourceOutput.Call(self, ctx, tracker, new AcquireResource(ctx.Data.Stream, instanceData));
                instanceRpc.Success<SyncObjectInstance>((self, ctx, tracker, syncInstance) =>
                {
                    var instance = tracker.Instance = syncInstance;

                    if (self.CompleteIfCanceled(ctx, tracker))
                        return;

                    var objEntryRpc = self.m_AcquireEntryDataFromModelDataOutput.Call(self, ctx, tracker, new AcquireEntryDataFromModelData(tracker.InstanceData.ManifestId, new PersistentKey(typeof(SyncObject), instance.ObjectId.Value)));
                    objEntryRpc.Success<EntryData>((self, ctx, tracker, objData) =>
                    {
                        tracker.ObjectId = objData.Id;
                        tracker.ObjectData = objData;

                        var objRpc = self.m_AcquireResourceOutput.Call(self, ctx, tracker, new AcquireResource(ctx.Data.Stream, tracker.ObjectData));
                        objRpc.Success<SyncObject>((self, ctx, tracker, syncObject) =>
                        {
                            tracker.Object = syncObject;

                            if (self.CompleteIfCanceled(ctx, tracker))
                                return;

                            var goRpc = self.m_ConvertToGameObjectOutput.Call(self, ctx, tracker, new ConvertToGameObject(ctx.Data.Stream, tracker.InstanceData, tracker.Instance, tracker.Object));
                            goRpc.Success<GameObject>((self, ctx, tracker, gameObject) =>
                            {
                                // gameObject may be null at this point, but the caller will
                                // check if the stream has been canceled anyway. Just forward.
                                self.ClearTrackerResources(tracker);
                                ctx.SendSuccess(gameObject);
                            });

                            goRpc.Failure((self, ctx, tracker, ex) => self.CompleteRequestAsFailure(ctx, tracker, ex));
                        });
                        objRpc.Failure((self, ctx, tracker, ex) => self.CompleteRequestAsFailure(ctx, tracker, ex));
                    });
                    objEntryRpc.Failure((self, ctx, tracker, ex) => self.CompleteRequestAsFailure(ctx, tracker, ex));
                });
                instanceRpc.Failure((self, ctx, tracker, ex) => self.CompleteRequestAsFailure(ctx, tracker, ex));
            });
            instanceEntryRpc.Failure((self, ctx, tracker, ex) => self.CompleteRequestAsFailure(ctx, tracker, ex));
        }

        bool CompleteIfCanceled(RpcContext<CreateGameObject> ctx, Tracker tracker)
        {
            if (ctx.Data.Stream.IsCancelled)
            {
                ClearTrackerResources(tracker);
                ctx.SendSuccess<GameObject>(null);
                return true;
            }

            return false;
        }

        void ClearTrackerResources(Tracker tracker)
        {
            if (tracker.InstanceData != null)
                m_ReleaseEntryDataOutput.Send(new ReleaseEntryData(tracker.InstanceData.Id));

            if (tracker.Instance != null)
                m_ReleaseResourceOutput.Send(new ReleaseResource(tracker.InstanceId));

            if (tracker.ObjectData != null)
                m_ReleaseEntryDataOutput.Send(new ReleaseEntryData(tracker.ObjectData.Id));

            if (tracker.Object != null)
                m_ReleaseResourceOutput.Send(new ReleaseResource(tracker.ObjectId));

            tracker.InstanceData = null;
            tracker.Instance = null;
            tracker.ObjectData = null;
            tracker.Object = null;
        }

        void CompleteRequestAsFailure(RpcContext<CreateGameObject> ctx, Tracker tracker, Exception ex)
        {
            ClearTrackerResources(tracker);
            ctx.SendFailure(ex);
        }

        class Tracker
        {
            public Guid InstanceId;
            public EntryData InstanceData;
            public SyncObjectInstance Instance;

            public Guid ObjectId;
            public EntryData ObjectData;
            public SyncObject Object;
        }
    }
}
