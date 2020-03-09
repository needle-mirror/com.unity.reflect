using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;
using Unity.Reflect.IO;

namespace UnityEngine.Reflect
{
    public class SyncInstance
    {
        public delegate void InstanceEventHandler(SyncInstance instance, SyncPrefab prefab);
        public event InstanceEventHandler onPrefabLoaded;
        public event InstanceEventHandler onPrefabChanged;

        public delegate void ObjectEventHandler(SyncObjectBinding obj);
        public event ObjectEventHandler onObjectCreated;
        public event ObjectEventHandler onObjectDestroyed;
        public event ObjectEventHandler onObjectChanged;

		readonly string m_SyncPath;
        Transform m_SyncRoot;
        
        Transform m_SyncInstanceRoot;
        
        SyncManifest m_Manifest;

        public SyncManifest Manifest => m_Manifest;
        
        Dictionary<SyncObjectBinding.Identifier, SyncObjectBinding> m_ElementInstances;

        SyncPrefabImporter m_SyncPrefabImporter;

        PlayerStorage m_Storage;

        ISet<SyncObjectBinding.Identifier> m_VisibilityFilter;

        SyncPrefab m_SyncPrefab;

        public SyncInstance(Transform syncRoot, string syncPath)
        {
            m_SyncPath = syncPath;
            m_SyncRoot = syncRoot;
            
            m_SyncInstanceRoot = null;

            m_ElementInstances = new Dictionary<SyncObjectBinding.Identifier, SyncObjectBinding>();

            m_Storage = new PlayerStorage(m_SyncPath, true, false);
            m_Manifest = m_Storage.OpenOrCreateManifest();
        }

        void BuildCache()
        {
            m_ElementInstances.Clear();

            var syncObjects = m_SyncInstanceRoot.GetComponentsInChildren<SyncObjectBinding>();
            foreach (var syncObject in syncObjects)
            {
                if (m_ElementInstances.ContainsKey(syncObject.identifier))
                {
                    Debug.LogError("Duplicate SyncObjectInstance Identifier : " + syncObject.identifier);
                    continue;
                }
                
                m_ElementInstances.Add(syncObject.identifier, syncObject);
            }
        }

        public SyncPrefab GetPrefab()
        {
            return m_SyncPrefab;
        }

        public void SetVisibilityFilter(ISet<SyncObjectBinding.Identifier> filter)
        {
            m_VisibilityFilter = filter;
        }

        public void RemoveVisibilityFilter(ISet<SyncObjectBinding.Identifier> filter)
        {
            if (m_VisibilityFilter == filter)
            {
                m_VisibilityFilter = null;
            }
        }

        public int GetInstanceCount()
        {
            return m_ElementInstances.Count();
        }
        
