﻿using System;
using System.Collections.Generic;
 using System.IO;
 using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public class SyncPrefabImporter
    {
        readonly RuntimeTextureCache m_TextureCache;
        readonly RuntimeMeshCache m_MeshCache;
        readonly RuntimeMaterialCache m_MaterialCache;
        readonly RuntimeObjectCache m_ObjectCache;

        public SyncPrefabImporter(bool importLights, params string[] sources)
        {
            var assetSource = new AssetSource(sources);

            m_TextureCache = new RuntimeTextureCache(assetSource);
            m_MaterialCache = new RuntimeMaterialCache(m_TextureCache, assetSource);
            m_MeshCache = new RuntimeMeshCache(assetSource);
            
            var elementSettings = new SyncObjectImportConfig
            {
                settings = new SyncObjectImportSettings { defaultMaterial = ReflectMaterialManager.defaultMaterial, importLights = importLights },
                materialCache = m_MaterialCache,
                meshCache = m_MeshCache
            };
            
            m_ObjectCache = new RuntimeObjectCache(elementSettings, assetSource);
        }
        
        public SyncObjectBinding CreateInstance(Transform root, SyncObjectInstance instance)
        {
            var syncObject = CreateInstance(root, instance, m_ObjectCache);
            
            if (syncObject != null)
            {
                foreach (var filter in syncObject.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.sharedMesh != null)
                    {
                        m_MeshCache.OwnAsset(filter.sharedMesh);
                    }
                }
                
                foreach (var renderer in syncObject.GetComponentsInChildren<Renderer>())
                {
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        if (mat != null)
                        {
                            m_MaterialCache.OwnAsset(mat);

                            foreach (var name in mat.GetTexturePropertyNames())
                            {
                                var texture = mat.GetTexture(name) as Texture2D;
                                if (texture != null)
                                {
                                    m_TextureCache.OwnAsset(texture);
                                }
                            }
                        }
                    }
                }
            }

            return syncObject;
        }

        public void RemoveInstance(SyncObjectBinding syncObject)
        {
            foreach (var filter in syncObject.GetComponentsInChildren<MeshFilter>())
            {
                if (filter != null && filter.sharedMesh != null)
                {
                    m_MeshCache.ReleaseAsset(filter.sharedMesh);
                }
            }
            
            foreach (var renderer in syncObject.GetComponentsInChildren<Renderer>())
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    foreach (var name in mat.GetTexturePropertyNames())
                    {
                        var texture = mat.GetTexture(name) as Texture2D;
                        if (texture != null)
                        {
                            m_TextureCache.ReleaseAsset(texture);
                        }
                    }
                        
                    m_MaterialCache.ReleaseAsset(mat);
                }
            }

            m_ObjectCache.RemoveInstance(syncObject);
        }
        
        public void ReimportMesh(string key)
        {
            m_MeshCache.Reimport(key);
        }
        
        public void ReimportMaterial(string key)
        {
            m_MaterialCache.Reimport(key);
        }
        
        public List<GameObject> ReimportElement(string key)
        {
            return m_ObjectCache.Reimport(key);
        }

        public static Transform Import(SyncPrefab syncPrefab, IObjectCache objectCache)
        {
            var root = CreateSyncPrefab(null, syncPrefab);

            foreach (var instance in syncPrefab.Instances)
            {
                CreateInstance(root.transform, instance, objectCache);
            }

            return root;
        }
        
        public static Transform CreateSyncPrefab(Transform parent, SyncPrefab syncPrefab)
        {                
            var root = new GameObject(syncPrefab.Name);

            if (parent != null)
                root.transform.parent = parent;
            
            var prefabComponent = root.AddComponent<SyncPrefabBinding>();
            prefabComponent.key = syncPrefab.Id.Value;

            return root.transform;
        }

        public static SyncObjectBinding CreateInstance(Transform root, SyncObjectInstance instance, IObjectCache objectCache)
        {
            var syncObject = objectCache.CreateInstance(instance.ObjectId.Value);

            if (syncObject == null)
            {
                Debug.LogWarning("Unable to instantiate SyncObject '" + instance.ObjectId + "'");
                return null;
            }

            syncObject.identifier = new SyncObjectBinding.Identifier(instance);

            var gameObject = syncObject.gameObject;
            
            gameObject.name = instance.Name;
            gameObject.transform.parent = root;
            ImportersUtils.SetTransform(gameObject.transform, instance.Transform);
            ImportersUtils.SetMetadata(gameObject, instance.Metadata);

            return syncObject;
        }
    }
}
