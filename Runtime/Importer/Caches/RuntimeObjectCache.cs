using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public interface IObjectCache
    {
        GameObject CreateInstance(string key);
    }
    
    class RuntimeObjectCache : IObjectCache
    {
        SyncElementSettings m_Settings;

        Dictionary<string, List<GameObject>> m_Instances;
        readonly SyncObjectImporter m_SyncObjectImporter = new SyncObjectImporter();
        readonly AssetSource m_AssetSource;
        
        public RuntimeObjectCache(SyncElementSettings settings, AssetSource assetSource)
        {
            m_Settings = settings;
            m_AssetSource = assetSource;
            m_Instances = new Dictionary<string, List<GameObject>>();
        }

        public void Reimport(string key)
        {
            m_Instances.TryGetValue(key, out List<GameObject> list);

            if (list?.Count > 0)
            {
                var model = m_AssetSource.LoadModel<SyncObject>(key);
                foreach (var gameObject in list)
                {
                    m_SyncObjectImporter.Reimport(model, gameObject, m_Settings);    
                }
            }
        }

        public void Add(SyncObjectBinding syncObject)
        {
            if (!m_Instances.TryGetValue(syncObject.identifier.key, out var list))
            {
                m_Instances[syncObject.identifier.key] = list = new List<GameObject>();
            }
            
            list.Add(syncObject.gameObject);
                
        }
        
        public GameObject CreateInstance(string key)
        {
            if (m_Instances.ContainsKey(key))
            {
                return Object.Instantiate(m_Instances[key].First());
            }
            
            var model = m_AssetSource.LoadModel<SyncObject>(key);

            if (model == null)
                return null;
            
            var value = m_SyncObjectImporter.Import(model, m_Settings);
            m_Instances[key] = new List<GameObject> { value };

            return value;
        }
        
        public void RemoveInstance(SyncObjectBinding syncObject)
        {
            if (m_Instances.ContainsKey(syncObject.identifier.key))
            {
                var list = m_Instances[syncObject.identifier.key];
                list.Remove(syncObject.gameObject);

                if (list.Count == 0)
                    m_Instances.Remove(syncObject.identifier.key);
            }

            Object.DestroyImmediate(syncObject.gameObject);
        }
    }
}