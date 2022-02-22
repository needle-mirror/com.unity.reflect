using Unity.Reflect.ActorFramework;
using UnityEngine;

namespace Unity.Reflect.Actors.Samples
{
    // actions on Unity objects are bound to main thread
    [Actor(isBoundToMainThread: true)]
    public class SampleAddColliderActor
    {
        // attribute to designate function as input connector
        [PipeInput]
        public void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            // grab the GameObjects from the context data
            // no need to check for null as the sample is really small and does not use live sync
            foreach (var gameObjectId in ctx.Data.GameObjectIds)
            {
                var go = gameObjectId.GameObject;
                
                // mesh filter required
                // exit if object already has collider
                if (go.TryGetComponent(out MeshFilter meshFilter) &&
                    !go.TryGetComponent(out MeshCollider _))
                {
                    // add the collider
                    var collider = go.AddComponent<MeshCollider>();
                    collider.sharedMesh = meshFilter.sharedMesh;
                }
            }

            ctx.Continue();
        }
    }
}
