using System;
using System.Collections.Generic;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEditor.Reflect
{   
    [ScriptedImporter(1, "SyncObject", importQueueOffset:2)]
    public class SyncObjectScriptedImporter : ReflectScriptedImporter, IMaterialCache, IMeshCache
    {
        [SerializeField, HideInInspector]
        bool m_ImportLights = true;
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var sceneElement = File.Load<SyncObject>(ctx.assetPath);
            
            Init(sceneElement.Name);
            
            var defaultMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Material.mat");

            var elementImporter = new SyncObjectImporter();
            var root = elementImporter.Import(sceneElement,
                new SyncObjectImportSettings { defaultMaterial = defaultMaterial, importLights = m_ImportLights, materialCache = this, meshCache = this });

            SetUniqueNames(root.transform); // TODO Find a deterministic way to avoid name collisions.
            
            ctx.AddObjectToAsset("root", root);
            ctx.SetMainObject(root);
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
        
        public Material GetMaterial(string key)
        {
            return GetReferencedAsset<Material>(key);
        }

        public Mesh GetMesh(string key)
        {
            return GetReferencedAsset<Mesh>(key);
        }
    }
}
