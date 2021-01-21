using System;
using System.Collections.Generic;
using Unity.Reflect;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;
using UnityEngine.Rendering;

namespace UnityEditor.Reflect
{   
    [ScriptedImporter(1, "SyncObject", importQueueOffset:2)]
    public class SyncObjectScriptedImporter : ReflectScriptedImporter, IMaterialCache, IMeshCache
    {
        [SerializeField, HideInInspector]
        bool m_ImportLights = true;
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var sceneElement = PlayerFile.Load<SyncObject>(ctx.assetPath);
            
            Init(sceneElement.Name);

            var defaultMaterial = ReflectMaterialManager.defaultMaterial;
            
            var syncedData = new SyncedData<SyncObject>(StreamKey.FromSyncId<SyncObject>(EditorSourceId, sceneElement.Id), sceneElement);
            
            var elementImporter = new SyncObjectImporter();
            var root = elementImporter.Import(syncedData,
                new SyncObjectImportConfig
                {
                    settings = new SyncObjectImportSettings { defaultMaterial = defaultMaterial, importLights = m_ImportLights }, materialCache = this, meshCache = this
                });

            SetUniqueNames(root.transform); // TODO Find a deterministic way to avoid name collisions.
            
            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
        }
        
        static bool IsUsingRenderPipeline()
        {
            return GraphicsSettings.renderPipelineAsset != null;
        }
        
        static void SetUniqueNames(Transform root)
        {
            if (root.childCount == 0)
                return;
            
            var names = new List<string>();
            
            foreach (Transform child in root)
            {
                var newName = ObjectNames.GetUniqueName(names.ToArray(), child.name);
                child.name = newName;
                names.Add(newName);
                
                SetUniqueNames(child);
            }
        }
        
        public Material GetMaterial(StreamKey id)
        {
            return GetReferencedAsset<Material>(id.key.Name);
        }

        public Mesh GetMesh(StreamKey id)
        {
            return GetReferencedAsset<Mesh>(id.key.Name);
        }
    }
}
