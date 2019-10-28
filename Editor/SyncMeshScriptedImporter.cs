using System;
using System.IO;
using Unity.Reflect.Model;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;
using File = Unity.Reflect.IO.File;

namespace UnityEditor.Reflect
{   
    [ScriptedImporter(1, "SyncMesh")]
    public class SyncMeshScriptedImporter : ScriptedImporter
    {        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var syncMesh = File.Load<SyncMesh>(ctx.assetPath);
            
            var meshImporter = new SyncMeshImporter();
            var mesh = meshImporter.Import(syncMesh, null);
            
            mesh.name = Path.GetFileNameWithoutExtension(syncMesh.Name);
            
            ctx.AddObjectToAsset("mesh", mesh);
            
            var root = ScriptableObject.CreateInstance<ReflectScriptableObject>();

            ctx.AddObjectToAsset("root", root, AssetPreview.GetMiniThumbnail(mesh));
            ctx.SetMainObject(root);
        }
    }
}
