using System.Collections.Generic;
using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Actor("da2a8d00-5bab-497f-aa31-1dca64446d49")]
    public class GameObjectLiveSyncRemoverActor
    {
#pragma warning disable 649
        NetOutput<DestroyGameObjectLifecycle> m_DestroyGameObjectLifecycleOutput;
#pragma warning restore 649

        readonly HashSet<DynamicGuid> m_ActiveIds = new HashSet<DynamicGuid>();

        [NetInput]
        void OnSpatialDataChanged(NetContext<SpatialDataChanged> ctx)
        {
            var delta = ctx.Data.Delta;
            
            foreach (var added in delta.Added)
                m_ActiveIds.Add(added.Id);

            var removing = new List<DynamicGuid>();
            
            foreach (var removed in delta.Removed)
            {
                m_ActiveIds.Remove(removed.Id);
                removing.Add(removed.Id);
            }

            foreach (var changed in delta.Changed)
            {
                m_ActiveIds.Remove(changed.Prev.Id);
                m_ActiveIds.Add(changed.Next.Id);
                removing.Add(changed.Prev.Id);
            }

            if (removing.Count > 0)
                m_DestroyGameObjectLifecycleOutput.Send(new DestroyGameObjectLifecycle(removing));
        }

        [PipeInput]
        void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            var removed = new List<DynamicGuid>();

            foreach (var go in ctx.Data.GameObjectIds)
            {
                if (!m_ActiveIds.Contains(go.Id))
                    removed.Add(go.Id);
            }
            
            if (removed.Count > 0)
                m_DestroyGameObjectLifecycleOutput.Send(new DestroyGameObjectLifecycle(removed));
            
            ctx.Continue();
        }
    }
}
