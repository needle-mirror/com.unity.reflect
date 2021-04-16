using Unity.Reflect.Actor;

namespace Unity.Reflect.Streaming
{
    [Actor(isBoundToMainThread: true)]
    public class JobActor
    {
        [RpcInput]
        void OnDelegateJob(RpcContext<DelegateJob> ctx)
        {
            ctx.Data.Job(ctx, ctx.Data.JobInput);
        }
    }
}
