using Unity.Reflect.ActorFramework;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Actors.Samples
{
    [RequireComponent(typeof(ReflectActorSystem))]
    public class SampleActorSystemStarter : MonoBehaviour
    {
        void Start()
        {
            // Get the scene component
            var actorSystem = gameObject.GetComponent<ReflectActorSystem>();
           
            // Initialize and start the system
            actorSystem.Instantiate();
            actorSystem.StartActorSystem();
            
            // Call the RpcInput that will trigger the streaming
            actorSystem.ForwardRpc<ManifestActor, GetManifests, NullData>(new GetManifests(new GetManifestOptions()));
        }
    }
}
