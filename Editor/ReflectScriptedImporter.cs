using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityEditor.Reflect
{
    public abstract class ReflectScriptedImporter : ScriptedImporter
    {        
        string m_AssetName;
        Dictionary<SourceAssetIdentifier, Object> m_Remaps;

        protected T GetReferencedAsset<T>(string path) where T : Object
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var k = new SourceAssetIdentifier(typeof(T), path);

            if (m_Remaps.TryGetValue(k, out var obj))
            {
                return (T) obj;
            }

            var refAssetPath = GetReferencedAssetPath(m_AssetName, assetPath, path);

            return AssetDatabase.LoadAssetAtPath<T>(refAssetPath);
        }

        protected static string GetReferencedAssetPath(string assetName, string assetPath, string relativePath)
        {
            var assetRelativePath = string.IsNullOrEmpty(assetName)
                ? assetPath
                : assetPath.Replace(assetName, string.Empty);
                        
            var commonFolder = Path.GetDirectoryName(assetRelativePath);
            
            var refAssetPath = SanitizeName(Path.GetFullPath(Path.Combine(commonFolder, relativePath)));
            var i = refAssetPath.IndexOf("/Assets/", StringComparison.InvariantCultureIgnoreCase);
            
            if (i > 0)
                refAssetPath = refAssetPath.Substring(i + 1);

            return refAssetPath;
        }

        protected static string SanitizeName(string name)
        {
            return name.Replace("\\", "/");
        }
        
        protected void Init(string assetName)
        {           
            m_AssetName = SanitizeName(assetName);
            m_Remaps = GetExternalObjectMap();
        }
    }
}
