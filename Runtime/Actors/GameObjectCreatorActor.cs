using System;
using System.Collections.Generic;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    [Actor("2d88c375-656d-4c0d-b6a6-566ed677d1e9")]
    public class GameObjectCreatorActor
    {
#pragma warning disable 649
        NetOutput<ReleaseModelResource> m_ReleaseModelResourceOutput;

        RpcOutput<AcquireDynamicEntry> m_AcquireDynamicEntryDataOutput;
        RpcOutput<AcquireResource> m_AcquireResourceOutput;
        RpcOutput<AcquireEntryDataFromModelData> m_AcquireEntryDataFromModelDataOutput;
        RpcOutput<ConvertToGameObject> m_ConvertToGameObjectOutput;
        
        [RpcOutput(optional:true)]
        RpcOutput<UpdateEntryDependencies> m_UpdateEntryDependenciesOutput;
#pragma warning restore 649

        [RpcInput]
        void OnCreateGameObject(RpcContext<CreateGameObject> ctx)
        {
            var tracker = new Tracker();
            tracker.InstanceId = ctx.Data.InstanceId;

            if (CompleteIfCanceled(ctx, tracker))
                return;
            
            var instanceEntryRpc = m_AcquireDynamicEntryDataOutput.Call(this, ctx, tracker, new AcquireDynamicEntry(ctx.Data.InstanceId));
            instanceEntryRpc.Success<DynamicEntry>((self, ctx, tracker, instanceData) =>
            {
                tracker.InstanceData = instanceData;

                var instanceRpc = self.m_AcquireResourceOutput.Call(self, ctx, tracker, new AcquireResource(ctx.Data.Stream, instanceData.Data));
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

                            var goRpc = self.m_ConvertToGameObjectOutput.Call(self, ctx, tracker, new ConvertToGameObject(ctx.Data.Stream, tracker.InstanceData.ManifestId, tracker.InstanceData.Data, tracker.Instance, tracker.ObjectData, tracker.Object));
                            goRpc.Success<GameObject>((self, ctx, tracker, gameObject) =>
                            {
                                tracker.GameObject = gameObject;

                                var rpc = m_UpdateEntryDependenciesOutput.Call(self, ctx, tracker, new UpdateEntryDependencies(tracker.InstanceData.Data.Id, tracker.InstanceData.ManifestId, new List<EntryGuid> { tracker.ObjectId }));
                                rpc.Success<NullData>((self, ctx, tracker, _) =>
                                {
                                    // gameObject may be null at this point, but the caller will
                                    // check if the stream has been canceled anyway. Just forward.
                                    ctx.SendSuccess(tracker.GameObject);
                                    self.ClearTrackerResources(tracker);
                                });
                                rpc.Failure((self, ctx, tracker, ex) =>
                                {
                                    ctx.SendSuccess(tracker.GameObject);
                                    self.ClearTrackerResources(tracker);
                                    Debug.LogException(ex);
                                });
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
                ctx.SendSuccess(NullData.Null);
                return true;
            }

            return false;
        }

        void ClearTrackerResources(Tracker tracker)
        {
            if (tracker.Instance != null)
                m_ReleaseModelResourceOutput.Send(new ReleaseModelResource(tracker.Instance));

            if (tracker.Object != null)
                m_ReleaseModelResourceOutput.Send(new ReleaseModelResource(tracker.Object));

            tracker.InstanceData = null;
            tracker.Instance = null;
            tracker.ObjectData = null;
            tracker.Object = null;
            tracker.GameObject = null;
        }

        void CompleteRequestAsFailure(RpcContext<CreateGameObject> ctx, Tracker tracker, Exception ex)
        {
            ClearTrackerResources(tracker);
            ctx.SendFailure(ex);
        }

        class Tracker
        {
            public DynamicGuid InstanceId;
            public DynamicEntry InstanceData;
            public SyncObjectInstance Instance;

            public EntryGuid ObjectId;
            public EntryData ObjectData;
            public SyncObject Object;

            public GameObject GameObject;
        }
    }
}
