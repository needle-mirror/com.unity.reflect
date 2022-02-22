using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;
using Object = UnityEngine.Object;

namespace UnityEditor.Reflect
{
    static class ReflectAssetsExtractor
    {
        internal enum TextureExtractionMode
        {
            None,
            UnityTexture2D,
            Png
        }
        
        internal enum MaterialExtractionMode
        {
            None,
            UnityMaterial,
        }

        internal enum MeshExtractionMode
        {
            None,
            UnityMesh,
            Fbx,
        }

        internal enum PrefabExtractionMode
        {
            None,
            Flat,
            WithNestedPrefabs,
        }
        
        internal class Settings
        {
            public string texturesFolder { get; set; }
            public string materialsFolder { get; set; }
            public string meshesFolder { get; set; }
            public string prefabsFolder { get; set; }
            public string mainPrefabsFolder { get; set; }

            public MaterialExtractionMode materialExtractionMode { get; set; }
            public bool materialOverride { get; set; }
            public bool materialSyncPrefabRemap { get; set; }
            
            public TextureExtractionMode textureExtractionMode { get; set; }
            public bool textureOverride { get; set; }
            
            public MeshExtractionMode meshExtractionMode { get; set; }
            public bool meshOverride { get; set; }
            
            public PrefabExtractionMode prefabExtractionMode { get; set; }
            public bool prefabOverride { get; set; }
            public int prefabMinOccurence { get; set; }
        }

        struct ExtractInfo
        {
            public string projectFullPath { get; }
            public Settings settings { get; }

            public ExtractInfo(string projectFullPath, Settings settings)
            {
                this.projectFullPath = projectFullPath;
                this.settings = settings;
            }
        }

        public static bool IsValidSyncPrefab(Object prefab)
        {
            return !string.IsNullOrEmpty(GetSyncPrefabPath(prefab));
        }
        
        static string GetSyncPrefabPath(Object prefab)
        {
            if (prefab == null)
                return null;

            var syncPrefabAssetPath = AssetDatabase.GetAssetPath(prefab);

            return HasExtension(syncPrefabAssetPath, SyncPrefab.Extension) ? syncPrefabAssetPath : null;
        }

        public static bool Extract(GameObject prefab, Settings settings)
        {
            var syncPrefabAssetPath = GetSyncPrefabPath(prefab);
            var extractInfo = new ExtractInfo(Application.dataPath.Replace("Assets", string.Empty), settings);

            if (!PrepareFolders(extractInfo))
            {
                Debug.LogError("Unable to prepare extraction folders. Please verify paths are valid.");
                return false;
            }
            
            // Gather dependencies
            var textureAssetPaths = new List<string>();
            var materialAssetPaths = new List<string>();
            var meshAssetPaths = new List<string>();

            foreach (var dependency in AssetDatabase.GetDependencies(syncPrefabAssetPath, true))
            {
                if (HasExtension(dependency, SyncTexture.Extension))
                {
                    textureAssetPaths.Add(dependency);
                }
                
                if (HasExtension(dependency, SyncMaterial.Extension))
                {
                    materialAssetPaths.Add(dependency);
                }
                
                if (HasExtension(dependency, SyncMesh.Extension))
                {
                    meshAssetPaths.Add(dependency);
                }
            }

            var generatedTextures = ExtractTextures(textureAssetPaths, extractInfo);

            Dictionary<string, Material> materialRemap = null;
            
            if (settings.materialExtractionMode != MaterialExtractionMode.None)
            {
                var textureRemap = new Dictionary<string, Texture>();

                foreach (var entry in generatedTextures)
                {
                    var previousAssetPath = entry.Key;
                    var newAssetPath = entry.Value;

                    textureRemap.Add(previousAssetPath.ToLower(), AssetDatabase.LoadAssetAtPath<Texture>(newAssetPath));
                }
                
                var generatedMaterials = ExtractMaterials(materialAssetPaths, textureRemap, extractInfo);

                // Fix importers Normal Maps Flags
                BumpMapSettings.EnableSilentMode(); // Hack to avoid getting the popup prompting to fix normals.
                UnityEditorInternal.InternalEditorUtility.PerformUnmarkedBumpMapTexturesFixing();
                BumpMapSettings.RestoreSilentMode();

                materialRemap = new Dictionary<string, Material>();

                foreach (var entry in generatedMaterials)
                {
                    var previousAssetPath = entry.Key;
                    var newAssetPath = entry.Value;

                    materialRemap.Add(previousAssetPath.ToLower(), AssetDatabase.LoadAssetAtPath<Material>(newAssetPath));
                }

                if (settings.materialSyncPrefabRemap)
                {
                    RemapSyncPrefabMaterials(materialRemap, syncPrefabAssetPath);
                }
            }

            var generatedMeshes = ExtractMeshes(meshAssetPaths, extractInfo);

            if (settings.prefabExtractionMode != PrefabExtractionMode.None)
            {
                var meshRemap = new Dictionary<string, Mesh>();

                foreach (var entry in generatedMeshes)
                {
                    var previousAssetPath = entry.Key;
                    var newAssetPath = entry.Value;

                    Mesh mesh;
                    
                    var asset = AssetDatabase.LoadAssetAtPath<Object>(newAssetPath);
                    if (asset is GameObject gameObject)
                    {
                        mesh = gameObject.GetComponent<MeshFilter>().sharedMesh;
                    }
                    else
                    {
                        mesh = asset as Mesh;
                    }

                    meshRemap.Add(previousAssetPath.ToLower(), mesh);
                }

                Dictionary<int, string> prefabRemap = null;
                
                if (settings.prefabExtractionMode == PrefabExtractionMode.WithNestedPrefabs)
                {
                    prefabRemap = ExtractNestedPrefabs(prefab.transform, materialRemap, meshRemap, extractInfo);
                }

                ExtractMainPrefab(prefab, prefabRemap, materialRemap, meshRemap, extractInfo.settings.mainPrefabsFolder);
            }

            return true;
        }

