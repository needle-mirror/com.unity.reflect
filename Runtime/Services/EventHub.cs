using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace UnityEngine.Reflect
{
    public class EventHub
    {
        public class Group
        {
            Group() { }
        }

        class GroupInternal
        {
            public List<HandleInternal> Handles = new List<HandleInternal>();
        }

        public struct Handle
        {
            int m_Id; // IMPORTANT! same memory layout as HandleInternal
        }

        struct HandleInternal
        {
            public int Id; // IMPORTANT! same memory layout as Handle
        }

        readonly Dictionary<HandleInternal, Type> m_HandleToTypes = new Dictionary<HandleInternal, Type>();
        readonly Dictionary<Type, List<(HandleInternal handle, Action<object> action)>> m_TypeToSubscribers = new Dictionary<Type, List<(HandleInternal, Action<object>)>>();
        int m_NextId;

        bool m_IsBroadcasting;
        int m_BroadcastDepth;
        readonly Queue<object> m_QueuedMessages = new Queue<object>();

        public Group CreateGroup()
        {
            return Unsafe.As<Group>(new GroupInternal());
        }

        public void DestroyGroup(Group group)
        {
            var g = Unsafe.As<GroupInternal>(group);
            foreach (var handle in g.Handles)
            {
                var h = handle;
                Unsubscribe(Unsafe.As<HandleInternal, Handle>(ref h));
            }
            g.Handles.Clear();
        }

        public void Subscribe<T>(Group group, Action<T> action) where T : class
        {
            var handle = Subscribe(action);

            var g = Unsafe.As<GroupInternal>(group);
            g.Handles.Add(Unsafe.As<Handle, HandleInternal>(ref handle));
        }

        public Handle Subscribe<T>(Action<T> action) where T : class
        {
            var handle = new HandleInternal { Id = ++m_NextId };
            m_HandleToTypes.Add(handle, typeof(T));

            if (!m_TypeToSubscribers.TryGetValue(typeof(T), out var subscribers))
            {
                subscribers = new List<(HandleInternal, Action<object>)>();
                m_TypeToSubscribers.Add(typeof(T), subscribers);
            }

            subscribers.Add((handle, Unsafe.As<Action<object>>(action)));

            return Unsafe.As<HandleInternal, Handle>(ref handle);
        }

        public void Unsubscribe(Handle handle)
        {
            var h = Unsafe.As<Handle, HandleInternal>(ref handle);

            if (!m_HandleToTypes.TryGetValue(h, out var type))
                return;

            m_HandleToTypes.Remove(h);

            if (!m_TypeToSubscribers.TryGetValue(type, out var subscribers))
                return;

            RemoveFirst(subscribers, h, (e, h1) => e.handle.Id == h1.Id);
        }

        public void Broadcast<T>(T message) where T : class
        {
            const int maxRecursionDepth = 100;

            m_QueuedMessages.Enqueue(message);

            if (m_IsBroadcasting)
                return;

            m_IsBroadcasting = true;
            var broadcastDepth = 0;

            while (m_QueuedMessages.Count != 0)
            {
                ++broadcastDepth;

                if (broadcastDepth > maxRecursionDepth)
                {
                    m_IsBroadcasting = false;
                    throw new Exception($"{nameof(EventHub)} infinite {nameof(Broadcast)} loop detected.");
                }
                
                var msgCount = m_QueuedMessages.Count;
                for (var i = 0; i < msgCount; ++i)
                {
                    var msg = m_QueuedMessages.Dequeue();

                    if (m_TypeToSubscribers.TryGetValue(msg.GetType(), out var subscribers))
                    {
                        ForEach(subscribers, msg, (e, msg1) => e.action(msg1));
                    }
                }
            }
            
            m_IsBroadcasting = false;
        }

        static void RemoveFirst(
            List<(HandleInternal handle, Action<object> action)> subscribers,
            HandleInternal search,
            Func<(HandleInternal handle, Action<object> action), HandleInternal, bool> pred)
        {
            for (var i = subscribers.Count - 1; i >= 0; --i)
            {
                if (pred(subscribers[i], search))
                {
                    subscribers.RemoveAt(i);
                    return;
                }
            }
        }

        static void ForEach<TItem, TParam>(List<TItem> items, TParam param, Action<TItem, TParam> action)
        {
            var count = items.Count;
            for (var i = 0; i < count; ++i)
            {
                try
                {
                    action(items[i], param);
                }
                catch(Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }
    }
}
