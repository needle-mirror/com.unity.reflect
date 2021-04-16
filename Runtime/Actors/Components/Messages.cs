using System;

namespace Unity.Reflect.Actor
{
    public class NetContext : NetContext<object>
    {
        NetContext() : base(null, null) { }
    }

    /// <summary>
    ///     Message with its data type as generic argument.
    /// </summary>
    /// <remarks>
    ///     <see cref="T"/> must be a reference type to allow unsafe cast between <see cref="NetContext"/> and <see cref="NetContext{T}"/>
    /// </remarks>
    /// <typeparam name="T"></typeparam>
    public class NetContext<T> where T : class
    {
        public ActorRef SourceId { get; }
        public T Data { get; }

        public NetContext(ActorRef sourceId, T data)
        {
            SourceId = sourceId;
            Data = data;
        }
    }

    public class EventMessage<T> where T : class
    {
        public T Data { get; }

        public EventMessage(T data)
        {
            Data = data;
        }
    }

    public class RpcMessage<T> where T : class
    {
        public int Id { get; }
        public T Data { get; }

        public RpcMessage(int id, T data)
        {
            Id = id;
            Data = data;
        }
    }

    public class RpcSuccessMessage<TResult>
        where TResult : class
    {
        public int Id { get; }
        public TResult Result { get; }

        public RpcSuccessMessage(int id, TResult result)
        {
            Id = id;
            Result = result;
        }
    }

    public class RpcFailureMessage
    {
        public int Id { get; }
        public Exception Exception { get; }

        public RpcFailureMessage(int id, Exception ex)
        {
            Id = id;
            Exception = ex;
        }
    }
}
