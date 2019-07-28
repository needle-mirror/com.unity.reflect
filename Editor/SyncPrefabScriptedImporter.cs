using System;
using System.Collections.Generic;
using System.IO;
using Unity.Reflect.Model;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;
using File = Unity.Reflect.IO.File;

namespace UnityEditor.Reflect
{   
    [ScriptedImporter(1, "SyncPrefab", importQueueOffset:3)]
    public class SyncPrefabScriptedImporter : ReflectScriptedImporter, IObjectCache
    {          
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var syncPrefab = File.Load<SyncPrefab>(ctx.assetPath);
            
            Init(syncPrefab.Name);

            var enumerator = SyncPrefabImporter.Import(null, syncPrefab, this, root =>
            {
                var names = new List<string>();
                SetUniqueNameForRoots(root, ref names); // TODO Find a deterministic way to avoid name collisions.
                
                ctx.AddObjectToAsset("root", root.gameObject);
                ctx.SetMainObject(root.gameObject);
            });

            while (enumerator.MoveNext())
            {
                // Wait for the import to finish.
            }
        }
        
        static void SetUniqueNameForRoots(Transform root, ref List<string> names)
        {
            foreach (Transform child in root)
            {
                var newName = ObjectNames.GetUniqueName(names.ToArray(), child.name);
                child.name = newName;
                names.Add(newName);
            }
        }

        public GameObject CreateInstance(string key)
        {
            var prefab = GetReferencedAsset<GameObject>(key);
            return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        }
        
        // Undocumented magic method to support nested prefab dependencies. Temporary solution until we get Asset Database V2
        static string[] GatherDependenciesFromSourceFile( string assetPath )
        {
            var syncPrefab = File.Load<SyncPrefab>(assetPath);
            var paths = new string[syncPrefab.Instances.Count];

            var assetName = SanitizeName(syncPrefab.Name);
            
            for (var i = 0; i < syncPrefab.Instances.Count; ++i)
            {
                paths[i] = GetReferencedAssetPath(assetName, assetPath, syncPrefab.Instances[i].Object);
            }

            return paths;
        }
    }
}
