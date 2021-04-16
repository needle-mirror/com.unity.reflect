using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.Reflect.Actor
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

        public Type GetInputMessageType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType.GetGenericArguments()[0];
        public Type GetInputType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType;
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

        public Type GetOutputMessageType(FieldInfo fieldInfo) => fieldInfo.FieldType.GetGenericArguments()[0];
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

    [Component(
        inputAttributeType: typeof(EventInputAttribute),
        outputAttributeType: typeof(EventOutputAttribute),
        outputType: typeof(EventOutput<>),
        inputMultiplicity: Multiplicity.Zero,
        outputMultiplicity: Multiplicity.Zero)]
    public class EventComponent
    {
        readonly NetComponent m_Net;
        ActorRef m_SelfRef;
        ActorRef m_PubSubRef;

        readonly Dictionary<Type, Action<EventContext>> m_Actions = new Dictionary<Type, Action<EventContext>>();

        public EventComponent(ActorRef self, NetComponent net)
        {
            m_Net = net;
            m_SelfRef = self;
        }

        public void Initialize(ActorRef pubSubRef)
        {
            m_PubSubRef = pubSubRef;
            m_Net.RegisterOpenGeneric<EventMessage<object>>(OnEventMessage);
        }

        public void Subscribe<TData>(Action<EventContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData), Unsafe.As<Action<EventContext>>(action));
            m_Net.Send(m_PubSubRef, new SubscribeToEvent(m_SelfRef, typeof(TData)));
        }

        public void Unsubscribe<TData>()
        {
            m_Actions.Remove(typeof(TData));
            m_Net.Send(m_PubSubRef, new UnsubscribeFromEvent(m_SelfRef, typeof(TData)));
        }

        public void Broadcast<TData>(TData evt)
            where TData : class
        {
            m_Net.Send(m_PubSubRef, new EventMessage<TData>(evt));
        }

        void OnEventMessage(NetContext<EventMessage<object>> ctx)
        {
            if (TryGetAction(ctx, out var action))
            {
                var eventCtx = Unsafe.As<EventContext>(new EventContext<object>(ctx));

                try
                {
                    action(eventCtx);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            else
                Debug.LogError($"Nobody has registered an action for data type {GetMessageType(ctx).Name}. Discarding event message.");
        }

        bool TryGetAction(NetContext<EventMessage<object>> ctx, out Action<EventContext> action)
        {
            return m_Actions.TryGetValue(ctx.Data.Data.GetType(), out action);
        }

        static Type GetMessageType(NetContext<EventMessage<object>> ctx)
        {
            return ctx.Data.GetType().GetGenericArguments()[0];
        }

        class EventContext : EventContext<object>
        {
            EventContext()
                : base(null) { }
        }

        public class SubscribeToEvent
        {
            public ActorRef ReceiverRef;
            public Type EventType;

            public SubscribeToEvent(ActorRef receiverRef, Type eventType)
            {
                ReceiverRef = receiverRef;
                EventType = eventType;
            }
        }

        public class UnsubscribeFromEvent
        {
            public ActorRef ReceiverRef;
            public Type EventType;

            public UnsubscribeFromEvent(ActorRef receiverRef, Type eventType)
            {
                ReceiverRef = receiverRef;
                EventType = eventType;
            }
        }
    }

    public class EventContext<TData>
        where TData : class
    {
        public NetContext<EventMessage<TData>> Ctx;

        public EventContext(NetContext<EventMessage<TData>> parentContext)
        {
            Ctx = parentContext;
        }

        public EventMessage<TData> Message => Ctx.Data;
        public TData Data => Ctx.Data.Data;
    }
}
