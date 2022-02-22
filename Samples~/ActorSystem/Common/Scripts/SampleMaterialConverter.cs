using System;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Actors.Samples
{
    [Serializable]
    public class SampleMaterialConverter : ActorFramework.IReflectMaterialConverter
    {
#pragma warning disable CS0649
        [SerializeField]
        Material m_Material;
#pragma warning restore CS0649
        
        public string Name => "Reflect Universal RP";

        public bool IsAvailable => true;

        public Material ConstructMaterial(SyncedData<SyncMaterial> syncMaterial, ITextureCache textureCache)
        {
            var material = new Material(m_Material) { enableInstancing = true };

            // Use old urp converter just to not copy/paste the code for setting up material properties
            var oldUrpConverter = new UniversalRenderPipelineMaterialConverter();
            oldUrpConverter.SetMaterialProperties(syncMaterial, material, textureCache);
            return material;
        }

        public Material DefaultMaterial => m_Material;
    }
}
