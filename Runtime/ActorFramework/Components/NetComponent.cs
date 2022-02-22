using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.ActorFramework
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

        public Type GetInputType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType;
        public Type[] GetLinkTypes(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType.GetGenericArguments();
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
        
        public Type[] GetLinkTypes(FieldInfo fieldInfo) => fieldInfo.FieldType.GetGenericArguments();
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

        public void SendCritical(TData data)
        {
            m_Net.SendCritical(m_Output.Receivers, data);
        }
    }

    [Component("1b938da3-daf8-433c-acd6-1da100a73887",
        typeof(NetInputAttribute),
        typeof(NetOutputAttribute),
        typeof(NetOutput<>),
        outputMultiplicity: Multiplicity.Any)]
    public class NetComponent : IRunnableComponent
    {
        // Make sure the default types do exist, so compiler/jitter
        // correctly generate the methods when Unsafe cast is used. il2cpp build may crash without this,
        // and it's not an issue in the implementation, as the spec is not clear whether or not it must be supported.
        static readonly NetMessage<object> k_UnusedMessage = new NetMessage<object>(null, null, false);
        static readonly NetContext<object> k_UnusedContext = new NetContext<object>(null);

        bool m_IsSuspended;
        readonly TimerComponent m_Timer;
        readonly ActorHandle m_Handle;
        readonly Scheduler m_Scheduler;
        readonly ConcurrentQueue<NetMessage<object>> m_HighPriorityMessages = new ConcurrentQueue<NetMessage<object>>();
        readonly ConcurrentQueue<NetMessage<object>> m_ReceivedMessages = new ConcurrentQueue<NetMessage<object>>();
        readonly Dictionary<Type, Action<NetContext<object>>> m_Actions = new Dictionary<Type, Action<NetContext<object>>>();

        Dictionary<ActorHandle, NetComponent> m_ActorSockets;
        
        public NetComponent(TimerComponent timer, ActorHandle handle, Scheduler scheduler, Dictionary<ActorHandle, NetComponent> actorSockets)
        {
            m_Timer = timer;
            m_Handle = handle;
            m_Scheduler = scheduler;
            m_ActorSockets = actorSockets;
        }

        public TickResult Tick(TimeSpan endTime)
        {
            while (!m_IsSuspended && (EnoughTimeRemaining(endTime) || !m_HighPriorityMessages.IsEmpty))
            {
                if (!ProcessMessage())
                    return TickResult.Wait;
            }

            return TickResult.Yield;
        }

        public void Register<TData>(Action<NetContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData), Unsafe.As<Action<NetContext<object>>>(action));
        }

        public void RegisterOpenGeneric<TData>(Action<NetContext<TData>> action)	
            where TData : class	
        {	
            m_Actions.Add(typeof(TData).GetGenericTypeDefinition(), Unsafe.As<Action<NetContext<object>>>(action));	
        }

        public void DelayedSend<TData>(TimeSpan delay, ActorHandle destination, TData data)
            where TData : class
        {
            if (delay <= TimeSpan.Zero)
                Send(destination, data);
            else
                m_Timer.DelayedExecute(delay, () => { Send(destination, data); });
        }

        public void Send<TData>(ActorHandle destination, TData data)
            where TData : class
        {
            var receiver = m_ActorSockets[destination];
            var netMessage = new NetMessage<TData>(m_Handle, data, false);
            var netContext = new NetContext<TData>(netMessage);
            netMessage.Ctx = netContext;
            receiver.EnqueueMessage(netMessage);
        }

        /// <summary>
        ///     If the same message is sent to many actors, make sure to use this call instead,
        ///     as there may be some mechanisms tracking the generated message for pooling purpose.
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="destinations"></param>
        /// <param name="data"></param>
        public void Send<TData>(List<ActorHandle> destinations, TData data)
            where TData : class
        {
            foreach (var destination in destinations)
            {
                var receiver = m_ActorSockets[destination];
                var netMessage = new NetMessage<TData>(m_Handle, data, false);
                var netContext = new NetContext<TData>(netMessage);
                netMessage.Ctx = netContext;
                receiver.EnqueueMessage(netMessage);
            }
        }

        /// <summary>
        ///     Sends a critical message to an actor, bypassing all queued messages. Use this
        ///     only when a late delivery could crash the application (e.g. critical memory signal).
        ///     Using this for not-so-critical messages could cause the application to crash because critical signals will be late.
        ///     This may also add a significant processing overhead because there's no batching.
        /// </summary>
        /// <typeparam name="TData"></typeparam>
        /// <param name="destination"></param>
        /// <param name="data"></param>
        public void SendCritical<TData>(ActorHandle destination, TData data)
            where TData : class
        {
            var receiver = m_ActorSockets[destination];
            var netMessage = new NetMessage<TData>(m_Handle, data, true);
            var netContext = new NetContext<TData>(netMessage);
            netMessage.Ctx = netContext;
            receiver.EnqueueMessageCritical(netMessage);
        }

        public void SendCritical<TData>(List<ActorHandle> destinations, TData data)
            where TData : class
        {
            foreach (var destination in destinations)
            {
                var receiver = m_ActorSockets[destination];
                var netMessage = new NetMessage<TData>(m_Handle, data, true);
                var netContext = new NetContext<TData>(netMessage);
                netMessage.Ctx = netContext;
                receiver.EnqueueMessageCritical(netMessage);
            }
        }

        public void Forward<TData>(ActorHandle destination, NetMessage<TData> msg)
            where TData : class
        {
            var receiver = m_ActorSockets[destination];
            if (msg.IsCritical)
                receiver.EnqueueMessageCritical(msg);
            else
                receiver.EnqueueMessage(msg);
        }

        public void Forward<TData>(List<ActorHandle> destinations, NetMessage<TData> msg)
            where TData : class
        {
            foreach (var destination in destinations)
                Forward(destination, msg);
        }

        public void Suspend()
        {
            m_IsSuspended = true;
        }

        public void Resume()
        {
            m_IsSuspended = false;
            m_Scheduler.WakeUpActor(m_Handle);
        }

        bool ProcessMessage()
        {
            if (m_HighPriorityMessages.TryDequeue(out var msg) ||
                m_ReceivedMessages.TryDequeue(out msg))
            {
                var dataType = GetDataType(msg);

                if (m_Actions.TryGetValue(dataType, out var action))
                {
                    ExecuteAction(action, msg);
                    return true;
                }

                if (dataType.IsGenericType)
                    dataType = dataType.GetGenericTypeDefinition();

                if (m_Actions.TryGetValue(dataType, out action))
                    ExecuteAction(action, msg);
                else
                    // Todo: Add a generic logger to remove unity dependency
                    Debug.LogError($"No action registered for {dataType.Name} in {m_Handle.Type.Name}. Discarding message.");

                return true;
            }

            return false;
        }

        void EnqueueMessage<TData>(NetMessage<TData> msg)
            where TData : class
        {
            m_ReceivedMessages.Enqueue(Unsafe.As<NetMessage<object>>(msg));
            m_Scheduler.WakeUpActor(m_Handle);
        }

        void EnqueueMessageCritical<TData>(NetMessage<TData> msg)
            where TData : class
        {
            m_HighPriorityMessages.Enqueue(Unsafe.As<NetMessage<object>>(msg));
            m_Scheduler.WakeUpActor(m_Handle);
        }

        void ExecuteAction(Action<NetContext<object>> action, NetMessage<object> msg)
        {
            try
            {
                action(msg.Ctx);
            }
            catch (Exception ex)
            {
                // Todo let the user input an Action for logging when action crashes
                Debug.LogError($"Error while processing message {msg.Data.GetType().Name} in actor {m_Handle.Type.Name} (from actor {msg.SourceId.Type.Name}): {ex}");
            }
        }

        static bool EnoughTimeRemaining(TimeSpan endTime)
        {
            var remaining = endTime - TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            return remaining > TimeSpan.FromMilliseconds(1);
        }
        
        static Type GetDataType(NetMessage<object> msg)
        {
            return msg.Data.GetType();
        }
    }

    /// <summary>
    ///     Context received when registered to message of type <see cref="TData"/>
    /// </summary>
    /// <typeparam name="TData"></typeparam>
    public sealed class NetContext<TData>
        where TData : class
    {
        /// <summary>
        ///     Access to the entire scope of the message
        /// </summary>
        public NetMessage<TData> Message;

        /// <summary>
        ///     The data sent by the actor
        /// </summary>
        public ref TData Data => ref Message.Data;

        public NetContext(NetMessage<TData> message)
        {
            Message = message;
        }
    }
}
