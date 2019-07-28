﻿using System;
using System.IO;
using Unity.Reflect.Model;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;
using UnityEngine.Reflect;
using File = Unity.Reflect.IO.File;

namespace UnityEditor.Reflect
{
    [ScriptedImporter(1, "SyncTexture")]
    public class SyncTextureScriptedImporter : ScriptedImporter
    {        
        public override void OnImportAsset(AssetImportContext ctx)
        {           
            var syncTexture = File.Load<SyncTexture>(ctx.assetPath);
     
            var textureImporter = new SyncTextureImporter();
            var texture = textureImporter.Import(syncTexture, null);

            if (syncTexture.Name != null)
            {
                texture.name = Path.GetFileNameWithoutExtension(syncTexture.Name);
            }
            
            ctx.AddObjectToAsset("texture", texture);
            
            var root = ScriptableObject.CreateInstance<ReflectScriptableObject>();

            ctx.AddObjectToAsset("root", root, AssetPreview.GetMiniThumbnail(texture));
            ctx.SetMainObject(root);
        }
    }
}
