using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Reflect.Model;
using File = Unity.Reflect.IO.File;

namespace UnityEngine.Reflect
{
    class AssetSource
    {
        List<string> m_Sources; // FixMe Only ONE source
        public AssetSource(params string[] sources)
        {
            m_Sources = sources.ToList();
        }
        
        public T LoadModel<T>(string key) where T : class, ISyncModel
        {
            var expectedPath = ExpectedPath(key);
            if (expectedPath == null)
            {
                Debug.LogError("Unable to find model using key '" + key + "'.");
                return default;
            }
            
            return File.Load<T>(expectedPath);
        }
        
        string ExpectedPath(string relativePath)
        {
            foreach (var source in m_Sources)
            {
                var path = Path.Combine(source, relativePath);
                if (System.IO.File.Exists(path))
                {
                    return path;
                }
            }

            return null;
        }
    }

    abstract class RuntimeCache<TModel, TAsset> where TModel : class, ISyncModel where TAsset : UnityEngine.Object
    {
        struct OwnerCount
        {
            public string Key;
            public int Count;

            public OwnerCount(string key)
            {
                Key = key;
                Count = 0;
            }
        }

        readonly Dictionary<string, TAsset> m_Cache = new Dictionary<string, TAsset>();

        readonly Dictionary<int, OwnerCount> m_OwnerCount = new Dictionary<int, OwnerCount>();

        readonly RuntimeImporter<TModel, TAsset> m_Importer;

        readonly AssetSource m_AssetSource;
        
        protected RuntimeCache(RuntimeImporter<TModel, TAsset> importer, AssetSource assetSource)
        {
            m_Importer = importer;
            m_AssetSource = assetSource;
        }
        
        public TAsset Get(string key)
        {
            return m_Cache[KeyFromPath(key)];
        }
        
        public void Set(string path, TAsset obj)
        {
            string key = KeyFromPath(path);
            m_Cache[key] = obj;
            m_OwnerCount[obj.GetInstanceID()] = new OwnerCount(key);
        }

        public TAsset TryGet(string key)
        {
            m_Cache.TryGetValue(KeyFromPath(key), out var value);
            
            return value;
        }

        public void OwnAsset(TAsset asset)
        {
            int id = asset.GetInstanceID();
            if (m_OwnerCount.TryGetValue(id, out var ownercount))
            {
                ++ownercount.Count;
                m_OwnerCount[id] = ownercount;
            }
        }

        public void ReleaseAsset(TAsset asset)
        {
            int id = asset.GetInstanceID();
            if (m_OwnerCount.TryGetValue(id, out var ownercount))
            {
                if (ownercount.Count == 1)
                {
                    m_OwnerCount.Remove(id);
                    m_Cache.Remove(ownercount.Key);
                    Object.Destroy(asset);
                }
                else
                {
                    --ownercount.Count;
                    m_OwnerCount[id] = ownercount;
                }
            }
        }

        protected TAsset Reimport(string key, object settings)
        {
            var model = m_AssetSource.LoadModel<TModel>(key);

            var asset = TryGet(key) ?? m_Importer.CreateNew(model);

            m_Importer.Reimport(model, asset, settings);

            return asset;
        }
        
        protected TAsset Import(string key, object settings)
        {
            var value = TryGet(key);
            if (value == null)
            {
                var model = m_AssetSource.LoadModel<TModel>(key);

                if (model == null)
                    return default;

                value = m_Importer.Import(model, settings);

                Set(key, value);
            }
            
            return value;
        }
        
        static string KeyFromPath(string path)
        {
            return path.ToLowerInvariant().Replace("\\", "/"); // TODO Improve this
        }
    }
}