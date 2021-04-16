using Unity.Reflect.Model;
using Unity.Reflect.Streaming;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Actor.Samples
{
    public class BasicActorSystemSample : MonoBehaviour
    {
        public Transform root;

        void Start()
        {
            // Create the actor setups and connections
            var asset = CreateBasicActorSystemAsset();

            // Assign root transform to GameObjectBuilderActor
            var gameObjectBuilderSetup = asset.GetActorSetup<GameObjectBuilderActor>();
            gameObjectBuilderSetup.GetActorSettings<GameObjectBuilderActor.Settings>().Root = root;
            
            // Instantiate and start the system
            Run(asset);
        }

        void Run(ActorSystemSetup asset)
        {
            // Create the scene component
            var reflectBootstrapper = gameObject.AddComponent<RuntimeReflectBootstrapper>();
            reflectBootstrapper.EnableExperimentalActorSystem = true;
            reflectBootstrapper.Asset = asset;

            // Start the system
            reflectBootstrapper.InstantiateAndStart(asset);
            var runtimeBridgeActor = reflectBootstrapper.FindActor<SampleBridgeActor>();
            runtimeBridgeActor.SendUpdateManifests();
        }

        static ActorSystemSetup CreateBasicActorSystemAsset()
        {
            // Create the system asset
            var asset = ActorUtils.CreateActorSystemSetup();

            // Create the actors
            var bridgeSetup = asset.CreateActorSetup<SampleBridgeActor>();                     // bridge from the UI
            var manifestSetup = asset.CreateActorSetup<ManifestActor>();                       // load data from the manifest
            var spatialSetup = asset.CreateActorSetup<SampleSpatialActor>();                   // filter spatial instance entries
            var instanceStreamingSetup = asset.CreateActorSetup<InstanceStreamingActor>();     // stream instance entries in batches
            
            var gameObjectCreatorSetup = asset.CreateActorSetup<GameObjectCreatorActor>();     // gather SyncMesh, SyncMaterial and SyncTexture dependencies
            var gameObjectConverterSetup = asset.CreateActorSetup<GameObjectConverterActor>(); // gather Mesh, Material and Texture asset dependencies
            var gameObjectBuilderSetup = asset.CreateActorSetup<GameObjectBuilderActor>();     // instantiate GameObject
            
            var unityResourceSetup = asset.CreateActorSetup<UnityResourceActor>();             // manage Mesh, Material and Texture assets
            var modelResourceSetup = asset.CreateActorSetup<ModelResourceActor>();             // manage SyncModel
            var dataProviderSetup = asset.CreateActorSetup<SampleDataProviderActor>();         // provide access to resource files
            
            var meshConverterSetup = asset.CreateActorSetup<MeshConverterActor>();             // convert SyncMesh to Mesh
            var materialConverterSetup = asset.CreateActorSetup<MaterialConverterActor>();     // convert SyncMaterial to Material
            var textureConverterSetup = asset.CreateActorSetup<TextureConverterActor>();       // convert SyncTexture to Texture

            var jobSetup = asset.CreateActorSetup<JobActor>();                                 // run jobs on the main thread
            var pubSubSetup = asset.CreateActorSetup<PubSubActor>();                           // manage event system
            var dummySetup = asset.CreateActorSetup<SampleDummyActor>();                       // connect to mandatory outputs not required in this sample

            // Connect the actors
            asset.ConnectRpc<UpdateManifests>(bridgeSetup, manifestSetup);
            
            asset.ConnectRpc<GetManifests>(manifestSetup, dataProviderSetup);
            asset.ConnectNet<EntryDataChanged>(manifestSetup, dummySetup);
            asset.ConnectNet<SpatialDataChanged>(manifestSetup, spatialSetup);
            
            asset.ConnectNet<UpdateStreaming>(spatialSetup, instanceStreamingSetup);

            asset.ConnectNet<GameObjectCreated>(instanceStreamingSetup, dummySetup);
            asset.ConnectRpc<CreateGameObject>(instanceStreamingSetup, gameObjectCreatorSetup);

            asset.ConnectNet<ReleaseEntryData>(gameObjectCreatorSetup, manifestSetup);
            asset.ConnectNet<ReleaseResource>(gameObjectCreatorSetup, modelResourceSetup);
            asset.ConnectRpc<AcquireEntryData>(gameObjectCreatorSetup, manifestSetup);
            asset.ConnectRpc<AcquireResource>(gameObjectCreatorSetup, modelResourceSetup);
            asset.ConnectRpc<AcquireEntryDataFromModelData>(gameObjectCreatorSetup, manifestSetup);
            asset.ConnectRpc<ConvertToGameObject>(gameObjectCreatorSetup, gameObjectConverterSetup);

            asset.ConnectNet<ReleaseUnityResource>(gameObjectConverterSetup, unityResourceSetup);
            asset.ConnectNet<ReleaseEntryData>(gameObjectConverterSetup, manifestSetup);
            asset.ConnectRpc<AcquireEntryDataFromModelData>(gameObjectConverterSetup, manifestSetup);
            asset.ConnectRpc<AcquireUnityResource>(gameObjectConverterSetup, unityResourceSetup);
            asset.ConnectRpc<BuildGameObject>(gameObjectConverterSetup, gameObjectBuilderSetup);

            asset.ConnectRpc<AcquireResource>(unityResourceSetup, modelResourceSetup);
            asset.ConnectNet<ReleaseResource>(unityResourceSetup, modelResourceSetup);
            asset.ConnectRpc<ConvertResource<SyncMesh>>(unityResourceSetup, meshConverterSetup);
            asset.ConnectRpc<ConvertResource<SyncMaterial>>(unityResourceSetup, materialConverterSetup);
            asset.ConnectRpc<ConvertResource<SyncTexture>>(unityResourceSetup, textureConverterSetup);

            asset.ConnectRpc<GetSyncModel>(modelResourceSetup, dataProviderSetup);

            asset.ConnectRpc<DelegateJob>(meshConverterSetup, jobSetup);

            asset.ConnectNet<ReleaseUnityResource>(materialConverterSetup, unityResourceSetup);
            asset.ConnectNet<ReleaseEntryData>(materialConverterSetup, manifestSetup);
            asset.ConnectRpc<AcquireUnityResource>(materialConverterSetup, unityResourceSetup);
            asset.ConnectRpc<AcquireEntryDataFromModelData>(materialConverterSetup, manifestSetup);

            return asset;
        }
    }
}
