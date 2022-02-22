using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Reflect.ActorFramework;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.Actors
{
    [Actor("b27ba76d-8675-4987-aec5-517619b96572", true)]
    public class BridgeActor
    {
#pragma warning disable 649
        NetComponent m_Net;
        RpcComponent m_Rpc;
        EventComponent m_Event;

        RpcOutput<UpdateManifests> m_UpdateManifestsOutput;
        NetOutput<PreShutdown> m_PreShutdownOutput;
#pragma warning restore 649

        ActorSystemSetup m_LoadedAsset;
        Action m_PreShutdownCallback;
        ActorRunner.Proxy m_Runner;
        Dictionary<Type, List<object>> m_Callbacks = new Dictionary<Type, List<object>>();

        public void Initialize(ActorSystemSetup asset)
        {
            m_LoadedAsset = asset;
        }

        public void SetActorRunner(ActorRunner.Proxy runner)
        {
            m_Runner = runner;
        }

        public TickResult Tick(TimeSpan endTime)
        {
            // Skip endTime and force callback processing to be instant
            return m_Net.Tick(TimeSpan.MaxValue);
        }

        [NetInput]
        void OnPreShutdown(NetContext<PreShutdown> _)
        {
            m_PreShutdownCallback();
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

#pragma warning disable CS0252
            // This is ok, we don't want to compare "similar" MulticastDelegate, we want to compare that we have the same Action delegate object
            callbacks.RemoveAll(x => x == Unsafe.As<Action<EventContext<object>>>(action));
#pragma warning restore CS0252

            if (callbacks.Count == 0)
            {
                m_Callbacks.Remove(typeof(TData));
                m_Event.Unsubscribe<TData>();
            }
        }

        void UnsubscribeAll()
        {
            m_Callbacks.Clear();
            m_Event.UnsubscribeAll();
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

        TSettings GetFirstOrEmptySettings<TSettings>()
            where TSettings : ActorSettings, new()
        {
            return GetFirstMatchingSettings<TSettings>() ?? new TSettings();
        }

        void UpdateSetting<TSettings>(string id, string key, object value)
            where TSettings : class
        {
            m_Event.Broadcast(new UpdateSetting<TSettings>(id, key, value));
        }

        void ForwardNet<TData>(ActorHandle destination, TData data)
            where TData : class
        {
            m_Net.Send(destination, data);
        }

        void ForwardRpc<TData, TSuccess>(ActorHandle destination, TData data, Action<TSuccess> success, Action<Exception> failure)
            where TData : class
            where TSuccess : class
        {
            var rpc = m_Rpc.Call((object)null, (object)null, (object)null, destination, data);
            rpc.Success<TSuccess>((self, ctx, userCtx, res) => success(res));
            rpc.Failure((self, ctx, userCtx, ex) => failure(ex));
        }

        void ForwardRpcBlocking<TData, TSuccess>(ActorHandle destination, TData data, Action<TSuccess> success, Action<Exception> failure)
            where TData : class
            where TSuccess : class
        {
            var cc = new ConditionCapture<bool>(false);
            var rpc = m_Rpc.Call((object)null, (object)null, cc, destination, data);
            rpc.Success<TSuccess>((self, ctx, cc, res) =>
            {
                success(res);
                cc.Data = true;
            });
            rpc.Failure((self, ctx, cc, ex) =>
            {
                failure(ex);
                cc.Data = true;
            });

            m_Runner.ProcessUntil(cc, c => c.Data);
        }

        void SendUpdateManifests()
        {
            var rpc = m_UpdateManifestsOutput.Call(this, (object)null, (object)null, new UpdateManifests());
            rpc.Success((self, ctx, userCtx, res) =>
            {
                Debug.Log("Manifests loaded");
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                if (!(ex is OperationCanceledException))
                    Debug.LogException(ex);
            });
        }

        void PreShutdown(Action onCompleted)
        {
            m_PreShutdownCallback = onCompleted;
            m_PreShutdownOutput.Send(new PreShutdown());
        }

        public struct Proxy
        {
            BridgeActor m_Self;

            public Proxy(BridgeActor self)
            {
                m_Self = self;
            }

            public bool IsInitialized => m_Self != null;

            public void Subscribe<TData>(Action<EventContext<TData>> action) where TData : class => m_Self.Subscribe(action);
            public void Unsubscribe<TData>(Action<EventContext<TData>> action) where TData : class => m_Self.Unsubscribe(action);
            public void UnsubscribeAll() => m_Self.UnsubscribeAll();
            public TSettings GetFirstMatchingSettings<TSettings>() where TSettings : ActorSettings, new() => m_Self.GetFirstMatchingSettings<TSettings>();
            public TSettings GetFirstOrEmptySettings<TSettings>() where TSettings : ActorSettings, new() => m_Self.GetFirstOrEmptySettings<TSettings>();
            public void UpdateSetting<TSettings>(string id, string key, object value) where TSettings : class  => m_Self.UpdateSetting<TSettings>(id, key, value);

            public void ForwardNet<TData>(ActorHandle destination, TData data) where TData : class => m_Self.ForwardNet(destination, data);
            public void ForwardRpc<TData, TSuccess>(ActorHandle destination, TData data, Action<TSuccess> success, Action<Exception> failure) where TData : class where TSuccess : class => m_Self.ForwardRpc(destination, data, success, failure);
            public void ForwardRpcBlocking<TData, TSuccess>(ActorHandle destination, TData data, Action<TSuccess> success, Action<Exception> failure) where TData : class where TSuccess : class => m_Self.ForwardRpcBlocking(destination, data, success, failure);

            public void SendUpdateManifests() => m_Self.SendUpdateManifests();
            public void PreShutdown(Action onCompleted) => m_Self.PreShutdown(onCompleted);
        }
    }
}
