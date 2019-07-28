using System;
using System.Collections;
using Unity.Reflect.Model;
using File = Unity.Reflect.IO.File;

namespace UnityEngine.Reflect
{
    public class SyncPrefabImporter
    {        
        readonly RuntimeTextureCache m_TextureCache;
        readonly RuntimeMeshCache m_MeshCache;
        readonly RuntimeMaterialCache m_MaterialCache;
        readonly RuntimeObjectCache m_ObjectCache;
        
        Material m_DefaultMaterial = new Material(Shader.Find("Standard")) { color = Color.cyan };

        public SyncPrefabImporter(bool importLights, params string[] sources)
        {   
            var assetSource = new AssetSource(sources);
            
            m_TextureCache = new RuntimeTextureCache(assetSource);
            m_MaterialCache = new RuntimeMaterialCache(m_TextureCache, assetSource);
            m_MeshCache = new RuntimeMeshCache(assetSource);
            
            var elementSettings = new SyncElementSettings
            {
                defaultMaterial = m_DefaultMaterial,
                importLights = importLights,
                materialCache = m_MaterialCache,
                meshCache = m_MeshCache
            };
            
            m_ObjectCache = new RuntimeObjectCache(elementSettings, assetSource);
        }
        
        public SyncObjectBinding CreateInstance(Transform root, SyncObjectInstance instance)
        {
            return CreateInstance(root, instance, m_ObjectCache);
        }
        
        public void RemoveInstance(SyncObjectBinding syncObject)
        {
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
        
        public void ReimportElement(string key)
        {
            m_ObjectCache.Reimport(key);
        }
        
        public IEnumerator ImportPrefab(Transform parent, SyncPrefab syncPrefab, Action<float, string> onProgress, Action<Transform> onDone)
        {   
            yield return Import(parent, syncPrefab, m_ObjectCache, onDone, onProgress);
        }
        
        public static IEnumerator Import(Transform parent, SyncPrefab syncPrefab, IObjectCache objectCache, Action<Transform> onDone,
            Action<float, string> onProgress = null)
        {
            var name = syncPrefab.Name;
            var elementInstances = syncPrefab.Instances;
                
            var root = new GameObject(name);

            if (parent != null)
                root.transform.parent = parent;
            
            var prefabComponent = root.AddComponent<SyncPrefabBinding>();
            prefabComponent.key = syncPrefab.Key;

            var total = elementInstances.Count;

            const string kInstantiating = "Instantiating";
            
            onProgress?.Invoke(0.0f, kInstantiating);

            var lastProgressUpdate = DateTime.Now;
            const int notifyProgressMs = 1000;
            
            for (var i = 0; i < total; ++i)
            {
                var instance = elementInstances[i];
                CreateInstance(root.transform, instance, objectCache);

                var timeNow = DateTime.Now;

                if ((timeNow - lastProgressUpdate).TotalMilliseconds > notifyProgressMs)
                {
                    onProgress?.Invoke((float)i/total, $"{kInstantiating} {i} of {total}");
                    lastProgressUpdate = timeNow;
                    yield return null;
                }
            }
            
            onDone.Invoke(root.transform);
        }
        
        static SyncObjectBinding CreateInstance(Transform root, SyncObjectInstance instance, IObjectCache objectCache)
        {
            var gameObject = objectCache.CreateInstance(instance.Object);

            if (gameObject == null)
            {
                Debug.LogWarning("Unable to instantiate SyncObject '" + instance.Object + "'");
                return null;
            }

            var syncObject = gameObject.GetComponent<SyncObjectBinding>();
            
            if (syncObject == null)
                syncObject = gameObject.AddComponent<SyncObjectBinding>();
            
            syncObject.identifier = new SyncObjectBinding.Identifier(instance);
            
            gameObject.name = instance.Name;
            gameObject.transform.parent = root;
            ImportersUtils.SetTransform(gameObject.transform, instance.Transform);

            var metadata = gameObject.GetComponent<Metadata>();
            if (metadata != null && instance.Metadata != null)
            {
                foreach (var parameter in instance.Metadata)
                {
                    var parameterValue = parameter.Value;
                    metadata.parameters.dictionary[parameter.Key] = new Metadata.Parameter
                    {
                        group = parameterValue.ParameterGroup,
                        value = parameterValue.Value,
                        visible = parameterValue.Visible
                    };
                }
            }

            return syncObject;
        }
    }
}
