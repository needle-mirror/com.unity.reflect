using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.Reflect.Actor
{
    public class RpcInputAttribute : Attribute, IInputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public RpcInputAttribute() { }
        public RpcInputAttribute(string guid, string displayName)
        {
            Id = guid;
            DisplayName = displayName;
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }

        public Type GetInputMessageType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType.GetGenericArguments()[0];
        public Type GetInputType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType;
    }

    public class RpcOutputAttribute : Attribute, IOutputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }

        public RpcOutputAttribute() { }
        public RpcOutputAttribute(string guid, string displayName)
        {
            Id = guid;
            DisplayName = displayName;
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }

        public Type GetOutputMessageType(FieldInfo fieldInfo) => fieldInfo.FieldType.GetGenericArguments()[0];
    }

    public class RpcOutput<TData>
        where TData : class
    {
        RpcComponent m_Rpc;
        RuntimeOutput m_Output;

        public RpcOutput(RpcComponent rpc, RuntimeOutput output)
        {
            m_Rpc = rpc;
            m_Output = output;
        }
        
        public RpcComponent.Rpc<TState, TContext, TUserContext> Call<TState, TContext, TUserContext>(TState state, TContext context, TUserContext userContext, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
        {
            return m_Rpc.Call(state, context, userContext, m_Output.Receivers[0], data);
        }
    }

    [Component(
        inputAttributeType: typeof(RpcInputAttribute),
        outputAttributeType: typeof(RpcOutputAttribute),
        outputType: typeof(RpcOutput<>),
        outputMultiplicity: Multiplicity.ExactlyOne)]
    public class RpcComponent
    {
        readonly NetComponent m_Net;

        readonly Dictionary<Type, Action<RpcContext>> m_Actions = new Dictionary<Type, Action<RpcContext>>();
        readonly Dictionary<int, HiddenContext<object, object, object>> m_PendingRpcs = new Dictionary<int, HiddenContext<object, object, object>>();

        int m_NextId;

        public RpcComponent(NetComponent net)
        {
            m_Net = net;

            m_Net.RegisterOpenGeneric<RpcMessage<object>>(OnRpcMessage);
            m_Net.RegisterOpenGeneric<RpcSuccessMessage<object>>(OnRpcSuccessMessage);
            m_Net.Register<RpcFailureMessage>(OnRpcFailureMessage);
        }
        
        public void Register<TData>(Action<RpcContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData), Unsafe.As<Action<RpcContext>>(action));
        }

        public Rpc<TState, TContext, TUserContext> Call<TState, TContext, TUserContext, TData>(TState state, TContext context, TUserContext userContext, ActorRef destination, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
        {
            var (ctx, rpc) = PrepareCall(state, context, userContext);
            m_Net.Send(destination, new RpcMessage<TData>(ctx.Id, data));
            return Unsafe.As<Rpc<TState, TContext, TUserContext>>(rpc);
        }

        (HiddenContext<object, object, object>, Rpc<object, object, object>) PrepareCall<TState, TContext, TUserContext>(TState state, TContext context, TUserContext userContext)
        {
            var ctx = new HiddenContext<object, object, object>
            {
                Id = m_NextId++,
                Component = this,
                State = state,
                Context = context,
                UserContext = userContext
            };

            m_PendingRpcs.Add(ctx.Id, ctx);

            return (ctx, new Rpc<object, object, object>(ctx));
        }

        void OnRpcMessage(NetContext<RpcMessage<object>> ctx)
        {
            if (TryGetAction(ctx, out var action))
            {
                var rpcCtx = Unsafe.As<RpcContext>(new RpcContext<object>(m_Net, ctx));

                try
                {
                    action(rpcCtx);
                }
                catch (Exception ex)
                {
                    rpcCtx.SendFailure(ex);
                }
            }
            else
                Debug.LogError($"Nobody has registered an action for data type {GetMessageType(ctx).Name}. Discarding rpc request.");
        }

        void OnRpcSuccessMessage(NetContext<RpcSuccessMessage<object>> ctx)
        {
            if (!m_PendingRpcs.TryGetValue(ctx.Data.Id, out var rpcCtx))
            {
                Debug.LogError($"Rpc success callback does not exist for id {ctx.Data.Id}. Discarding result.");
                return;
            }

            if (ctx.Data.Result != null &&
                ctx.Data.Result.GetType() != rpcCtx.ExpectedSuccessType &&
                !rpcCtx.ExpectedSuccessType.IsInstanceOfType(ctx.Data.Result))
            {
                rpcCtx.FailureAction(rpcCtx.State, rpcCtx.Context, rpcCtx.UserContext,
                    new Exception($"Rpc success type ({ctx.Data.Result.GetType()}) does not match expected type ({rpcCtx.ExpectedSuccessType}). Discarding result."));
                return;
            }

            rpcCtx.SuccessAction(rpcCtx.State, rpcCtx.Context, rpcCtx.UserContext, ctx.Data.Result);
        }

        void OnRpcFailureMessage(NetContext<RpcFailureMessage> ctx)
        {
            if (!m_PendingRpcs.TryGetValue(ctx.Data.Id, out var rpcCtx))
            {
                Debug.LogError($"Rpc failure callback does not exist for id {ctx.Data.Id}. Discarding result.");
                return;
            }

            rpcCtx.FailureAction(rpcCtx.State, rpcCtx.Context, rpcCtx.UserContext, ctx.Data.Exception);
        }

        bool TryGetAction(NetContext<RpcMessage<object>> ctx, out Action<RpcContext> action)
        {
            return m_Actions.TryGetValue(ctx.Data.Data.GetType(), out action);
        }

        static Type GetMessageType(NetContext<RpcMessage<object>> ctx)
        {
            return ctx.Data.GetType().GetGenericArguments()[0];
        }
        
        class RpcContext : RpcContext<object>
        {
            RpcContext()
                : base(null, null) { }
        }

        class HiddenContext<TState, TContext, TUserContext>
            where TState : class
            where TContext : class
            where TUserContext : class
        {
            public int Id;

            public RpcComponent Component;
            public TState State;
            public TContext Context;
            public TUserContext UserContext;

            public Action<TState, TContext, TUserContext, object> SuccessAction;
            public Action<TState, TContext, TUserContext, Exception> FailureAction;
            
            public Type ExpectedSuccessType;
        }

        public class Rpc<TState, TContext, TUserContext>
            where TState : class
            where TContext : class
            where TUserContext : class
        {
            readonly HiddenContext<TState, TContext, TUserContext> m_Ctx;

            public Rpc(object ctx)
            {
                m_Ctx = (HiddenContext<TState, TContext, TUserContext>)ctx;
            }

            public void Success<TResult>(Action<TState, TContext, TUserContext, TResult> action)
                where TResult : class
            {
                m_Ctx.SuccessAction = Unsafe.As<Action<TState, TContext, TUserContext, object>>(action);
                m_Ctx.ExpectedSuccessType = typeof(TResult);
            }

            public void Failure(Action<TState, TContext, TUserContext, Exception> action)
            {
                m_Ctx.FailureAction = action;
            }
        }
    }
    
    public class RpcContext<TData>
        where TData : class
    {
        readonly NetComponent m_Net;

        public NetContext<RpcMessage<TData>> Ctx;

        public RpcContext(NetComponent net, NetContext<RpcMessage<TData>> parentContext)
        {
            m_Net = net;
            Ctx = parentContext;
        }

        public RpcMessage<TData> Message => Ctx.Data;
        public TData Data => Ctx.Data.Data;

        public void SendSuccess<TResult>(TResult data)
            where TResult : class
        {
            m_Net.Send(Ctx.SourceId, new RpcSuccessMessage<TResult>(Ctx.Data.Id, data));
        }

        public void SendFailure(Exception ex)
        {
            m_Net.Send(Ctx.SourceId, new RpcFailureMessage(Ctx.Data.Id, ex));
        }
    }
}
