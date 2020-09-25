using System;
using UnityEngine;
using UnityEngine.Reflect;
using UnityEngine.Reflect.Pipeline;
using UnityEngine.Reflect.Pipeline.Samples;

namespace Unity.Reflect.Samples
{
    public class AddingCollidersSample : MonoBehaviour
    {
        void Start()
        {
            // Create a pipeline asset

            var pipelineAsset = ScriptableObject.CreateInstance<PipelineAsset>();

            // Create the nodes required for this sample

            var addColliderNode = pipelineAsset.CreateNode<AddColliderNode>();

            // Create the rest of the pipeline

            var projectStreamer = pipelineAsset.CreateNode<ProjectStreamerNode>();
            var instanceProvider = pipelineAsset.CreateNode<SyncObjectInstanceProviderNode>();
            var dataProvider = pipelineAsset.CreateNode<DataProviderNode>();
            var meshConverter = pipelineAsset.CreateNode<MeshConverterNode>();
            var materialConverter = pipelineAsset.CreateNode<MaterialConverterNode>();
            var textureConverter = pipelineAsset.CreateNode<TextureConverterNode>();
            var instanceConverter = pipelineAsset.CreateNode<InstanceConverterNode>();

            // Inputs / Outputs

            pipelineAsset.CreateConnection(projectStreamer.assetOutput, instanceProvider.input);
            pipelineAsset.CreateConnection(instanceProvider.output, dataProvider.instanceInput);
            pipelineAsset.CreateConnection(dataProvider.syncMeshOutput, meshConverter.input);
            pipelineAsset.CreateConnection(dataProvider.syncMaterialOutput, materialConverter.input);
            pipelineAsset.CreateConnection(dataProvider.syncTextureOutput, textureConverter.input);
            pipelineAsset.CreateConnection(dataProvider.instanceDataOutput, instanceConverter.input);
            pipelineAsset.CreateConnection(instanceConverter.output, addColliderNode.input);

            // Params

            pipelineAsset.SetParam(dataProvider.hashCacheParam, projectStreamer);
            pipelineAsset.SetParam(materialConverter.textureCacheParam, textureConverter);
            pipelineAsset.SetParam(instanceConverter.materialCacheParam, materialConverter);
            pipelineAsset.SetParam(instanceConverter.meshCacheParam, meshConverter);

            // Add a ReflectPipeline node and start the pipeline

            var reflectBehaviour = gameObject.AddComponent<ReflectPipeline>();

            reflectBehaviour.pipelineAsset = pipelineAsset;
            reflectBehaviour.InitializeAndRefreshPipeline(new SampleSyncModelProvider());
        }
    }
}
