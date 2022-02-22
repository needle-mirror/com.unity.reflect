using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.Reflect.ActorFramework
{
    public class EventInputAttribute : Attribute, IInputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public EventInputAttribute() { }
        public EventInputAttribute(string guid, string displayName)
        {
            Id = guid;
            DisplayName = displayName;
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }
        
        public Type GetInputType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType;
        public Type[] GetLinkTypes(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType.GetGenericArguments();
    }

    public class EventOutputAttribute : Attribute, IOutputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public EventOutputAttribute() { }
        public EventOutputAttribute(string guid, string displayName)
        {
            Id = guid;
            DisplayName = displayName;
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }
        
        public Type[] GetLinkTypes(FieldInfo fieldInfo) => fieldInfo.FieldType.GetGenericArguments();
    }

    public class EventOutput<TData>
        where TData : class
    {
        EventComponent m_Event;

        public EventOutput(EventComponent evt)
        {
            m_Event = evt;
        }
        
        public void Broadcast(TData data)
        {
            m_Event.Broadcast(data);
        }
    }

    [Component("3381977f-4318-4c84-8545-dacea1dcde7e",
        typeof(EventInputAttribute),
        typeof(EventOutputAttribute),
        typeof(EventOutput<>),
        inputMultiplicity: Multiplicity.Zero,
        outputMultiplicity: Multiplicity.Zero)]
    public class EventComponent
    {
        // Make sure the default types do exist, so compiler/jitter
        // correctly generate the methods when Unsafe cast is used. il2cpp build may crash without this,
        // and it's not an issue in the implementation, as the spec is not clear whether or not it must be supported.
        static readonly EventMessage<object> k_UnusedMessage = new EventMessage<object>(null);
        static readonly EventContext<object> k_UnusedContext = new EventContext<object>();

        readonly NetComponent m_Net;
        ActorHandle m_SelfRef;
        ActorHandle m_PubSubRef;

        readonly Dictionary<Type, Action<EventContext<object>>> m_Actions = new Dictionary<Type, Action<EventContext<object>>>();

        public EventComponent(ActorHandle self, NetComponent net)
        {
            m_Net = net;
            m_SelfRef = self;
        }

        public void Initialize(ActorHandle pubSubRef)
        {
            m_PubSubRef = pubSubRef;
            m_Net.RegisterOpenGeneric<EventMessage<object>>(OnEventMessage);
        }

        public void Subscribe<TData>(Action<EventContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData), Unsafe.As<Action<EventContext<object>>>(action));
            m_Net.Send(m_PubSubRef, new SubscribeToEvent(m_SelfRef, typeof(TData)));
        }

        public void Unsubscribe<TData>()
        {
            m_Actions.Remove(typeof(TData));
            m_Net.Send(m_PubSubRef, new UnsubscribeFromEvent(m_SelfRef, typeof(TData)));
        }

        public void UnsubscribeAll()
        {
            foreach(var kv in m_Actions)
                m_Net.Send(m_PubSubRef, new UnsubscribeFromEvent(m_SelfRef, kv.Key));

            m_Actions.Clear();
        }

        public void Broadcast<TData>(TData data)
            where TData : class
        {
            var eventMsg = new EventMessage<TData>(data);
            var eventCtx = new EventContext<TData>();
            eventMsg.Ctx = eventCtx;
            m_Net.Send(m_PubSubRef, eventMsg);
        }

        void OnEventMessage(NetContext<EventMessage<object>> ctx)
        {
            if (TryGetAction(ctx, out var action))
            {
                ctx.Data.Ctx.Message = ctx.Message;

                try
                {
                    action(ctx.Data.Ctx);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            // If not mapped, it may be a misconfiguration or simply an unsubscribe that is not processed yet on the PubSub side.
        }

        bool TryGetAction(NetContext<EventMessage<object>> ctx, out Action<EventContext<object>> action)
        {
            return m_Actions.TryGetValue(ctx.Data.Data.GetType(), out action);
        }

        public class SubscribeToEvent
        {
            public ActorHandle ReceiverRef;
            public Type EventType;

            public SubscribeToEvent(ActorHandle receiverRef, Type eventType)
            {
                ReceiverRef = receiverRef;
                EventType = eventType;
            }
        }

        public class UnsubscribeFromEvent
        {
            public ActorHandle ReceiverRef;
            public Type EventType;

            public UnsubscribeFromEvent(ActorHandle receiverRef, Type eventType)
            {
                ReceiverRef = receiverRef;
                EventType = eventType;
            }
        }
    }

    /// <summary>
    ///     Context received when subscribing to events
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public sealed class EventContext<TData>
        where TData : class
    {
        public NetMessage<EventMessage<TData>> Message;

        public ref TData Data => ref Message.Data.Data;
    }
}
