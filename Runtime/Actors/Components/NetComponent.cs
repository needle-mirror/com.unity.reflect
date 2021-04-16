using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using Reflect.Unity.Actor;
using Unity.Reflect.Streaming;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.Actor
{
    public class NetInputAttribute : Attribute, IInputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public NetInputAttribute() { }
        public NetInputAttribute(string guid, string displayName)
        {
            Id = guid;
            DisplayName = displayName;
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }

        public Type GetInputMessageType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType.GetGenericArguments()[0];
        public Type GetInputType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType;
    }

    public class NetOutputAttribute : Attribute, IOutputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public NetOutputAttribute() { }
        public NetOutputAttribute(string guid, string displayName)
        {
            Id = guid;
            DisplayName = displayName;
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }

        public Type GetOutputMessageType(FieldInfo fieldInfo) => fieldInfo.FieldType.GetGenericArguments()[0];
    }

    public class NetOutput<TData>
        where TData : class
    {
        NetComponent m_Net;
        RuntimeOutput m_Output;

        public NetOutput(NetComponent net, RuntimeOutput output)
        {
            m_Net = net;
            m_Output = output;
        }
        
        public void Send(TData data)
        {
            m_Net.Send(m_Output.Receivers, data);
        }
    }

    [Component(
        inputAttributeType: typeof(NetInputAttribute),
        outputAttributeType: typeof(NetOutputAttribute),
        outputType: typeof(NetOutput<>),
        outputMultiplicity: Multiplicity.OneOrMore)]
    public class NetComponent : IRunnableComponent
    {
        readonly ActorRef m_ActorRef;
        readonly Scheduler m_Scheduler;
        // Simulate actor connections with direct pointer and concurrent queue for now
        // Will implement a real async connection later.
        readonly ConcurrentQueue<NetContext> m_ReceivedMessages = new ConcurrentQueue<NetContext>();
        readonly Dictionary<Type, Action<NetContext>> m_Actions = new Dictionary<Type, Action<NetContext>>();
        Dictionary<ActorRef, NetComponent> m_ActorSockets;

        [ComponentCtor]
        public NetComponent(ActorRef actorRef, Scheduler scheduler, Dictionary<ActorRef, NetComponent> actorSockets)
        {
            m_ActorRef = actorRef;
            m_Scheduler = scheduler;
            m_ActorSockets = actorSockets;
        }

        public bool Tick(TimeSpan endTime, CancellationToken token)
        {
            while (!token.IsCancellationRequested &&
                EnoughTimeRemaining(endTime))
            {
                if (!ProcessMessage())
                    return true;
            }

            return false;
        }

        public void Register<TData>(Action<NetContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData), Unsafe.As<Action<NetContext>>(action));
        }
        
        public void RegisterOpenGeneric<TData>(Action<NetContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData).GetGenericTypeDefinition(), Unsafe.As<Action<NetContext>>(action));
        }

        public void Send<TData>(ActorRef destination, TData data)
            where TData : class
        {
            var receiver = m_ActorSockets[destination];
            receiver.EnqueueMessage(new NetContext<TData>(m_ActorRef, data));
        }

        /// <summary>
        ///     If the same message is sent to many actors, make sure to use this call instead,
        ///     as there may be some mechanisms tracking the generated message for pooling purpose.
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="destinations"></param>
        /// <param name="data"></param>
        public void Send<TData>(List<ActorRef> destinations, TData data)
            where TData : class
        {
            foreach (var destination in destinations)
            {
                var receiver = m_ActorSockets[destination];
                receiver.EnqueueMessage(new NetContext<TData>(m_ActorRef, data));
            }
        }

        bool ProcessMessage()
        {
            if (m_ReceivedMessages.TryDequeue(out var ctx))
            {
                var dataType = GetDataType(ctx);

                if (m_Actions.TryGetValue(dataType, out var action))
                {
                    ExecuteAction(action, ctx);
                    return true;
                }

                if (dataType.IsGenericType)
                    dataType = dataType.GetGenericTypeDefinition();

                if (m_Actions.TryGetValue(dataType, out action))
                    ExecuteAction(action, ctx);
                else
                    // Todo: Add a generic logger to remove unity dependency
                    Debug.LogError($"No action registered for {dataType.Name} in {m_ActorRef.Type.Name}. Discarding message.");

                return true;
            }

            return false;
        }

        void EnqueueMessage<TData>(NetContext<TData> ctx)
            where TData : class
        {
            m_ReceivedMessages.Enqueue(Unsafe.As<NetContext>(ctx));

            // Awake itself when another thread enqueue a message in this queue
            m_Scheduler.AwakeActor(m_ActorRef);
        }

        void ExecuteAction(Action<NetContext> action, NetContext ctx)
        {
            try
            {
                action(ctx);
            }
            catch (Exception ex)
            {
                // Todo let the user input an Action for logging when action crashes
                Debug.LogError($"Error while processing message {ctx.Data.GetType().Name} in actor {m_ActorRef.Type.Name} (from actor {ctx.SourceId.Type.Name}): {ex}");
            }
        }

        static bool EnoughTimeRemaining(TimeSpan endTime)
        {
            var remaining = endTime - TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            return remaining > TimeSpan.FromMilliseconds(1);
        }
        
        static Type GetDataType(NetContext<object> ctx)
        {
            return ctx.Data.GetType();
        }
    }
}
