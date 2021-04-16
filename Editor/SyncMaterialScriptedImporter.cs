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
    [ScriptedImporter(1, "SyncMaterial", importQueueOffset:1)]
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

            var syncedData = new SyncedData<SyncMaterial>(StreamKey.FromSyncId<SyncMaterial>(EditorSourceId, syncMaterial.Id), syncMaterial);
            
            var materialImporter = new SyncMaterialImporter();
            var material = materialImporter.Import(syncedData, this);
            
            material.name = Path.GetFileNameWithoutExtension(syncMaterial.Name);

            ctx.AddObjectToAsset("material", material);
            
            var root = ScriptableObject.CreateInstance<ReflectScriptableObject>();

            ctx.AddObjectToAsset("root", root, AssetPreview.GetMiniThumbnail(material));
            ctx.SetMainObject(root);
        }
    }
}
