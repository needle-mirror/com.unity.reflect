﻿using System;
using System.Collections.Generic;
 using System.IO;
 using Unity.Reflect;
 using Unity.Reflect.Data;
 using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class SyncPrefabImporter
    {
        public static Transform Import(SyncPrefab syncPrefab, IObjectCache objectCache)
        {
            var root = CreateSyncPrefab(null, syncPrefab.Name);

            foreach (var instance in syncPrefab.Instances)
            {
                CreateInstance(root.transform, syncPrefab.Name, instance, objectCache);
            }

            return root;
        }
        
        static Transform CreateSyncPrefab(Transform parent, string source)
        {                
            var root = new GameObject(source);

            if (parent != null)
                root.transform.parent = parent;
            
            var prefabComponent = root.AddComponent<SyncPrefabBinding>();
            prefabComponent.sourceId = source;

            return root.transform;
        }

        public static SyncObjectBinding CreateInstance(Transform root, string source, SyncObjectInstance instance, IObjectCache objectCache)
        {
            var objectKey = new StreamKey(source, PersistentKey.GetKey<SyncObject>(instance.ObjectId));
            var syncObject = objectCache.CreateInstance(objectKey);

            if (syncObject == null)
            {
                Debug.LogWarning("Unable to instantiate SyncObject '" + instance.ObjectId + "'");
                return null;
            }

            syncObject.streamKey = new StreamKey(source, PersistentKey.GetKey<SyncObjectInstance>(instance.Id));
            
            var gameObject = syncObject.gameObject;
            
            gameObject.name = instance.Name;
            gameObject.transform.parent = root;
            ImportersUtils.SetTransform(gameObject.transform, instance.Transform);
            ImportersUtils.SetMetadata(gameObject, instance.Metadata);

            return syncObject;
        }
    }
}
