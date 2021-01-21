using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;
using Unity.Reflect.IO;
using UnityEngine.Reflect;

namespace UnityEditor.Reflect
{
    public class SyncInstance
    {
        internal static SyncPrefab GenerateSyncPrefabFromManifest(string name, string rootFolder, SyncManifest manifest)
        {
            var prefab = new SyncPrefab { Name = name };

            var content = manifest.Content;
            foreach (var pair in content)
            {
                if (pair.Key.IsKeyFor<SyncObjectInstance>())
                {
                    // Load SynObjectInstance from disk
                    var instancePath = Path.Combine(rootFolder, pair.Value.ModelPath);
                    var objectInstance = PlayerFile.Load<SyncObjectInstance>(instancePath);
                    objectInstance.Name = Path.GetFileNameWithoutExtension(objectInstance.Name);
                    prefab.Instances.Add(objectInstance);
                }
            }

            return prefab;
        }

        internal static string GetPrefabPath(string rootFolder)
        {
            return Directory.EnumerateFiles(rootFolder, $"*{SyncPrefab.Extension}", SearchOption.AllDirectories).FirstOrDefault();
        }
    }
}
