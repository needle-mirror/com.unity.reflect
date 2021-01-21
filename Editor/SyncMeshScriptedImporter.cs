using System;
using System.IO;
using Unity.Reflect;
using Unity.Reflect.Model;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;
using Unity.Reflect.IO;

namespace UnityEditor.Reflect
{   
    [ScriptedImporter(1, "SyncMesh")]
    public class SyncMeshScriptedImporter : ScriptedImporter
    {
        [SerializeField]
        bool m_GenerateLightmapUVs = false;
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var syncMesh = PlayerFile.Load<SyncMesh>(ctx.assetPath);
            var syncedData = new SyncedData<SyncMesh>(StreamKey.FromSyncId<SyncMesh>(ReflectScriptedImporter.EditorSourceId, syncMesh.Id), syncMesh);
            
            var meshImporter = new SyncMeshImporter();
            var mesh = meshImporter.Import(syncedData, null);

            if (m_GenerateLightmapUVs)
            {
                Unwrapping.GenerateSecondaryUVSet(mesh);
            }
            
            mesh.name = Path.GetFileNameWithoutExtension(syncMesh.Name);
            
            ctx.AddObjectToAsset("mesh", mesh);
            
            var root = ScriptableObject.CreateInstance<ReflectScriptableObject>();

            ctx.AddObjectToAsset("root", root, AssetPreview.GetMiniThumbnail(mesh));
            ctx.SetMainObject(root);
        }
    }
}
