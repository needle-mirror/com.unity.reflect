using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public interface IObjectCache
    {
        SyncObjectBinding CreateInstance(string key);
    }
    
    class RuntimeObjectCache : IObjectCache
    {
        SyncObjectImportSettings m_Settings;

        Dictionary<string, List<GameObject>> m_Instances;
        readonly SyncObjectImporter m_SyncObjectImporter = new SyncObjectImporter();
        readonly AssetSource m_AssetSource;
        
        public RuntimeObjectCache(SyncObjectImportSettings settings, AssetSource assetSource)
        {
            m_Settings = settings;
            m_AssetSource = assetSource;
            m_Instances = new Dictionary<string, List<GameObject>>();
        }

        public List<GameObject> Reimport(string key)
        {
			List<GameObject> list;
			m_Instances.TryGetValue(key, out list);

            if (list?.Count > 0)
            {
                var model = m_AssetSource.LoadModel<SyncObject>(key);
                foreach (var gameObject in list)
                {
                    m_SyncObjectImporter.Reimport(model, gameObject, m_Settings);    
                }
            }
			return list;
        }

        public SyncObjectBinding CreateInstance(string key)
        {
            var model = m_AssetSource.LoadModel<SyncObject>(key);

            if (model == null)
                return null;
            
            var gameObject = m_SyncObjectImporter.Import(model, m_Settings);

            if (!m_Instances.TryGetValue(key, out var list))
            {
                m_Instances[key] = list = new List<GameObject>();
            }
            
            list.Add(gameObject);

            var syncObject = gameObject.GetComponent<SyncObjectBinding>();
            
            if (syncObject == null)
                syncObject = gameObject.AddComponent<SyncObjectBinding>();
            
            return syncObject;
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