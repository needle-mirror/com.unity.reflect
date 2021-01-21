using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Reflect;
using Unity.Reflect.Model;
using Unity.Reflect.IO;

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
            
            return PlayerFile.Load<T>(expectedPath);
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
}