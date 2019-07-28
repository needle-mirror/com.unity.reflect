using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Unity.Reflect.Model;
using File = Unity.Reflect.IO.File;

namespace UnityEngine.Reflect
{
    class AssetSource
    {
        List<string> m_Sources;
        public AssetSource(params string[] sources)
        {
            m_Sources = sources.ToList();
        }
        
        public T LoadModel<T>(string key) where T : IMessage, ISyncModel
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

    abstract class RuntimeCache<TModel, TAsset> where TModel : ISyncModel, IMessage where TAsset : class
    {
        readonly Dictionary<string, TAsset> m_Cache = new Dictionary<string, TAsset>();
        
        readonly RuntimeImporter<TModel, TAsset> m_Importer;

        readonly AssetSource m_AssetSource;
        
        protected RuntimeCache(RuntimeImporter<TModel, TAsset> importer, AssetSource assetSource)
        {
            m_Importer = importer;
            m_AssetSource = assetSource;
        }
        
        public TAsset Get(string key)
        {
            key = KeyFromPath(key);
            return m_Cache[key];
        }
        
        public void Set(string path, TAsset obj)
        {
            m_Cache[KeyFromPath(path)] = obj;
        }

        public TAsset TryGet(string key)
        {
            key = KeyFromPath(key);
            m_Cache.TryGetValue(key, out var value);
            
            return value;
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

            if (value != null)
                return value;
           
            var model =  m_AssetSource.LoadModel<TModel>(key);

            if (model == null)
                return default;
            
            value = m_Importer.Import(model, settings);

            Set(key, value);
            
            return value;
        }
        
        static string KeyFromPath(string path)
        {
            return path.ToLowerInvariant().Replace("\\", "/"); // TODO Improve this
        }
    }
}