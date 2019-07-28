using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;
using File = Unity.Reflect.IO.File;
using Unity.Reflect.IO;

namespace UnityEngine.Reflect.Services
{
    public class SyncInstance
    {
        readonly string m_SyncPath;
        Transform m_SyncRoot;
        
        Transform m_SyncInstanceRoot;
        
        SyncManifest m_Manifest;

        public SyncManifest Manifest => m_Manifest;
        
        Dictionary<SyncObjectBinding.Identifier, SyncObjectBinding> m_ElementInstances;

        SyncPrefabImporter m_SyncPrefabImporter;

        LocalStorage m_Storage;

        public SyncInstance(Transform syncRoot, string syncPath)
        {
            m_SyncPath = syncPath;
            m_SyncRoot = syncRoot;
            
            m_SyncInstanceRoot = null;

            m_ElementInstances = new Dictionary<SyncObjectBinding.Identifier, SyncObjectBinding>();
            
            m_Storage = new LocalStorage(m_SyncPath);
            m_Manifest = m_Storage.OpenOrCreateManifest();
        }

        void BuildCache()
        {
            if (m_SyncInstanceRoot == null)
                return;

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

        public IEnumerator ApplyModifications(SyncManifest manifest, Action<float, string> onProgress)
        {
            // Otherwise, create a new instance of it.
            if (m_SyncInstanceRoot == null)
            {
                // Try to instantiate a new one
                var prefabPath = GetPrefabPath(m_SyncPath);

                if (prefabPath != null)
                {
                    m_SyncPrefabImporter = new SyncPrefabImporter(true, m_SyncPath);
                    yield return m_SyncPrefabImporter.ImportPrefab(m_SyncRoot, OpenSyncPrefab(prefabPath), onProgress ,root =>
                    {
                        m_SyncInstanceRoot = root;
                    });
                }

                if (m_SyncInstanceRoot != null)
                {
                    BuildCache();
                }
            }

            if (m_SyncInstanceRoot == null) // Nothing to Sync with yet.
                yield break;

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

                        if (exportData.DstHash != newExportData.DstHash)
                        {
                            Debug.Log("Modified : " + key + " (" + exportData.DstPath + ")");

                            if (exportData.DstPath.EndsWith(SyncMaterial.Extension))
                            {
                                m_SyncPrefabImporter.ReimportMaterial(exportData.DstPath);
                            }

                            if (exportData.DstPath.EndsWith(SyncMesh.Extension))
                            {
                                m_SyncPrefabImporter.ReimportMesh(exportData.DstPath);
                            }
                            
                            if (exportData.DstPath.EndsWith(SyncObject.Extension))
                            {
                                m_SyncPrefabImporter.ReimportElement(exportData.DstPath);
                            }

                            if (exportData.DstPath.EndsWith(SyncPrefab.Extension))
                            {
                                // If Prefab changed that means that:
                                // - An element has been moved
                                // - An element has been added
                                // - An element has been removed

                                var path = Path.Combine(m_SyncPath, exportData.DstPath);
                                var syncPrefab = OpenSyncPrefab(path);

                                var instances = new HashSet<SyncObjectBinding.Identifier>();

                                foreach (var instance in syncPrefab.Instances) // TODO Narrow down which instance has changed?
                                {
                                    var identifier = new SyncObjectBinding.Identifier(instance);

                                    instances.Add(identifier);

                                    if (!m_ElementInstances.TryGetValue(identifier, out var syncObject))
                                    {
                                        Debug.Log("Adding Element Instance : " + identifier);
                                        syncObject = m_SyncPrefabImporter.CreateInstance(m_SyncInstanceRoot, instance);
                                    }

                                    if (syncObject != null)
                                    {
                                        m_ElementInstances[syncObject.identifier] = syncObject;

                                        // Hack. Put the instance at its positions even if it didn't change. TODO Optimize
                                        ImportersUtils.SetTransform(syncObject.transform, instance.Transform);
                                    }
                                }

                                // Remove any non referenced elements
                                var identifiers = m_ElementInstances.Keys.ToArray();
                                foreach (var identifier in identifiers)
                                {
                                    if (!instances.Contains(identifier))
                                    {
                                        Debug.Log("Removed Element : " + identifier);

                                        m_SyncPrefabImporter.RemoveInstance(m_ElementInstances[identifier]);
                                        m_ElementInstances.Remove(identifier);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            m_Manifest = manifest;
        }

        static SyncPrefab OpenSyncPrefab(string path)
        {
            var syncPrefab = File.Load<SyncPrefab>(path);
            // TODO Invoke any SyncPrefab event
            return syncPrefab;
        }
        
        static string GetPrefabPath(string rootFolder)
        {
            return Directory.EnumerateFiles(rootFolder, $"*{SyncPrefab.Extension}", SearchOption.AllDirectories).FirstOrDefault();
        }
    }
}
