using System;
using System.Collections.Generic;
using System.IO;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEditor.Reflect
{
    class ReflectEditorMaterialUpgrade
    {
        static class DialogText
        {
            public static readonly string title = "Reflect Material Upgrade";
            public static readonly string proceed = "Proceed";
            public static readonly string ok = "Ok";
            public static readonly string cancel = "Cancel";
            public static readonly string noSelectionMessage = "You must select at least one material.";
        }
        
        [MenuItem("Window/Reflect/Convert to current RenderPipeline", priority = 1100)]
        static void ConvertReflectToCurrentRenderPipeline()
        {
            var assetPaths = new List<string>();
            
            FindAssetWithExtension(SyncMaterial.Extension, assetPaths);
            FindAssetWithExtension(SyncObject.Extension, assetPaths); // Because some SyncObjects might be using the defaultMaterial
            FindAssetWithExtension(SyncPrefab.Extension, assetPaths); // TODO Investigate why it's not automatically triggered
        
            if (assetPaths.Count == 0)
            {
                EditorUtility.DisplayDialog(DialogText.title, DialogText.noSelectionMessage, DialogText.ok);
                return;
            }
        
            if (!EditorUtility.DisplayDialog(DialogText.title, $"Convert imported Reflect assets to {ReflectMaterialManager.converterName}", 
                DialogText.proceed, DialogText.cancel))
            {
                return;
            }
        
            AssetDatabase.StartAssetEditing();
        
            foreach (var assetPath in assetPaths)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
            }
            
            AssetDatabase.StopAssetEditing();
        }
        
        static void FindAssetWithExtension(string extension, List<string> assetPaths)
        {
            var fullPaths = Directory.GetFiles(Application.dataPath, $"*{extension}", SearchOption.AllDirectories);
        
            foreach (var path in fullPaths)
            {
                var assetPath = path.Replace(Application.dataPath, "Assets" + Path.DirectorySeparatorChar);
                assetPaths.Add(assetPath);
            }
        }
    }
}