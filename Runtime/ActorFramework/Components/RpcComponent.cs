using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace Unity.Reflect.ActorFramework
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

        public Type[] GetLinkTypes(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType.GetGenericArguments();
        public Type GetInputType(MethodInfo methodInfo) => methodInfo.GetParameters()[0].ParameterType;
    }

    public class RpcOutputAttribute : Attribute, IOutputAttribute
    {
        public string Id { get; }
        public string DisplayName { get; }
        public bool Optional { get; }

        public RpcOutputAttribute() { }
        
        public RpcOutputAttribute(bool optional)
        {
            Optional = optional;
        }
        
        public RpcOutputAttribute(string guid, string displayName, bool optional = false)
        {
            Id = guid;
            DisplayName = displayName;
            Optional = optional;
            
            if (guid != null && !Guid.TryParse(guid, out _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");
        }
        
        public Type[] GetLinkTypes(FieldInfo fieldInfo)
        {
            var args = fieldInfo.FieldType.GetGenericArguments();
            if (args.Length == 1)
                args = fieldInfo.FieldType.BaseType.GetGenericArguments();

            return args;
        }
    }

    public class RpcOutput<TData> : RpcOutput<TData, object>
        where TData : class
    {
        public RpcOutput(RpcComponent rpc, RuntimeOutput output)
            : base(rpc, output) { }
    }

    public class RpcOutput<TData, TResult>
        where TData : class
        where TResult : class
    {
        RpcComponent m_Rpc;
        RuntimeOutput m_Output;

        public RpcOutput(RpcComponent rpc, RuntimeOutput output)
        {
            m_Rpc = rpc;
            m_Output = output;
        }
        
        public RpcComponent.Rpc<TState, TContext, TUserContext, TResult> Call<TState, TContext, TUserContext>(TState state, TContext context, TUserContext userContext, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
        {
            var rpc = m_Rpc.Call<TState, TContext, TUserContext, TData, TResult>(state, context, userContext, m_Output.Receivers.FirstOrDefault(), data);
            return rpc;
        }
        
        public RpcComponent.Rpc<TState, TContext, TUserContext, TResult> CallCritical<TState, TContext, TUserContext>(TState state, TContext context, TUserContext userContext, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
        {
            var rpc = m_Rpc.CallCritical<TState, TContext, TUserContext, TData, TResult>(state, context, userContext, m_Output.Receivers[0], data);
            return rpc;
        }
    }

    public class RpcComponentConnectionValidator : IActorGraphConnectionValidator
    {
        public bool WouldBeValid(ActorPort source, ActorPort destination, ActorSystemSetup asset)
        {
            var sourceConfig = asset.ActorConfigs
                .Select(x => x.InputConfigs.Concat(x.OutputConfigs).FirstOrDefault(x => x.Id == source.ConfigId))
                .First(x => x != null);
            var destinationConfig = asset.ActorConfigs
                .Select(x => x.InputConfigs.Concat(x.OutputConfigs).FirstOrDefault(x => x.Id == destination.ConfigId))
                .First(x => x != null);

            return AreReturnTypesCompatible(sourceConfig.TypeNormalizedFullName, destinationConfig.TypeNormalizedFullName);
        }

        public bool IsValid(ActorPort source, ActorPort destination, ActorSystemSetup asset)
        {
            var sourceConfig = asset.ActorConfigs
                .Select(x => x.InputConfigs.Concat(x.OutputConfigs).FirstOrDefault(x => x.Id == source.ConfigId))
                .First(x => x != null);
            var destinationConfig = asset.ActorConfigs
                .Select(x => x.InputConfigs.Concat(x.OutputConfigs).FirstOrDefault(x => x.Id == destination.ConfigId))
                .First(x => x != null);

            return AreReturnTypesCompatible(sourceConfig.TypeNormalizedFullName, destinationConfig.TypeNormalizedFullName);
        }

        static bool AreReturnTypesCompatible(string sourceFullName, string destinationFullName)
        {
            var sourceElem = ReflectionUtils.ExtractTypes(sourceFullName);
            var destinationElem = ReflectionUtils.ExtractTypes(destinationFullName);

            var sourceIsFullyTyped = sourceElem.GenericElems.Count == 2;
            var destinationIsFullyTyped = destinationElem.GenericElems.Count == 2;

            var objString = typeof(object).ToString();

            int sourceStartIndex = 0, sourceEndIndex = objString.Length;
            var sourceStr = objString;

            int destStartIndex = 0, destEndIndex = objString.Length;
            var destStr = objString;

            if (sourceIsFullyTyped)
            {
                sourceStartIndex = sourceElem.GenericElems[1].NameStartIndex;
                sourceEndIndex = sourceElem.GenericElems[1].NameEndIndex;
                sourceStr = sourceFullName;
            }

            if (destinationIsFullyTyped)
            {
                destStartIndex = destinationElem.GenericElems[1].NameStartIndex;
                destEndIndex = destinationElem.GenericElems[1].NameEndIndex;
                destStr = destinationFullName;
            }

            if (AreSubStringEqual(sourceStr, sourceStartIndex, sourceEndIndex, destStr, destStartIndex, destEndIndex))
                return true;
            
            var t1Str = sourceStr.Substring(sourceStartIndex, sourceEndIndex - sourceStartIndex);
            var t1ClosedType = ReflectionUtils.GetClosedTypeFromAnyAssembly(t1Str);

            var t2Str = destStr.Substring(destStartIndex, destEndIndex - destStartIndex);
            var t2ClosedType = ReflectionUtils.GetClosedTypeFromAnyAssembly(t2Str);

            return ReflectionUtils.AreRelated(t1ClosedType, t2ClosedType);
        }

        static bool AreSubStringEqual(string lhs, int lhsStartIndex, int lhsEndIndex, string rhs, int rhsStartIndex, int rhsEndIndex)
        {
            if (ReferenceEquals(lhs, rhs) && lhsStartIndex == rhsStartIndex && lhsEndIndex == rhsEndIndex)
                return true;

            if (lhsEndIndex - lhsStartIndex != rhsEndIndex - rhsStartIndex)
                return false;

            for (var i = 0; i < lhsEndIndex - lhsStartIndex; ++i)
            {
                if (lhs[lhsStartIndex + i] != rhs[rhsStartIndex + i])
                    return false;
            }

            return true;
        }
    }

    [Component("c7a05dee-b8a6-427f-b093-3e977c61b66d",
        typeof(RpcInputAttribute),
        typeof(RpcOutputAttribute),
        typeof(RpcOutput<,>),
        typeof(RpcComponentConnectionValidator),
        outputMultiplicity: Multiplicity.ExactlyOne)]
    public class RpcComponent
    {
        // Make sure the default types do exist, so compiler/jitter
        // correctly generate the methods when Unsafe cast is used. il2cpp build may crash without this,
        // and it's not an issue in the implementation, as the spec is not clear whether or not it must be supported.
        static readonly RpcMessage<object, object> k_UnusedMessage = new RpcMessage<object, object>(default, null);
        static readonly RpcContext<object, object> k_UnusedContext = new RpcContext<object, object>();

        readonly NetComponent m_Net;
        readonly ActorHandle m_Handle;

        readonly Dictionary<Type, Action<RpcContext<object, object>>> m_Actions = new Dictionary<Type, Action<RpcContext<object, object>>>();
        readonly Dictionary<int, HiddenContext<object, object, object>> m_PendingRpcs = new Dictionary<int, HiddenContext<object, object, object>>();

        int m_NextId;

        public RpcComponent(NetComponent net, ActorHandle handle)
        {
            m_Net = net;
            m_Handle = handle;

            m_Net.RegisterOpenGeneric<RpcMessage<object, object>>(OnRpcMessage);
            m_Net.Register<RpcSuccessMessage>(OnRpcSuccessMessage);
            m_Net.Register<RpcFailureMessage>(OnRpcFailureMessage);
        }
        
        public void Register<TData>(Action<RpcContext<TData>> action)
            where TData : class
        {
            m_Actions.Add(typeof(TData), Unsafe.As<Action<RpcContext<object, object>>>(action));
        }
        
        public void Register<TData, TResult>(Action<RpcContext<TData, TResult>> action)
            where TData : class
            where TResult : class
        {
            m_Actions.Add(typeof(TData), Unsafe.As<Action<RpcContext<object, object>>>(action));
        }

        public Rpc<TState, TContext, TUserContext, object> Call<TState, TContext, TUserContext, TData>(TState state, TContext context, TUserContext userContext, ActorHandle destination, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
        {
            return Call<TState, TContext, TUserContext, TData, object>(state, context, userContext, destination, data);
        }

        public Rpc<TState, TContext, TUserContext, TResult> Call<TState, TContext, TUserContext, TData, TResult>(TState state, TContext context, TUserContext userContext, ActorHandle destination, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
            where TResult : class
        {
            var (ctx, rpc) = PrepareCall<TState, TContext, TUserContext, TResult>(state, context, userContext);
            
            if (destination != null)
            {
                var rpcMsg = new RpcMessage<TData, TResult>(ctx.Id, data);
                var rpcCtx = new RpcContext<TData, TResult>();
                rpcMsg.Ctx = rpcCtx;
                m_Net.Send(destination, rpcMsg);
            }
            else
            {
                var rpcMsg = new RpcSuccessMessage(ctx.Id, NullData.Null);
                m_Net.Send(m_Handle, rpcMsg);
            }

            return rpc;
        }

        public Rpc<TState, TContext, TUserContext, object> CallCritical<TState, TContext, TUserContext, TData>(TState state, TContext context, TUserContext userContext, ActorHandle destination, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
        {
            return CallCritical<TState, TContext, TUserContext, TData, object>(state, context, userContext, destination, data);
        }

        public Rpc<TState, TContext, TUserContext, TResult> CallCritical<TState, TContext, TUserContext, TData, TResult>(TState state, TContext context, TUserContext userContext, ActorHandle destination, TData data)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TData : class
            where TResult : class
        {
            var (ctx, rpc) = PrepareCall<TState, TContext, TUserContext, TResult>(state, context, userContext);
            var rpcMsg = new RpcMessage<TData, TResult>(ctx.Id, data);
            var rpcCtx = new RpcContext<TData, TResult>();
            rpcMsg.Ctx = rpcCtx;
            m_Net.SendCritical(destination, rpcMsg);
            return rpc;
        }

        (HiddenContext<TState, TContext, TUserContext>, Rpc<TState, TContext, TUserContext, TResult>) PrepareCall<TState, TContext, TUserContext, TResult>(TState state, TContext context, TUserContext userContext)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TResult : class
        {
            var ctx = new HiddenContext<TState, TContext, TUserContext>
            {
                Id = m_NextId++,
                State = state,
                Context = context,
                UserContext = userContext
            };

            m_PendingRpcs.Add(ctx.Id, Unsafe.As<HiddenContext<object, object, object>>(ctx));

            return (ctx, new Rpc<TState, TContext, TUserContext, TResult>(ctx));
        }

        void OnRpcMessage(NetContext<RpcMessage<object, object>> ctx)
        {
            if (TryGetAction(ctx, out var action))
            {
                ctx.Data.Ctx.Net = m_Net;
                ctx.Data.Ctx.Message = ctx.Message;
                try
                {
                    action(ctx.Data.Ctx);
                }
                catch (Exception ex)
                {
                    ctx.Data.Ctx.SendFailure(ex);
                }
            }
            else
            {
                ctx.Data.Ctx.SendFailure(new InvalidRpcEndpointException(GetMessageType(ctx), m_Handle.Type));
            }
        }

        void OnRpcSuccessMessage(NetContext<RpcSuccessMessage> ctx)
        {
            if (!m_PendingRpcs.TryGetValue(ctx.Data.Id, out var rpcCtx))
            {
                var type = ctx.Data.Result?.GetType() ?? typeof(NullData);
                Debug.LogError($"Actor {ctx.Message.SourceId.Type.Name} is trying to send rpc success '{type.Name}', but there is no matching pending rpc in actor {m_Handle.Type.Name}.");
                return;
            }

            m_PendingRpcs.Remove(ctx.Data.Id);

            if (ctx.Data.Result != null &&
                ctx.Data.Result.GetType() != rpcCtx.ExpectedSuccessType &&
                !rpcCtx.ExpectedSuccessType.IsInstanceOfType(ctx.Data.Result))
            {
                var failureAction = Unsafe.As<Action<object, object, object, object>>(rpcCtx.FailureAction);
                failureAction(rpcCtx.State, rpcCtx.Context, rpcCtx.UserContext,
                    new Exception($"Rpc success type ({ctx.Data.Result.GetType()}) does not match expected type ({rpcCtx.ExpectedSuccessType}). Discarding result."));
                return;
            }
            
            var successAction = Unsafe.As<Action<object, object, object, object>>(rpcCtx.SuccessAction);
            successAction(rpcCtx.State, rpcCtx.Context, rpcCtx.UserContext, ctx.Data.Result);
        }

        void OnRpcFailureMessage(NetContext<RpcFailureMessage> ctx)
        {
            if (!m_PendingRpcs.TryGetValue(ctx.Data.Id, out var rpcCtx))
            {
                Debug.LogError($"Actor {ctx.Message.SourceId.Type.Name} is trying to send rpc failure, but there is no matching pending rpc in actor {m_Handle.Type.Name}. Exception: {ctx.Data.Exception}");
                return;
            }

            m_PendingRpcs.Remove(ctx.Data.Id);
            
            var failureAction = Unsafe.As<Action<object, object, object, object>>(rpcCtx.FailureAction);
            failureAction(rpcCtx.State, rpcCtx.Context, rpcCtx.UserContext, ctx.Data.Exception);
        }

        bool TryGetAction(NetContext<RpcMessage<object, object>> ctx, out Action<RpcContext<object, object>> action)
        {
            return m_Actions.TryGetValue(ctx.Data.Data.GetType(), out action);
        }

        static Type GetMessageType(NetContext<RpcMessage<object, object>> ctx)
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
            
            public Type ExpectedSuccessType;
        }

        public readonly struct Rpc<TState, TContext, TUserContext, TResult>
            where TState : class
            where TContext : class
            where TUserContext : class
            where TResult : class
        {
            readonly HiddenContext<TState, TContext, TUserContext> m_Ctx;

            public Rpc(object ctx)
            {
                m_Ctx = (HiddenContext<TState, TContext, TUserContext>)ctx;
            }

            public void Success(Action<TState, TContext, TUserContext, TResult> action)
            {
                m_Ctx.SuccessAction = action;
                m_Ctx.ExpectedSuccessType = typeof(TResult);
            }

            public void Success<TCastResult>(Action<TState, TContext, TUserContext, TCastResult> action)
                where TCastResult : class
            {
                m_Ctx.SuccessAction = action;
                m_Ctx.ExpectedSuccessType = typeof(TCastResult);
            }

            public void Failure(Action<TState, TContext, TUserContext, Exception> action)
            {
                m_Ctx.FailureAction = action;
            }
        }
    }

    public sealed class RpcContext<TData> : RpcContext<TData, object>
        where TData : class
    {
        // Never add methods here as it may break CLR assumptions
        // that this default ctor is called before an object of this type exists
        // This class is never instantiated but it's received by some delegates via implicit unsafe casting
    }
    
    public class RpcContext<TData, TResult>
        where TData : class
        where TResult : class
    {
        public NetMessage<RpcMessage<TData, TResult>> Message;
        internal NetComponent Net;

        public ref TData Data => ref Message.Data.Data;

        public void SendSuccess(TResult data)
        {
            Net.Send(Message.SourceId, new RpcSuccessMessage(Message.Data.Id, data));
        }

        public void SendFailure(Exception ex)
        {
            Net.Send(Message.SourceId, new RpcFailureMessage(Message.Data.Id, ex));
        }
    }

    public class InvalidRpcEndpointException : Exception
    {
        public InvalidRpcEndpointException(Type messageType, Type actorType)
            : base($"The rpc endpoint '{messageType.FullName}' does not exist in actor '{actorType.FullName}'") { }
    }
}
