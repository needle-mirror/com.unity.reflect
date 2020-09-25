using System;
using UnityEngine;
using UnityEngine.Reflect.Pipeline;

namespace Unity.Reflect.Samples
{
    public class ShaderReplacementSample : MonoBehaviour
    {
        public Shader opaqueShader;
        public Shader transparentShader;

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
            var textureConverter = pipelineAsset.CreateNode<TextureConverterNode>();
            var instanceConverter = pipelineAsset.CreateNode<InstanceConverterNode>();
            
            // Create the custom material converter
            var customMaterialConverter = pipelineAsset.CreateNode<SampleMaterialConverterNode>();
            customMaterialConverter.opaqueShader = opaqueShader;
            customMaterialConverter.transparentShader = transparentShader;

            // Inputs / Outputs

            pipelineAsset.CreateConnection(projectStreamer.assetOutput, instanceProvider.input);
            pipelineAsset.CreateConnection(instanceProvider.output, dataProvider.instanceInput);
            pipelineAsset.CreateConnection(dataProvider.syncMeshOutput, meshConverter.input);
            pipelineAsset.CreateConnection(dataProvider.syncMaterialOutput, customMaterialConverter.input);
            pipelineAsset.CreateConnection(dataProvider.syncTextureOutput, textureConverter.input);
            pipelineAsset.CreateConnection(dataProvider.instanceDataOutput, instanceConverter.input);

            // Params

            pipelineAsset.SetParam(dataProvider.hashCacheParam, projectStreamer);
            pipelineAsset.SetParam(customMaterialConverter.textureCacheParam, textureConverter);
            pipelineAsset.SetParam(instanceConverter.materialCacheParam, customMaterialConverter);
            pipelineAsset.SetParam(instanceConverter.meshCacheParam, meshConverter);

            reflectBehaviour.pipelineAsset = pipelineAsset;
            reflectBehaviour.InitializeAndRefreshPipeline(new SampleSyncModelProvider());
        }
    }
}
