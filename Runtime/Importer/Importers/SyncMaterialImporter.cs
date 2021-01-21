using System;
using Unity.Reflect;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class SyncMaterialImporter : RuntimeImporter<SyncMaterial, Material>
    {
        public override Material CreateNew(SyncMaterial syncMaterial, object settings = null)
        {
            var shader = ReflectMaterialManager.GetShader(syncMaterial);
            
            var material = new Material(shader);
            material.enableInstancing = true;
            return material;
        }

        protected override void Clear(Material material)
        {
            // Nothing
        }

        protected override void ImportInternal(SyncedData<SyncMaterial> syncMaterial, Material material, object settings)
        {
            ReflectMaterialManager.ComputeMaterial(syncMaterial, material, settings as ITextureCache);
        }
    }
}
