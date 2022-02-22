using System;
using System.Threading;
using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Actor("713b151d-5595-4808-94e1-439b7272a384", true)]
    public class JobActor
    {
        CancellationToken m_Token;

        public void Inject(CancellationToken token)
        {
            m_Token = token;
        }

        [RpcInput]
        void OnDelegateJob(RpcContext<DelegateJob> ctx)
        {
            if (m_Token.IsCancellationRequested)
            {
                ctx.SendFailure(new OperationCanceledException());
                return;
            }

            ctx.Data.Job(ctx, ctx.Data.JobInput);
        }
    }
}