        public bool ApplyModifications(SyncManifest manifest)
        {
            bool hasChanged = false;

            // Otherwise, create a new instance of it.
            if (m_SyncInstanceRoot == null)
            {
                // Try to instantiate a new one
                var prefabPath = GetPrefabPath(m_SyncPath);

                if (prefabPath == null)
                    throw new Exception("Unable to load SyncPrefab. Data might be corrupted. Please clean local data and retry.");
                
                m_SyncPrefabImporter = new SyncPrefabImporter(true, m_SyncPath);
                m_SyncPrefab = OpenSyncPrefab(prefabPath);
                m_SyncInstanceRoot = SyncPrefabImporter.CreateSyncPrefab(m_SyncRoot, m_SyncPrefab);
                
                if (m_SyncInstanceRoot == null)
                    throw new Exception("Unable to create a SyncInstance. Data might be corrupted or from a unsupported version. Please clean local data and retry.");

                BuildCache();
                hasChanged = true;
            }

            if (m_Manifest != null)
            {
                // Try to find what has changed...
                var newEntries = manifest.Content;
                var currentEntries = m_Manifest.Content;

                foreach (var entry in newEntries)
                {
                    var key = entry.Key;

                    if (currentEntries.ContainsKey(key))
                    {
                        var exportData = currentEntries[key];
                        var newExportData = entry.Value;

                        if (exportData.Hash != newExportData.Hash)
                        {
                            hasChanged = true;
                            Debug.Log("Modified : " + key + " (" + exportData.ModelPath + ")");

                            if (exportData.ModelPath.EndsWith(SyncMaterial.Extension))
                            {
                                m_SyncPrefabImporter.ReimportMaterial(exportData.ModelPath);
                            }

                            if (exportData.ModelPath.EndsWith(SyncMesh.Extension))
                            {
                                m_SyncPrefabImporter.ReimportMesh(exportData.ModelPath);
                            }

                            if (exportData.ModelPath.EndsWith(SyncObject.Extension))
                            {
                                var objects = m_SyncPrefabImporter.ReimportElement(exportData.ModelPath);
                                if (objects != null)
                                {
                                    foreach (var obj in objects)
                                    {
                                        var binding = obj.GetComponent<SyncObjectBinding>();
                                        if (binding != null)
                                        {
                                            onObjectChanged?.Invoke(binding);
                                        }
                                    }
                                }
							}

                            if (exportData.ModelPath.EndsWith(SyncPrefab.Extension))
                            {
                                // If Prefab changed that means that:
                                // - An element has been moved
                                // - An element has been added
                                // - An element has been removed
                                var prefabPath = GetPrefabPath(m_SyncPath);
                                m_SyncPrefab = OpenSyncPrefab(prefabPath);
                                onPrefabChanged?.Invoke(this, m_SyncPrefab);
                            }
                        }
                    }
                }
            }

            m_Manifest = manifest;

            return hasChanged;
        }

        public IEnumerator ApplyPrefabChanges()
		{
            //  breathe after sorting scores
            yield return null;

            var instances = new HashSet<SyncObjectBinding.Identifier>();

            int counter = 0;

			foreach (var instance in m_SyncPrefab.Instances) // TODO Narrow down which instance has changed?
			{
				var identifier = new SyncObjectBinding.Identifier(instance);

                if ((m_VisibilityFilter == null) || m_VisibilityFilter.Contains(identifier))
                {
                    instances.Add(identifier);

                    if (!m_ElementInstances.TryGetValue(identifier, out var syncObject))
                    {
                        //Debug.Log("Adding Element Instance : " + identifier);
                        syncObject = m_SyncPrefabImporter.CreateInstance(m_SyncInstanceRoot, instance);
                        if (syncObject != null)
                        {
                            onObjectCreated?.Invoke(syncObject);
                            if (++counter % 20 == 0)
                            {
                                yield return null;
                            }
                        }
                    }

                    if (syncObject != null)
                    {
                        m_ElementInstances[syncObject.identifier] = syncObject;

                        // Hack. Put the instance at its positions even if it didn't change. TODO Optimize
                        ImportersUtils.SetTransform(syncObject.transform, instance.Transform);
                    }
                }
            }

			// Remove any non referenced elements
            var keys = m_ElementInstances.Keys.ToArray();
            foreach (var key in keys)
            {
                if (!instances.Contains(key))
                {
                    //Debug.Log("Removed Element : " + identifier);
                    var obj = m_ElementInstances[key];
                    onObjectDestroyed?.Invoke(obj);
                    m_SyncPrefabImporter.RemoveInstance(obj);
                    m_ElementInstances.Remove(key);
                }
            }
 		}

        SyncPrefab OpenSyncPrefab(string path)
        {
            var syncPrefab = PlayerFile.Load<SyncPrefab>(path);
            onPrefabLoaded?.Invoke(this, syncPrefab);
            return syncPrefab;
        }

        static string GetPrefabPath(string rootFolder)
        {
            return Directory.EnumerateFiles(rootFolder, $"*{SyncPrefab.Extension}", SearchOption.AllDirectories).FirstOrDefault();
        }
    }
}
