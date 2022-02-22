using System;

namespace Unity.Reflect.ActorFramework
{
    public class NullData
    {
        public static NullData Null = null;
    }

    public sealed class NetMessage<TData>
        where TData : class
    {
        public NetContext<TData> Ctx;
        public ActorHandle SourceId;
        public TData Data;
        public bool IsCritical;

        public NetMessage(ActorHandle sourceId, TData data, bool isCritical)
        {
            SourceId = sourceId;
            Data = data;
            IsCritical = isCritical;
        }
    }

    public sealed class EventMessage<TData>
        where TData : class
    {
        public EventContext<TData> Ctx;
        public TData Data;

        public EventMessage(TData data)
        {
            Data = data;
        }
    }

    public sealed class RpcMessage<TData, TResult>
        where TData : class
        where TResult : class
    {
        public RpcContext<TData, TResult> Ctx;
        public int Id;
        public TData Data;

        public RpcMessage(int id, TData data)
        {
            Id = id;
            Data = data;
        }
    }

    public sealed class RpcSuccessMessage
    {
        public int Id;
        public object Result;

        public RpcSuccessMessage(int id, object result)
        {
            Id = id;
            Result = result;
        }
    }

    public sealed class RpcFailureMessage
    {
        public int Id;
        public Exception Exception;

        public RpcFailureMessage(int id, Exception ex)
        {
            Id = id;
            Exception = ex;
        }
    }

    public sealed class PipeMessage<TData>
        where TData : class
    {
        public PipeContext<TData> Ctx;
        public int Id;
        public ActorHandle Origin;
        public ActorHandle Next;
        public Exception Exception;
        public TData Data;

        public PipeMessage(int id, ActorHandle origin, TData data)
        {
            Id = id;
            Origin = origin;
            Data = data;
        }
    }
}
