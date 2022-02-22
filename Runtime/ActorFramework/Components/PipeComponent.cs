using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.Reflect.ActorFramework
{
    public class PipeInputAttribute : Attribute, IInputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public PipeInputAttribute() { }
        public PipeInputAttribute(string guid = null, string displayName = null)
        {
            Id = guid;
            DisplayName = displayName;
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }

        public Type[] GetLinkTypes(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType.GetGenericArguments();
        public Type GetInputType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType;
    }

    public class PipeOutputAttribute : Attribute, IOutputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public PipeOutputAttribute() { }
        public PipeOutputAttribute(string guid = null, string displayName = null)
        {
            Id = guid;
            DisplayName = displayName;
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }

        public Type GetOutputType(FieldInfo fieldInfo) => fieldInfo.FieldType;
        public Type[] GetLinkTypes(FieldInfo fieldInfo) => fieldInfo.FieldType.GetGenericArguments();
    }

    public class PipeOutput<TData>
        where TData : class
    {
        ActorHandle m_Self;
        PipeComponent m_Pipe;
        RuntimeOutput m_Output;

        public PipeOutput(ActorHandle self, PipeComponent pipe, RuntimeOutput output)
        {
            m_Self = self;
            m_Pipe = pipe;
            m_Output = output;
        }
        
        public PipeComponent.Pipe<TState, TContext, TUserContext, TData> Push<TState, TContext, TUserContext>(TState state, TContext context, TUserContext userContext, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
        {
            if (m_Output.Receivers.Count == 0)
                return m_Pipe.Push(state, context, userContext, m_Self, data);

            return m_Pipe.Push(state, context, userContext, m_Output.Receivers[0], data);
        }
        
        public PipeComponent.Pipe<TState, TContext, TUserContext, TData> PushCritical<TState, TContext, TUserContext>(TState state, TContext context, TUserContext userContext, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
        {
            if (m_Output.Receivers.Count == 0)
                return m_Pipe.PushCritical(state, context, userContext, m_Self, data);

            return m_Pipe.PushCritical(state, context, userContext, m_Output.Receivers[0], data);
        }
    }

    [Component("03137b05-eb1c-4cab-90c3-2835663696f7",
        typeof(PipeInputAttribute),
        typeof(PipeOutputAttribute),
        typeof(PipeOutput<>),
        outputMultiplicity: Multiplicity.ZeroOrOne)]
    public class PipeComponent
    {
        // Make sure the default types do exist, so compiler/jitter
        // correctly generate the methods when Unsafe cast is used. il2cpp build may crash without this,
        // and it's not an issue in the implementation, as the spec is not clear whether or not it must be supported.
        static readonly PipeMessage<object> k_UnusedMessage = new PipeMessage<object>(default, default, default);
        static readonly PipeContext<object> k_UnusedContext = new PipeContext<object>();

        readonly NetComponent m_Net;
        readonly ActorHandle m_Handle;

        readonly Dictionary<Type, ActionContext> m_Actions = new Dictionary<Type, ActionContext>();
        readonly Dictionary<int, HiddenContext<object, object, object>> m_PendingCallbacks = new Dictionary<int, HiddenContext<object, object, object>>();

        int m_NextId;

        public PipeComponent(NetComponent net, ActorHandle handle)
        {
            m_Net = net;
            m_Handle = handle;

            m_Net.RegisterOpenGeneric<PipeMessage<object>>(OnPipeMessage);
        }
        
        public void Register<TData>(Action<PipeContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData), new ActionContext(null, Unsafe.As<Action<PipeContext<object>>>(action)));
        }
        
        public void Register<TData>(ActorHandle next, Action<PipeContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData), new ActionContext(next, Unsafe.As<Action<PipeContext<object>>>(action)));
        }

        internal void SetNextFor(Type dataType, ActorHandle next)
        {
            m_Actions[dataType].Next = next;
        }

        public Pipe<TState, TContext, TUserContext, TData> Push<TState, TContext, TUserContext, TData>(TState state, TContext context, TUserContext userContext, ActorHandle destination, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
        {
            var (ctx, pipe) = PrepareCall<TState, TContext, TUserContext, TData>(state, context, userContext);
            var pipeMsg = new PipeMessage<TData>(ctx.Id, m_Handle, data);
            var pipeCtx = new PipeContext<TData>();
            pipeMsg.Ctx = pipeCtx;
            m_Net.Send(destination, pipeMsg);
            return pipe;
        }

        public Pipe<TState, TContext, TUserContext, TData> PushCritical<TState, TContext, TUserContext, TData>(TState state, TContext context, TUserContext userContext, ActorHandle destination, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
        {
            var (ctx, pipe) = PrepareCall<TState, TContext, TUserContext, TData>(state, context, userContext);
            var pipeMsg = new PipeMessage<TData>(ctx.Id, m_Handle, data);
            var pipeCtx = new PipeContext<TData>();
            pipeMsg.Ctx = pipeCtx;
            m_Net.SendCritical(destination, pipeMsg);
            return pipe;
        }

        (HiddenContext<TState, TContext, TUserContext>, Pipe<TState, TContext, TUserContext, TData>) PrepareCall<TState, TContext, TUserContext, TData>(TState state, TContext context, TUserContext userContext)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
        {
            var ctx = new HiddenContext<TState, TContext, TUserContext>
            {
                Id = m_NextId++,
                State = state,
                Context = context,
                UserContext = userContext
            };

            m_PendingCallbacks.Add(ctx.Id, Unsafe.As<HiddenContext<object, object, object>>(ctx));

            return (ctx, new Pipe<TState, TContext, TUserContext, TData>(ctx));
        }

        void OnPipeMessage(NetContext<PipeMessage<object>> ctx)
        {
            var origin = ctx.Message.Data.Origin;
            if (origin != m_Handle)
            {
                if (TryGetAction(ctx, out var action))
                {
                    ctx.Data.Ctx.Net = m_Net;
                    ctx.Data.Ctx.Message = ctx.Message;
                    ctx.Data.Next = action.Next ?? ctx.Data.Origin;
                    try
                    {
                        action.Action(ctx.Data.Ctx);
                    }
                    catch (Exception ex)
                    {
                        if (origin != m_Handle)
                        {
                            ctx.Message.Data.Exception = ex;
                            m_Net.Forward(origin, ctx.Message);
                        }
                    }
                }
                else
                {
                    if (origin != m_Handle)
                    {
                        ctx.Message.Data.Exception = new InvalidPipeEndpointException(GetMessageType(ctx), m_Handle.Type);
                        m_Net.Forward(origin, ctx.Message);
                    }
                }
            }
            else
            {
                if (ctx.Message.Data.Exception == null)
                    OnPipeSuccessMessage(ctx);
                else
                    OnPipeFailureMessage(ctx);
            }
        }

        void OnPipeSuccessMessage(NetContext<PipeMessage<object>> ctx)
        {
            if (!m_PendingCallbacks.TryGetValue(ctx.Data.Id, out var pipeCtx))
            {
                var type = ctx.Data.Data?.GetType() ?? typeof(NullData);
                Debug.LogError($"Actor {ctx.Message.SourceId.Type.Name} is trying to send pipe success '{type.Name}', but there is no matching pending pipe in actor {m_Handle.Type.Name}.");
                return;
            }

            m_PendingCallbacks.Remove(ctx.Data.Id);

            var successAction = Unsafe.As<Action<object, object, object, object>>(pipeCtx.SuccessAction);
            successAction(pipeCtx.State, pipeCtx.Context, pipeCtx.UserContext, ctx.Data.Data);
        }

        void OnPipeFailureMessage(NetContext<PipeMessage<object>> ctx)
        {
            if (!m_PendingCallbacks.TryGetValue(ctx.Data.Id, out var pipeCtx))
            {
                Debug.LogError($"Actor {ctx.Message.SourceId.Type.Name} is trying to send pipe failure, but there is no matching pending pipe in actor {m_Handle.Type.Name}. Exception: {ctx.Data.Exception}");
                return;
            }

            m_PendingCallbacks.Remove(ctx.Data.Id);
            
            var failureAction = Unsafe.As<Action<object, object, object, object>>(pipeCtx.FailureAction);
            failureAction(pipeCtx.State, pipeCtx.Context, pipeCtx.UserContext, ctx.Data.Exception);
        }

        bool TryGetAction(NetContext<PipeMessage<object>> ctx, out ActionContext action)
        {
            return m_Actions.TryGetValue(ctx.Data.Data.GetType(), out action);
        }

        static Type GetMessageType(NetContext<PipeMessage<object>> ctx)
        {
            return ctx.Data.GetType().GetGenericArguments()[0];
        }
        
        class HiddenContext<TState, TContext, TUserContext>
            where TState : class
            where TContext : class
            where TUserContext : class
        {
            public int Id;
            
            public TState State;
            public TContext Context;
            public TUserContext UserContext;

            public object SuccessAction;
            public object FailureAction;
        }

        class ActionContext
        {
            public ActorHandle Next;
            public Action<PipeContext<object>> Action;

            public ActionContext(ActorHandle next, Action<PipeContext<object>> action)
            {
                Next = next;
                Action = action;
            }
        }

        public readonly struct Pipe<TState, TContext, TUserContext, TData>
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
        {
            readonly HiddenContext<TState, TContext, TUserContext> m_Ctx;

            public Pipe(object ctx)
            {
                m_Ctx = (HiddenContext<TState, TContext, TUserContext>)ctx;
            }

            public void Success(Action<TState, TContext, TUserContext, TData> action)
            {
                m_Ctx.SuccessAction = action;
            }

            public void Failure(Action<TState, TContext, TUserContext, Exception> action)
            {
                m_Ctx.FailureAction = action;
            }
        }
    }
    
    public class PipeContext<TData>
        where TData : class
    {
        public NetMessage<PipeMessage<TData>> Message;
        internal NetComponent Net;

        public ref TData Data => ref Message.Data.Data;

        public void Continue()
        {
            Net.Forward(Message.Data.Next, Message);
        }
    }

    public class InvalidPipeEndpointException : Exception
    {
        public InvalidPipeEndpointException(Type messageType, Type actorType)
            : base($"The pipe endpoint '{messageType.FullName}' does not exist in actor '{actorType.FullName}'") { }
    }

    public class PipePortDescriptorScanner : IPortDescriptorScanner
    {
        public List<PortDescriptor> Scan(ActorDescriptor actor, List<ComponentDescriptor> components)
        {
            var methods = GetInputMethods(actor.Type);

            var ports = methods.Select(x =>
                {
                    var attr = PortDescriptorScanner.GetInputAttribute(x);
                    return new PortDescriptor
                    {
                        Id = Guid.NewGuid(),
                        IsGeneratedId = true,
                        IsVirtual = true,
                        PortType = PortType.Output,
                        SyntaxTreeName = $"__{nameof(PipeComponent)}__" + x.Name,
                        DisplayName = attr.DisplayName ?? PortDescriptorScanner.Prettify(x.Name),
                        Type = attr.GetInputType(x),
                        LinkTypes = attr.GetLinkTypes(x)
                    };
                })
                .ToList();

            return ports;
        }
        
        static List<MethodInfo> GetInputMethods(Type actorType)
        {
            var methods = actorType
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy)
                .Where(x => x.GetCustomAttribute<PipeInputAttribute>() != null)
                .ToList();

            if (actorType.BaseType != null &&
                actorType.BaseType.GetCustomAttribute<ActorAttribute>() != null)
            {
                methods.AddRange(GetInputMethods(actorType.BaseType));
            }

            return methods;
        }
    }
}