        static void RemapSyncPrefabMaterials(Dictionary<string, Material> materialRemap, string syncPrefabAssetPath)
        {
            var materialNameRemap = new Dictionary<string, Material>();
            
            foreach (var generatedMaterial in materialRemap)
            {
                materialNameRemap.Add(Path.GetFileNameWithoutExtension(generatedMaterial.Key).ToLower(), generatedMaterial.Value);
            }

            var syncPrefabImporter = (SyncPrefabScriptedImporter)AssetImporter.GetAtPath(syncPrefabAssetPath);

            var remaps = syncPrefabImporter.MaterialRemaps;

            for (int i = 0; i < remaps.Length; ++i)
            {
                var remap = remaps[i];

                if (remap.remappedMaterial != null && AssetDatabase.Contains(remap.remappedMaterial))
                {
                    continue;
                }

                if (materialNameRemap.TryGetValue(remap.syncMaterialName.ToLower(), out var material))
                {
                    remap.remappedMaterial = material;
                    remaps[i] = remap;
                }
                else
                {
                    Debug.LogWarning($"Unable to find {remap.syncMaterialName}");
                }
            }

            syncPrefabImporter.MaterialRemaps = remaps;

            EditorUtility.SetDirty(syncPrefabImporter);

            syncPrefabImporter.SaveAndReimport();
        }
        
        static bool PrepareFolders(ExtractInfo info)
        {
            try
            { 
                if (info.settings.textureExtractionMode != TextureExtractionMode.None)
                {
                    EnsureFolder(Path.Combine(info.projectFullPath, info.settings.texturesFolder));
                }
                
                if (info.settings.materialExtractionMode != MaterialExtractionMode.None)
                {
                    EnsureFolder(Path.Combine(info.projectFullPath, info.settings.materialsFolder));
                }
                
                if (info.settings.meshExtractionMode != MeshExtractionMode.None)
                {
                    EnsureFolder(Path.Combine(info.projectFullPath, info.settings.meshesFolder));
                }
                
                if (info.settings.prefabExtractionMode == PrefabExtractionMode.WithNestedPrefabs)
                {
                    EnsureFolder(Path.Combine(info.projectFullPath, info.settings.prefabsFolder));
                }
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
                return false;
            }

            return true;
        }

        static void EnsureFolder(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        static bool HasExtension(string assetPath, string extension)
        {
            return assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase);
        }

        static bool AssetExists(string projectFullPath, string assetPath)
        {
            return File.Exists(Path.Combine(projectFullPath, assetPath));
        }
        
