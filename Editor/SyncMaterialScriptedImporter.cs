using System;
using System.IO;
using Unity.Reflect;
using Unity.Reflect.Model;
using UnityEditor.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;
using Unity.Reflect.IO;

namespace UnityEditor.Reflect
{
    [ScriptedImporter(2, "SyncMaterial", importQueueOffset:1)]
    public class SyncMaterialScriptedImporter : ReflectScriptedImporter, ITextureCache
    {
        public virtual Texture2D GetTexture(StreamKey key)
        {
            return GetReferencedAsset<Texture2D>(key.key.Name);
        }
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var syncMaterial = PlayerFile.Load<SyncMaterial>(ctx.assetPath);

            Init(syncMaterial.Name);
            
            var material = Import(syncMaterial, this, new SyncMaterialImporter());

            ctx.AddObjectToAsset("material", material);
            
            var root = ScriptableObject.CreateInstance<ReflectScriptableObject>();

            ctx.AddObjectToAsset("root", root, AssetPreview.GetMiniThumbnail(material));
            ctx.SetMainObject(root);
        }

        public static Material Import(SyncMaterial syncMaterial, ITextureCache textureCache, SyncMaterialImporter importer)
        {
            var syncedData = new SyncedData<SyncMaterial>(StreamKey.FromSyncId<SyncMaterial>(EditorSourceId, syncMaterial.Id), syncMaterial);
            
            var material = importer.Import(syncedData, textureCache);
            
            material.name = Path.GetFileNameWithoutExtension(syncMaterial.Name);

            return material;
        }
    }
}
