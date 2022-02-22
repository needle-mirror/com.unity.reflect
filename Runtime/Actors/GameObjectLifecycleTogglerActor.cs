using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Actor("8f5e1042-9fd7-4028-aaf8-2394fc65b2df", true)]
    public class GameObjectTogglerActor
    {
        [PipeInput]
        void OnGameObjectEnabling(PipeContext<GameObjectEnabling> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
                go.GameObject.SetActive(true);
            
            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDisabling(PipeContext<GameObjectDisabling> ctx)
        {
            foreach (var go in ctx.Data.GameObjectIds)
                go.GameObject.SetActive(false);
            
            ctx.Continue();
        }
    }
}