        static Dictionary<string, string> ExtractMeshes(IEnumerable<string> meshAssetPaths, ExtractInfo info)
        {
            var projectFullPath = info.projectFullPath;
            var extractionMode = info.settings.meshExtractionMode;
            var assetFolder = info.settings.meshesFolder;
            var overrideExisting = info.settings.meshOverride;
            
            var generated = new Dictionary<string, string>();

            if (extractionMode == MeshExtractionMode.None)
                return generated;
            
#if !FBX_EXPORTER_AVAILABLE
            if (extractionMode == MeshExtractionMode.Fbx)
            {
                extractionMode = MeshExtractionMode.UnityMesh;
                Debug.LogError("FBXExporter package required to generate FBX meshes.");
            }
#endif
            
            AssetDatabase.StartAssetEditing();

            foreach (var meshAssetPath in meshAssetPaths)
            {
                var newMeshAssetPath = $"{assetFolder}/{Path.GetFileNameWithoutExtension(meshAssetPath)}";
                
                switch (extractionMode)
                {
                    case MeshExtractionMode.UnityMesh:
                        newMeshAssetPath += ".asset";
                        break;
                    case MeshExtractionMode.Fbx:
                        newMeshAssetPath += ".fbx";
                        break;
                    default:
                        Debug.LogError($"Unknown texture generation type {extractionMode}");
                        continue;
                }

                if (overrideExisting || !AssetExists(projectFullPath, newMeshAssetPath))
                {
                    var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshAssetPath);

                    if (extractionMode == MeshExtractionMode.Fbx)
                    {
#if FBX_EXPORTER_AVAILABLE
                        var root = new GameObject(mesh.name);
                        root.AddComponent<MeshFilter>().sharedMesh = mesh;

                        // Make sure to assign a material for each subMesh otherwise the FBX Exporter will merge them
                        var renderer = root.AddComponent<MeshRenderer>();
                        var materials = new Material[mesh.subMeshCount];

                        for (int i = 0; i < materials.Length; ++i)
                        {
                            var mat = new Material(Shader.Find("VertexLit")) { name = $"material_{i}" };
                            materials[i] = mat;
                        }

                        renderer.sharedMaterials = materials;

                        Formats.Fbx.Exporter.ModelExporter.ExportObject(Path.Combine(projectFullPath, newMeshAssetPath), root);

                        Object.DestroyImmediate(root);
#endif
                    }
                    else // MeshExtractionMode.UnityMesh
                    {
                        var copy = Object.Instantiate(mesh);
                        AssetDatabase.CreateAsset(copy, newMeshAssetPath);
                    }
                }

                generated.Add(meshAssetPath, newMeshAssetPath);
            }
            
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
            
            return generated;
        }

        static Dictionary<string, string> ExtractTextures(IEnumerable<string> textureAssetPaths, ExtractInfo info)
        {
            var generated = new Dictionary<string, string>();

            var projectFullPath = info.projectFullPath;
            var extractionMode = info.settings.textureExtractionMode;
            var assetFolder = info.settings.texturesFolder;
            var overrideExisting = info.settings.textureOverride;

            if (extractionMode == TextureExtractionMode.None)
                return generated;
            
            AssetDatabase.StartAssetEditing();

            foreach (var textureAssetPath in textureAssetPaths)
            {
                try
                {
                    var newTextureAssetPath = $"{assetFolder}/{Path.GetFileNameWithoutExtension(textureAssetPath)}";

                    switch (extractionMode)
                    {
                        case TextureExtractionMode.UnityTexture2D:
                            newTextureAssetPath += ".asset";
                            break;
                        
                        case TextureExtractionMode.Png:
                            newTextureAssetPath += ".png";
                            break;
                        
                        default:
                            Debug.LogError($"Unknown texture generation type {extractionMode}");
                            continue;
                    }

                    if (overrideExisting || !AssetExists(projectFullPath, newTextureAssetPath))
                    {
                        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);

                        if (extractionMode == TextureExtractionMode.Png)
                        {
                            var data = texture.EncodeToPNG();
                            File.WriteAllBytes(Path.Combine(projectFullPath, newTextureAssetPath), data);
                        }
                        else // TextureExtractionMode.UnityTexture2D
                        {
                            var copy = Object.Instantiate(texture);
                            AssetDatabase.CreateAsset(copy, newTextureAssetPath);
                        }
                    }
                    
                    generated.Add(textureAssetPath, newTextureAssetPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error extracting texture {textureAssetPath}: {e.Message}");
                }
            }
            
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();

            return generated;
        }

