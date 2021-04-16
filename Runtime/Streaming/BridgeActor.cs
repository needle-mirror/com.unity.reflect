using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Reflect.Actor;
using Unity.Reflect.Collections;
using UnityEngine;

namespace Unity.Reflect.Streaming
{
    [Actor(isBoundToMainThread: true)]
    public class BridgeActor
    {
#pragma warning disable 649
        EventComponent m_Event;
        RpcOutput<UpdateManifests> m_UpdateManifestsOutput;
        NetOutput<SetHighlightVisibility> m_SetHighlightVisibilityOutput;
        NetOutput<SetHighlightFilter> m_SetHighlightFilterOutput;
        RpcOutput<GetFilterStates> m_GetFilterStatesOutput;
        RpcOutput<PickFromRay> m_PickFromRayOutput;
        RpcOutput<PickFromSamplePoints> m_PickFromSamplePointsOutput;
        RpcOutput<PickFromDistance> m_PickFromDistanceOutput;
#pragma warning restore 649

        ActorSystemSetup m_LoadedAsset;
        Dictionary<Type, List<object>> m_Callbacks = new Dictionary<Type, List<object>>();

        public void Initialize(ActorSystemSetup asset)
        {
            m_LoadedAsset = asset;
        }

        void OnEventMessage(EventContext<object> ctx)
        {
            if (!m_Callbacks.TryGetValue(ctx.Data.GetType(), out var callbacks))
                return;

            foreach (var callback in callbacks)
                Unsafe.As<Action<EventContext<object>>>(callback)(ctx);
        }

        void Subscribe<TData>(Action<EventContext<TData>> action)
            where TData : class
        {
            if (!m_Callbacks.TryGetValue(typeof(TData), out var callbacks))
            {
                callbacks = new List<object>();
                m_Callbacks.Add(typeof(TData), callbacks);
                Action<EventContext<object>> proxyAction = OnEventMessage;
                m_Event.Subscribe(Unsafe.As<Action<EventContext<TData>>>(proxyAction));
            }

            callbacks.Add(action);
        }

        void Unsubscribe<TData>(Action<EventContext<TData>> action)
            where TData : class
        {
            if (!m_Callbacks.TryGetValue(typeof(TData), out var callbacks))
                return;

            callbacks.RemoveAll(x => x == Unsafe.As<Action<EventContext<object>>>(action));

            if (callbacks.Count == 0)
            {
                m_Callbacks.Remove(typeof(TData));
                m_Event.Unsubscribe<TData>();
            }
        }

        TSettings GetFirstMatchingSettings<TSettings>()
            where TSettings : ActorSettings, new()
        {
            var firstMatch = m_LoadedAsset.ActorSetups
                .FindAll(x => x.Settings is TSettings)
                .Select(x => x.Settings)
                .OrderBy(x => x.GetType() != typeof(TSettings))
                .Cast<TSettings>()
                .FirstOrDefault();

            return firstMatch;
        }

        void UpdateSetting<TSettings>(string id, string key, object value)
            where TSettings : class
        {
            m_Event.Broadcast(new UpdateSetting<TSettings>(id, key, value));
        }

        void SendUpdateManifests()
        {
            var rpc = m_UpdateManifestsOutput.Call(this, (object)null, (object)null, new UpdateManifests());
            rpc.Success<object>((self, ctx, userCtx, res) =>
            {
                Debug.Log("Manifests loaded");
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                Debug.LogException(ex);
            });
        }

        void SetHighlightVisibility(string groupKey, string filterKey, bool isVisible)
        {
            m_SetHighlightVisibilityOutput.Send(new SetHighlightVisibility(groupKey, filterKey, isVisible));
        }

        void SetHighlightFilter(string groupKey, string filterKey)
        {
            m_SetHighlightFilterOutput.Send(new SetHighlightFilter(groupKey, filterKey));
        }

        void GetFilterStates(string groupKey, Action<List<FilterState>> callback)
        {
            var rpc = m_GetFilterStatesOutput.Call((object)null, (object)null, callback, new GetFilterStates(groupKey));
            rpc.Success<List<FilterState>>((self, ctx, callback, result) =>
            {
                callback(result);
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                Debug.LogException(ex);
            });
        }

        void PickFromRay(Ray ray, Action<List<ISpatialObject>> callback)
        {
            var rpc = m_PickFromRayOutput.Call((object)null, (object)null, callback, new PickFromRay(ray));
            rpc.Success<List<ISpatialObject>>((self, ctx, callback, result) =>
            {
                callback(result);
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                Debug.LogException(ex);
            });
        }

        void PickFromSamplePoints(Vector3[] samplePoints, int count, Action<List<ISpatialObject>> callback)
        {
            var rpc = m_PickFromSamplePointsOutput.Call((object)null, (object)null, callback, new PickFromSamplePoints(samplePoints, count));
            rpc.Success<List<ISpatialObject>>((self, ctx, callback, result) =>
            {
                callback(result);
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                Debug.LogException(ex);
            });
        }

        void PickFromDistance(int distance, Action<List<ISpatialObject>> callback)
        {
            var rpc = m_PickFromDistanceOutput.Call((object)null, (object)null, callback, new PickFromDistance(distance));
            rpc.Success<List<ISpatialObject>>((self, ctx, callback, result) =>
            {
                callback(result);
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                Debug.LogException(ex);
            });
        }

        public struct Proxy
        {
            BridgeActor m_Self;

            public Proxy(BridgeActor self) => m_Self = self;

            public bool IsInitialized => m_Self != null;

            public void Subscribe<TData>(Action<EventContext<TData>> action) where TData : class => m_Self.Subscribe(action);
            public void Unsubscribe<TData>(Action<EventContext<TData>> action) where TData : class => m_Self.Unsubscribe(action);
            public TSettings GetFirstMatchingSettings<TSettings>() where TSettings : ActorSettings, new() => m_Self.GetFirstMatchingSettings<TSettings>();
            public void UpdateSetting<TSettings>(string id, string key, object value) where TSettings : class  => m_Self.UpdateSetting<TSettings>(id, key, value);

            public void SendUpdateManifests() => m_Self.SendUpdateManifests();
            public void SetHighlightVisibility(string groupKey, string filterKey, bool isVisible) => m_Self.SetHighlightVisibility(groupKey, filterKey, isVisible);
            public void SetHighlightFilter(string groupKey, string filterKey) => m_Self.SetHighlightFilter(groupKey, filterKey);
            public void GetFilterStates(string groupKey, Action<List<FilterState>> callback) => m_Self.GetFilterStates(groupKey, callback);
            public void PickFromRay(Ray ray, Action<List<ISpatialObject>> callback) => m_Self.PickFromRay(ray, callback);
            public void PickFromSamplePoints(Vector3[] samplePoints, int count, Action<List<ISpatialObject>> callback) => m_Self.PickFromSamplePoints(samplePoints, count, callback);
            public void PickFromDistance(int distance, Action<List<ISpatialObject>> callback) => m_Self.PickFromDistance(distance, callback);
        }
    }
}
