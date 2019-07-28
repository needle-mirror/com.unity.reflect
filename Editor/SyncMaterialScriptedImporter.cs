using System;
using System.IO;
using Unity.Reflect.Model;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;
using File = Unity.Reflect.IO.File;

namespace UnityEditor.Reflect
{
    [ScriptedImporter(1, "SyncMaterial", importQueueOffset:1)]
    public class SyncMaterialScriptedImporter : ReflectScriptedImporter, ITextureCache
    {       
        public virtual Texture2D GetTexture(string key)
        {           
            return GetReferencedAsset<Texture2D>(key);
        }
        
        public override void OnImportAsset(AssetImportContext ctx)
        {           
            var syncMaterial = File.Load<SyncMaterial>(ctx.assetPath);
            
            Init(syncMaterial.Name);

            var materialImporter = new SyncMaterialImporter();
            var material = materialImporter.Import(syncMaterial, this);
            
            if (syncMaterial.Name != null)
            {
                material.name = Path.GetFileNameWithoutExtension(syncMaterial.Name);
            }
            
            ctx.AddObjectToAsset("material", material);
            
            var root = ScriptableObject.CreateInstance<ReflectScriptableObject>();

            ctx.AddObjectToAsset("root", root, AssetPreview.GetMiniThumbnail(material));
            ctx.SetMainObject(root);
        }
    }
}