        static Dictionary<string, string> ExtractMaterials(IEnumerable<string> materialAssetPaths,
            IReadOnlyDictionary<string, Texture> textureRemap, ExtractInfo info)
        {
            var projectFullPath = info.projectFullPath;
            var assetFolder = info.settings.materialsFolder;
            var overrideExisting = info.settings.materialOverride;
            
            var generated = new Dictionary<string, string>();
            
            AssetDatabase.StartAssetEditing();

            foreach (var materialAssetPath in materialAssetPaths)
            {
                try
                {
                    var newMaterialAssetPath = $"{assetFolder}/{Path.GetFileNameWithoutExtension(materialAssetPath)}.mat";

                    if (overrideExisting || !AssetExists(projectFullPath, newMaterialAssetPath))
                    {
                        var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);

                        material = new Material(material);

                        if (textureRemap != null)
                        {
                            ReplaceTextures(material, textureRemap);
                        }

                        AssetDatabase.CreateAsset(material, newMaterialAssetPath);
                    }

                    generated.Add(materialAssetPath, newMaterialAssetPath);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error extracting Material {materialAssetPath}: {e.Message}");
                }
            }

            AssetDatabase.StopAssetEditing();
            
            return generated;
        }
        
        static Dictionary<int,string> ExtractNestedPrefabs(Transform root,
            IReadOnlyDictionary<string, Material> materialRemap, IReadOnlyDictionary<string, Mesh> meshRemap, 
            ExtractInfo info)
        {
            var projectFullPath = info.projectFullPath;
            var assetFolder = info.settings.prefabsFolder;
            var overrideExisting = info.settings.prefabOverride;
            var minPrefabOccurence = info.settings.prefabMinOccurence;
                
            AssetDatabase.StartAssetEditing();
            

            var prefabsData = new Dictionary<string, List<int>>();

            foreach (Transform child in root)
            {
                try
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(child))
                    {
                        var prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(child.gameObject);

                        if (!prefabsData.TryGetValue(prefabPath, out var ids))
                        {
                            prefabsData[prefabPath] = ids = new List<int>();
                        }

                        ids.Add(child.GetInstanceID());
                    }
                }
                catch (Exception e)
                {
                    var name = child == null ? "NULL" : child.name;
                    Debug.LogError($"Error checking GameObject {name} : {e.Message}");
                }
            }

            var prefabPathCache = new Dictionary<int, string>();
            
            foreach (var data in prefabsData)
            {
                try
                {
                    var ids = data.Value;

                    if (ids.Count < minPrefabOccurence)
                        continue;

                    var prefabPath = data.Key;

                    var name = Path.GetFileNameWithoutExtension(prefabPath);
                    var newPrefabPath = $"{assetFolder}/{name}.prefab";

                    if (overrideExisting || !AssetExists(projectFullPath, newPrefabPath))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                        var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
                        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

                        ReplaceMaterials(instance, materialRemap);

                        ReplaceMeshes(instance, meshRemap);

                        PrefabUtility.SaveAsPrefabAsset(instance, newPrefabPath);

                        Object.DestroyImmediate(instance);
                    }

                    foreach (var instanceId in ids)
                    {
                        prefabPathCache.Add(instanceId, newPrefabPath);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error extracting Prefab {data.Key}: {e.Message}");
                }
            }

            AssetDatabase.StopAssetEditing();

            return prefabPathCache;
        }

        static void ExtractMainPrefab(GameObject prefab, IReadOnlyDictionary<int, string> prefabPathCache,
            IReadOnlyDictionary<string, Material> materialRemap, IReadOnlyDictionary<string, Mesh> meshRemap, string assetFolder)
        {
            // Extract Main Prefab
            var root = new GameObject($"{prefab.name} (Editable)");
            
            var syncPrefabBindingComponent = prefab.GetComponent<SyncPrefabBinding>();

            if (syncPrefabBindingComponent != null)
            {
                UnityEditorInternal.ComponentUtility.CopyComponent(syncPrefabBindingComponent);
                UnityEditorInternal.ComponentUtility.PasteComponentAsNew(root);
            }
            
            foreach (Transform child in prefab.transform)
            {
                if (prefabPathCache != null && prefabPathCache.TryGetValue(child.GetInstanceID(), out var prefabPath))
                {
                    var nestedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                    var instance = ((GameObject)PrefabUtility.InstantiatePrefab(nestedPrefab, root.transform)).transform;

                    instance.name = child.name;
                    instance.localPosition = child.localPosition;
                    instance.localRotation = child.localRotation;
                    instance.localScale = child.localScale;

                    var syncObjectBindingComponent = child.GetComponent<SyncObjectBinding>();

                    if (syncObjectBindingComponent != null)
                    {
                        UnityEditorInternal.ComponentUtility.CopyComponent(syncObjectBindingComponent);
                        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(instance.gameObject);
                    }

                    var metadataComponent = child.GetComponent<Metadata>();

                    if (metadataComponent != null)
                    {
                        UnityEditorInternal.ComponentUtility.CopyComponent(metadataComponent);
                        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(instance.gameObject);
                    }
                }
                else
                {
                    var instance = Object.Instantiate(child.gameObject, root.transform);
                    instance.name = instance.name.Replace("(Clone)", string.Empty);
                    
                    ReplaceMaterials(instance, materialRemap);
                    ReplaceMeshes(instance, meshRemap);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, $"{assetFolder}/{root.name}.prefab");
            
            Object.DestroyImmediate(root);
        }
        
        static void ReplaceTextures(Material material, IReadOnlyDictionary<string, Texture> textureRemap)
        {
            // Use serialization to replace Textures. This will work with any type of Shader assigned to this Material.
            var serializedObject = new SerializedObject(material);
            var iterator = serializedObject.GetIterator();

            while (iterator.Next(true))
            {
                if (iterator.type.Contains("PPtr<Texture>") && iterator.objectReferenceInstanceIDValue != 0)
                {
                    var asset = AssetDatabase.GetAssetPath(iterator.objectReferenceInstanceIDValue);

                    if (textureRemap.TryGetValue(asset.ToLower(), out var texture))
                    {
                        iterator.objectReferenceValue = texture;
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();
        }

        static void ReplaceMaterials(GameObject prefab, IReadOnlyDictionary<string, Material> materialRemap)
        {
            if (materialRemap == null || materialRemap.Count == 0)
                return;
            
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
            {
                var materials = renderer.sharedMaterials;

                for (int i = 0; i < materials.Length; ++i)
                {
                    var assetPath = AssetDatabase.GetAssetPath(materials[i]);
                    if (materialRemap.TryGetValue(assetPath.ToLower(), out var material))
                    {
                        materials[i] = material;
                    }
                }
                
                renderer.sharedMaterials = materials;
            }
        }
        
        static void ReplaceMeshes(GameObject prefab, IReadOnlyDictionary<string, Mesh> meshRemap)
        {
            if (meshRemap == null || meshRemap.Count == 0)
                return;
            
            foreach (var meshFilter in prefab.GetComponentsInChildren<MeshFilter>())
            {
                var assetPath = AssetDatabase.GetAssetPath(meshFilter.sharedMesh);
                if (meshRemap.TryGetValue(assetPath.ToLower(), out var mesh))
                {
                    meshFilter.sharedMesh = mesh;
                }
            }
        }

        static class BumpMapSettings
        {
            static PropertyInfo s_PropertyInfo;
            static bool s_OriginalValue;

            static BumpMapSettings()
            {
                if (s_PropertyInfo == null)
                {
                    Assembly asm = Assembly.Load("UnityEditor");
                    Type testType = asm.GetType("UnityEditor.BumpMapSettings");

                    s_PropertyInfo = testType.GetProperty("silentMode", BindingFlags.Static | BindingFlags.Public);
                    s_OriginalValue = (bool)s_PropertyInfo.GetValue(null, null);
                }
            }

            public static void EnableSilentMode()
            {
                s_PropertyInfo.SetValue(null, true);
            }
            
            public static void RestoreSilentMode()
            {
                s_PropertyInfo.SetValue(null, s_OriginalValue);
            }
        }
    }
}