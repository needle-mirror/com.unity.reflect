using Unity.Reflect.Streaming;
using UnityEngine;

namespace Unity.Reflect.Actor.Samples
{
    // actions on Unity objects are bound to main thread
    [Actor(isBoundToMainThread: true)]
    public class AddColliderActor
    {
        // attribute to designate function as input connector
        [NetInput]
        public void OnGameObjectCreated(NetContext<GameObjectCreated> ctx)
        {
            // grab the GameObject from the context data
            var gameObject = ctx.Data.GameObject;

            // mesh filter required
            if (!gameObject.TryGetComponent(out MeshFilter meshFilter))
                return;

            // exit if object already has collider
            if (gameObject.TryGetComponent(out MeshCollider collider))
                return;

            // add the collider
            collider = gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = meshFilter.sharedMesh;
        }
    }
}
