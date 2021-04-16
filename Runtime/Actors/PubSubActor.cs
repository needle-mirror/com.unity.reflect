using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Unity.Reflect.Actor
{
    [Actor]
    public class PubSubActor
    {
#pragma warning disable 649
        NetComponent m_Net;
#pragma warning restore 649

        CancellationToken m_Token;

        Dictionary<Type, List<ActorRef>> m_Receivers = new Dictionary<Type, List<ActorRef>>();
        
        public void Inject()
        {
            m_Net.Register<EventComponent.SubscribeToEvent>(OnSubscribeToEvent);
            m_Net.Register<EventComponent.UnsubscribeFromEvent>(OnUnsubscribeFromEvent);
            m_Net.RegisterOpenGeneric<EventMessage<object>>(OnEventMessage);
        }

        public void SetToken(CancellationToken token)
        {
            m_Token = token;
        }
        
        void OnSubscribeToEvent(NetContext<EventComponent.SubscribeToEvent> ctx)
        {
            if (!m_Receivers.TryGetValue(ctx.Data.EventType, out var receivers))
            {
                receivers = new List<ActorRef>();
                m_Receivers.Add(ctx.Data.EventType, receivers);
            }

            if (receivers.Contains(ctx.Data.ReceiverRef))
            {
                Debug.LogWarning("Already subscribed");
                return;
            }

            receivers.Add(ctx.Data.ReceiverRef);
        }
        
        void OnUnsubscribeFromEvent(NetContext<EventComponent.UnsubscribeFromEvent> ctx)
        {
            if (!m_Receivers.TryGetValue(ctx.Data.EventType, out var receivers))
            {
                Debug.LogWarning("No subscription");
                return;
            }

            if (!receivers.Remove(ctx.Data.ReceiverRef))
                Debug.LogWarning("No subscription");
        }

        void OnEventMessage(NetContext<EventMessage<object>> ctx)
        {
            if (!m_Receivers.TryGetValue(ctx.Data.Data.GetType(), out var receivers))
                return;

            // Todo: need a forward function from NetComponent to be able to pool correctly this forwarded message
            m_Net.Send(receivers, ctx.Data);
        }
    }
}
