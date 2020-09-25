using System;
using UnityEngine;
using UnityEngine.Reflect.Pipeline;

namespace Unity.Reflect.Samples
{
    public class BasicPipelineSample : MonoBehaviour
    {
        public Transform root;
        
        void Start()
        {
            // Create a ReflectPipeline
            var reflectBehaviour = gameObject.AddComponent<ReflectPipeline>();
            
            // Create a PipelineAsset 
            var pipelineAsset = ScriptableObject.CreateInstance<PipelineAsset>();
            
            // Nodes
            
            var projectStreamer = pipelineAsset.CreateNode<ProjectStreamerNode>();
            var instanceProvider = pipelineAsset.CreateNode<SyncObjectInstanceProviderNode>();
            var dataProvider = pipelineAsset.CreateNode<DataProviderNode>();
            var meshConverter = pipelineAsset.CreateNode<MeshConverterNode>();
            var materialConverter = pipelineAsset.CreateNode<MaterialConverterNode>();
            var textureConverter = pipelineAsset.CreateNode<TextureConverterNode>();
            var instanceConverter = pipelineAsset.CreateNode<InstanceConverterNode>();
            
            // Assign root transform to InstanceConverterNode
            instanceConverter.SetRoot(root, reflectBehaviour);
            
            // Inputs / Outputs

            pipelineAsset.CreateConnection(projectStreamer.assetOutput, instanceProvider.input);
            pipelineAsset.CreateConnection(instanceProvider.output, dataProvider.instanceInput);
            pipelineAsset.CreateConnection(dataProvider.syncMeshOutput, meshConverter.input);
            pipelineAsset.CreateConnection(dataProvider.syncMaterialOutput, materialConverter.input);
            pipelineAsset.CreateConnection(dataProvider.syncTextureOutput, textureConverter.input);
            pipelineAsset.CreateConnection(dataProvider.instanceDataOutput, instanceConverter.input);

            // Params

            pipelineAsset.SetParam(dataProvider.hashCacheParam, projectStreamer);
            pipelineAsset.SetParam(materialConverter.textureCacheParam, textureConverter);
            pipelineAsset.SetParam(instanceConverter.materialCacheParam, materialConverter);
            pipelineAsset.SetParam(instanceConverter.meshCacheParam, meshConverter);

            reflectBehaviour.pipelineAsset = pipelineAsset;
            reflectBehaviour.InitializeAndRefreshPipeline(new SampleSyncModelProvider());
        }
    }
}
