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
    [ScriptedImporter(1, "SyncTexture")]
    public class SyncTextureScriptedImporter : ScriptedImporter
    {        
        public override void OnImportAsset(AssetImportContext ctx)
        {           
            var syncTexture = PlayerFile.Load<SyncTexture>(ctx.assetPath);

            var texture = Import(syncTexture, new SyncTextureImporter());
            
            ctx.AddObjectToAsset("texture", texture);
            
            var root = ScriptableObject.CreateInstance<ReflectScriptableObject>();

            ctx.AddObjectToAsset("root", root, AssetPreview.GetMiniThumbnail(texture));
            ctx.SetMainObject(root);
        }
        
        public static Texture2D Import(SyncTexture syncTexture, SyncTextureImporter importer)
        {
            var syncedData = new SyncedData<SyncTexture>(StreamKey.FromSyncId<SyncTexture>(ReflectScriptedImporter.EditorSourceId, syncTexture.Id), syncTexture);
            
            var texture = importer.Import(syncedData, null);
            
            texture.name = Path.GetFileNameWithoutExtension(syncTexture.Name);

            return texture;
        }
    }
}
