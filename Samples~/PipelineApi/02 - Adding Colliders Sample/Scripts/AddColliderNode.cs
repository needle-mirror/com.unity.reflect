using System;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEngine.Reflect.Pipeline.Samples
{
    public class AddColliderNode : ReflectNode<AddCollider>
    {
        public GameObjectInput input = new GameObjectInput();

        protected override AddCollider Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
            var node = new AddCollider();
            input.streamEvent = node.OnGameObjectEvent;
            return node;
        }
    }

    public class AddCollider : IReflectNodeProcessor
    {
        public void OnGameObjectEvent(SyncedData<GameObject> stream, StreamEvent streamEvent)
        {
            if (streamEvent == StreamEvent.Added)
            {
                var gameObject = stream.data;
                if (!gameObject.TryGetComponent(out MeshFilter meshFilter))
                    return;

                // When multiple instances of the same SyncObject get converted, instead of creating a GameObject every time from scratch,
                // the InstanceConverterNode duplicates the first generated GameObject.
                // This allows a big performance gain, but the drawback is that the output GameObject is not guaranteed to be "clean" ;
                // because any change that has been performed to the first GameObject (from a Node or anywhere else) will be present
                // in the following instances of the same SyncObject.

                // In this situation, the AddColliderNode automatically adds a MeshCollider to the first GameObject.
                // Any new instance of the same SyncObject will thus trigger the duplication of this first GameObject.
                // For that reason, we add this safety check not to add the same MeshCollider component twice.
                if (gameObject.TryGetComponent(out MeshCollider _))
                    return;

                var collider = gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = meshFilter.sharedMesh;
            }
        }

        public void OnPipelineInitialized()
        {
            // not needed
        }

        public void OnPipelineShutdown()
        {
            // not needed
        }
    }
}
