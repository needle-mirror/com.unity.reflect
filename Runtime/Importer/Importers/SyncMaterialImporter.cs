using System;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{   
    public class SyncMaterialImporter : RuntimeImporter<SyncMaterial, Material>
    {
        public override Material CreateNew(SyncMaterial syncMaterial)
        {
            var material = new Material(StandardShaderHelper.GetShader(syncMaterial));            
            return material;
        }

        protected override void Clear(Material material)
        {
            // Nothing
        }

        protected override void ImportInternal(SyncMaterial syncMaterial, Material material, object settings)
        {
            var textureCache = (ITextureCache)settings;
            StandardShaderHelper.ComputeMaterial(syncMaterial, material, textureCache);
        }
    }
}
