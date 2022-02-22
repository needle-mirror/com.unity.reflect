using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Actor("a2cb4db7-f83f-49b4-9192-eb82d17acaea")]
    public class GameObjectLifecycleEventDispatcherActor
    {
#pragma warning disable 649
        RpcOutput<ExecuteSyncEvent> m_ExecuteSyncEventOutput;
#pragma warning restore 649

        [PipeInput]
        void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            var rpc = m_ExecuteSyncEventOutput.Call(this, ctx, (object)null, new ExecuteSyncEvent(ctx.Data));
            rpc.Success((self, ctx, userCtx, _) => ctx.Continue());
            rpc.Failure((self, ctx, userCtx, ex) => ctx.Continue());
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            var rpc = m_ExecuteSyncEventOutput.Call(this, ctx, (object)null, new ExecuteSyncEvent(ctx.Data));
            rpc.Success((self, ctx, userCtx, _) => ctx.Continue());
            rpc.Failure((self, ctx, userCtx, ex) => ctx.Continue());
        }

        [PipeInput]
        void OnGameObjectEnabling(PipeContext<GameObjectEnabling> ctx)
        {
            var rpc = m_ExecuteSyncEventOutput.Call(this, ctx, (object)null, new ExecuteSyncEvent(ctx.Data));
            rpc.Success((self, ctx, userCtx, _) => ctx.Continue());
            rpc.Failure((self, ctx, userCtx, ex) => ctx.Continue());
        }

        [PipeInput]
        void OnGameObjectDisabling(PipeContext<GameObjectDisabling> ctx)
        {
            var rpc = m_ExecuteSyncEventOutput.Call(this, ctx, (object)null, new ExecuteSyncEvent(ctx.Data));
            rpc.Success((self, ctx, userCtx, _) => ctx.Continue());
            rpc.Failure((self, ctx, userCtx, ex) => ctx.Continue());
        }
    }
}
