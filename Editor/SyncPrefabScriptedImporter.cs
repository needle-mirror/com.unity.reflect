using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.Model;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;
using Unity.Reflect.IO;

namespace UnityEditor.Reflect
{   
    [ScriptedImporter(1, "SyncPrefab", importQueueOffset:3)]
    public class SyncPrefabScriptedImporter : ReflectScriptedImporter, IObjectCache
    {
        [Serializable]
        struct MaterialRemap
        {
            public string syncMaterialName;
            public Material remappedMaterial;
            
            public MaterialRemap(string syncMaterialName, Material remappedMaterial)
            {
                this.syncMaterialName = syncMaterialName;
                this.remappedMaterial = remappedMaterial;
            }
        }

        [SerializeField, HideInInspector]
        MaterialRemap[] m_MaterialRemaps;
        
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var syncPrefab = PlayerFile.Load<SyncPrefab>(ctx.assetPath);
            
            Init(syncPrefab.Name);

            var root = SyncPrefabImporter.Import(syncPrefab, this);
            
            var names = new List<string>();
            SetUniqueNameForRoots(root, ref names); // TODO Find a deterministic way to avoid name collisions.

            RemapMaterials(root);
            
            ctx.AddObjectToAsset("root", root.gameObject);
            ctx.SetMainObject(root.gameObject);
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

        public SyncObjectBinding CreateInstance(string key)
        {
            var prefab = GetReferencedAsset<GameObject>(key);
            var gameObject = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            return gameObject.AddComponent<SyncObjectBinding>();
        }
                
        void RemapMaterials(Transform root)
        {
            if (m_MaterialRemaps == null)
                m_MaterialRemaps = new MaterialRemap[] { };
            
            var remaps = m_MaterialRemaps.ToDictionary(r => r.syncMaterialName, r => r.remappedMaterial);
            
            var renderers = root.GetComponentsInChildren<Renderer>();

            var processedMaterials = new Dictionary<string, MaterialRemap>();
            
            foreach (var renderer in renderers)
            {
                var sharedMaterialCopy = renderer.sharedMaterials;
                var changed = false;
                
                for (int i = 0; i < sharedMaterialCopy.Length; ++i)
                {
                    var sharedMaterial = sharedMaterialCopy[i];

                    if (sharedMaterial == null)
                        continue;
                    
                    if (remaps.TryGetValue(sharedMaterial.name, out var material))
                    {
                        if (material != null)
                        {
                            changed = true;
                            sharedMaterialCopy[i] = material;
                        }
                    }

                    if (!processedMaterials.ContainsKey(sharedMaterial.name))
                    {
                        processedMaterials.Add(sharedMaterial.name, new MaterialRemap(sharedMaterial.name, material));
                    }
                }

                if (changed)
                {
                    renderer.sharedMaterials = sharedMaterialCopy;
                }
            }

            m_MaterialRemaps = processedMaterials.Values.ToArray();
        }
        
        // Undocumented magic method to support nested prefab dependencies. Temporary solution until we get Asset Database V2
        static string[] GatherDependenciesFromSourceFile( string assetPath )
        {
            var syncPrefab = PlayerFile.Load<SyncPrefab>(assetPath);
            var paths = new string[syncPrefab.Instances.Count];

            var assetName = SanitizeName(syncPrefab.Name);
            
            for (var i = 0; i < syncPrefab.Instances.Count; ++i)
            {
                paths[i] = GetReferencedAssetPath(assetName, assetPath, syncPrefab.Instances[i].ObjectId.Value);
            }

            return paths;
        }
    }
}
