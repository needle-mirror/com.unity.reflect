using System;
using System.IO;
using System.Linq;
using Unity.Reflect.Utils;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UnityEditor.Reflect
{
    class ReflectAssetsExtractorWindow : EditorWindow, ISerializationCallbackReceiver
    {
        static readonly string k_PackagePath = "Packages/com.unity.reflect";
        static readonly string k_ResourcePath = $"{k_PackagePath}/Editor/{nameof(ReflectAssetsExtractorWindow)}";
        static readonly string k_LayoutPath = k_ResourcePath + "/Layouts";

        static readonly string k_ReflectExtractAssetsWindowLayoutPath = $"{k_LayoutPath}/{nameof(ReflectAssetsExtractorWindow)}.uxml";

        [SerializeField]
        GameObject m_SelectedSyncPrefab;
        
        [SerializeField]
        string m_PrefabExtractPathValue;
        
        [SerializeField]
        string m_NestedPrefabsExtractPathValue;
        
        [SerializeField]
        string m_MaterialsExtractPathValue;
        
        [SerializeField]
        string m_TexturesExtractPathValue;
        
        [SerializeField]
        string m_MeshesExtractPathValue;
        
        ObjectField m_SyncPrefabField;
        
        EnumField m_PrefabModeEnum;
        EnumField m_MaterialModeEnum;
        EnumField m_TextureModeEnum;
        EnumField m_MeshModeEnum;
        
#if !FBX_EXPORTER_AVAILABLE
        HelpBox m_MeshErrorBox;
#endif
        HelpBox m_WarningBox;
        
        TextField m_PrefabExtractPath;
        TextField m_NestedPrefabsExtractPath;
        TextField m_MaterialsExtractPath;
        TextField m_TexturesExtractPath;
        TextField m_MeshesExtractPath;

        IntegerField m_PrefabsMinOccurence;
        
        Toggle m_MaterialRemap;
        Toggle m_OverrideExisting;

        Button m_ExtractButton;
        
        static class SaveKeys
        {
            public static readonly string prefabModeEnumKey = "ReflectAssetsExtractor_PrefabModeEnum";
            public static readonly string materialModeEnumKey = "ReflectAssetsExtractor_MaterialModeEnum";
            public static readonly string textureModeEnumKey = "ReflectAssetsExtractor_TextureModeEnum";
            public static readonly string meshModeEnumKey = "ReflectAssetsExtractor_MeshModeEnum";

            public static readonly string prefabsMinOccurenceKey = "ReflectAssetsExtractor_PrefabsMinOccurence";
            public static readonly string materialRemapKey = "ReflectAssetsExtractor_MaterialRemap";
            public static readonly string overrideExistingKey = "ReflectAssetsExtractor_OverrideExisting";
        }
        
        public static void ShowWindow(GameObject prefab)
        {
            if (!CheckSyncPrefabValidity(prefab))
                return;
            
            var window = GetWindow<ReflectAssetsExtractorWindow>("Reflect Assets Extractor");
            window.minSize = new Vector2(250.0f, 200.0f);
            
            window.Show();

            window.m_SyncPrefabField.value = prefab;
            window.UpdateExtractFolders(prefab);
        }
        
        [MenuItem ("Assets/Reflect/Extract Assets")]
        static void ExtractAssets()
        {
            ShowWindow(Selection.activeObject as GameObject);
        }
        
        [MenuItem("Assets/Reflect/Extract Assets", true)]
        static bool ValidateExtractAssets()
        {
            return ReflectAssetsExtractor.IsValidSyncPrefab(Selection.activeObject as GameObject);
        }

        void OnEnable()
        {
            var uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_ReflectExtractAssetsWindowLayoutPath);

            Initialize(uiAsset);
        }

        void OnDisable()
        {
            SaveSettings();
        }

        static bool CheckSyncPrefabValidity(Object prefab)
        {
            if (!ReflectAssetsExtractor.IsValidSyncPrefab(prefab))
            {
                var error = prefab == null ? "Invalid SyncPrefab" : $"{prefab.name} is not a valid SyncPrefab asset.";
                Debug.LogError(error);
                
                return false;
            }

            return true;
        }

        void Initialize(VisualTreeAsset root)
        {
            var ui = root.CloneTree();
            rootVisualElement.Add(ui);

            // Make sure the content takes the full available space.
            ui.style.width = ui.style.height = new StyleLength(new Length(100, UnityEngine.UIElements.LengthUnit.Percent));

            m_SyncPrefabField = ui.Q<ObjectField>("syncPrefab-field");
            m_SyncPrefabField.objectType = typeof(GameObject);
            m_SyncPrefabField.allowSceneObjects = false;
            m_SyncPrefabField.value = m_SelectedSyncPrefab;
            m_SyncPrefabField.SetEnabled(false); // Do not allow direct modification of the associated SyncPrefab

            m_PrefabModeEnum = ui.Q<EnumField>("prefab-mode");
            m_PrefabModeEnum.Init(ReflectAssetsExtractor.PrefabExtractionMode.Flat);
            m_PrefabModeEnum.RegisterValueChangedCallback(evt => { RefreshUIState(); });
            
            m_PrefabsMinOccurence = ui.Q<IntegerField>("nested-prefabs-min-occurence");
            m_PrefabsMinOccurence.RegisterValueChangedCallback(evt =>
            {
                m_PrefabsMinOccurence.value = Mathf.Max(1, evt.newValue);
            });

            m_MaterialModeEnum = ui.Q<EnumField>("material-mode");
            m_MaterialModeEnum.Init(ReflectAssetsExtractor.MaterialExtractionMode.None);
            m_MaterialModeEnum.RegisterValueChangedCallback(evt => { RefreshUIState(); });
            
            m_MaterialRemap = ui.Q<Toggle>("material-remap");

            m_TextureModeEnum = ui.Q<EnumField>("texture-mode");
            m_TextureModeEnum.Init(ReflectAssetsExtractor.TextureExtractionMode.None);

            m_MeshModeEnum = ui.Q<EnumField>("mesh-mode");
            m_MeshModeEnum.Init(ReflectAssetsExtractor.MeshExtractionMode.None);

            m_OverrideExisting = ui.Q<Toggle>("override-existing");
            m_OverrideExisting.RegisterValueChangedCallback(evt => { RefreshUIState(); });

            var prefabPathSelector = ui.Q<VisualElement>("prefab-path-selector");
            m_PrefabExtractPath = InitPathSelector(prefabPathSelector, "Main Prefab");
            m_PrefabExtractPath.value = m_PrefabExtractPathValue;
            m_PrefabExtractPath.RegisterCallback<FocusOutEvent>(evt => { RefreshUIState(); });
            
            var nestedPrefabsPathSelector = ui.Q<VisualElement>("nested-prefabs-path-selector");
            m_NestedPrefabsExtractPath = InitPathSelector(nestedPrefabsPathSelector, "Nested Prefabs");
            m_NestedPrefabsExtractPath.value = m_NestedPrefabsExtractPathValue;
            m_NestedPrefabsExtractPath.RegisterCallback<FocusOutEvent>(evt => { RefreshUIState(); });
            
            var materialsPathSelector = ui.Q<VisualElement>("materials-path-selector");
            m_MaterialsExtractPath = InitPathSelector(materialsPathSelector, "Materials");
            m_MaterialsExtractPath.value = m_MaterialsExtractPathValue;
            m_MaterialsExtractPath.RegisterCallback<FocusOutEvent>(evt => { RefreshUIState(); });
            
            var texturesPathSelector = ui.Q<VisualElement>("textures-path-selector");
            m_TexturesExtractPath = InitPathSelector(texturesPathSelector, "Textures");
            m_TexturesExtractPath.value = m_TexturesExtractPathValue;
            m_TexturesExtractPath.RegisterCallback<FocusOutEvent>(evt => { RefreshUIState(); });
            
            var meshesPathSelector = ui.Q<VisualElement>("meshes-path-selector");
            m_MeshesExtractPath = InitPathSelector(meshesPathSelector, "Meshes");
            m_MeshesExtractPath.value = m_MeshesExtractPathValue;
            m_MeshesExtractPath.RegisterCallback<FocusOutEvent>(evt => { RefreshUIState(); });

            m_ExtractButton = ui.Q<Button>("extract-button");
            m_ExtractButton.RegisterCallback<ClickEvent>(evt =>
            {
                var prefab = m_SyncPrefabField.value as GameObject;
                
                if (!CheckSyncPrefabValidity(prefab))
                    return;
                
                SaveSettings();

                // Settings
                var settings = new ReflectAssetsExtractor.Settings
                { 
                    mainPrefabsFolder = m_PrefabExtractPath.value,
                    prefabsFolder = m_NestedPrefabsExtractPath.value,
                    materialsFolder = m_MaterialsExtractPath.value,
                    texturesFolder = m_TexturesExtractPath.value,
                    meshesFolder = m_MeshesExtractPath.value,

                    materialExtractionMode = (ReflectAssetsExtractor.MaterialExtractionMode)m_MaterialModeEnum.value,
                    materialOverride = m_OverrideExisting.value,
                    materialSyncPrefabRemap = m_MaterialRemap.value,
                
                    textureExtractionMode = (ReflectAssetsExtractor.TextureExtractionMode)m_TextureModeEnum.value,
                    textureOverride = m_OverrideExisting.value,
                
                    meshExtractionMode = (ReflectAssetsExtractor.MeshExtractionMode)m_MeshModeEnum.value,
                    meshOverride = m_OverrideExisting.value,
                
                    prefabExtractionMode = (ReflectAssetsExtractor.PrefabExtractionMode)m_PrefabModeEnum.value,
                    prefabOverride = m_OverrideExisting.value,
                    prefabMinOccurence = m_PrefabsMinOccurence.value,
                };

                if (ReflectAssetsExtractor.Extract(prefab, settings))
                {
                    Close();
                }
            });

            m_SyncPrefabField.RegisterValueChangedCallback(evt =>
            {
                var prefab = evt.newValue as GameObject;

                if (prefab == null)
                    return;

                if (!CheckSyncPrefabValidity(prefab))
                    return;
                
                UpdateExtractFolders(prefab);
            });

#if !FBX_EXPORTER_AVAILABLE
            m_MeshErrorBox = ui.Q<HelpBox>("error-message");
            m_MeshErrorBox.text = "Please install the FBX Exporter package from the Package Manager to enable FBX extraction.";

            m_MeshModeEnum.RegisterValueChangedCallback(evt => { RefreshUIState(); });
#endif
            
            m_WarningBox = ui.Q<HelpBox>("warn-message");
            m_WarningBox.text = "At least one extraction folder is not empty and its content might be overridden.";
            m_WarningBox.text += "\nIf not sure, toggle off 'Override Existing' in the Advanced settings.";

            LoadSettings();
        }

        void SaveSettings()
        {
            EditorPrefs.SetInt( SaveKeys.prefabModeEnumKey, (int)(ReflectAssetsExtractor.PrefabExtractionMode)m_PrefabModeEnum.value); 
            EditorPrefs.SetInt( SaveKeys.materialModeEnumKey, (int)(ReflectAssetsExtractor.MaterialExtractionMode)m_MaterialModeEnum.value); 
            EditorPrefs.SetInt( SaveKeys.textureModeEnumKey, (int)(ReflectAssetsExtractor.TextureExtractionMode)m_TextureModeEnum.value); 
            EditorPrefs.SetInt( SaveKeys.meshModeEnumKey, (int)(ReflectAssetsExtractor.MeshExtractionMode)m_MeshModeEnum.value); 

            EditorPrefs.SetInt( SaveKeys.prefabsMinOccurenceKey, m_PrefabsMinOccurence.value); 
            EditorPrefs.SetBool( SaveKeys.materialRemapKey, m_MaterialRemap.value); 
            EditorPrefs.SetBool( SaveKeys.overrideExistingKey, m_OverrideExisting.value);
        }

        void LoadSettings()
        {
            m_PrefabModeEnum.value = (ReflectAssetsExtractor.PrefabExtractionMode) EditorPrefs.GetInt( SaveKeys.prefabModeEnumKey, (int)(ReflectAssetsExtractor.PrefabExtractionMode.Flat)); 
            m_MaterialModeEnum.value = (ReflectAssetsExtractor.MaterialExtractionMode) EditorPrefs.GetInt( SaveKeys.materialModeEnumKey, (int)(ReflectAssetsExtractor.MaterialExtractionMode.None)); 
            m_TextureModeEnum.value = (ReflectAssetsExtractor.TextureExtractionMode) EditorPrefs.GetInt( SaveKeys.textureModeEnumKey, (int)(ReflectAssetsExtractor.TextureExtractionMode.None)); 
            m_MeshModeEnum.value = (ReflectAssetsExtractor.MeshExtractionMode) EditorPrefs.GetInt( SaveKeys.meshModeEnumKey, (int)(ReflectAssetsExtractor.MeshExtractionMode.None)); 

            m_PrefabsMinOccurence.value = EditorPrefs.GetInt( SaveKeys.prefabsMinOccurenceKey, 2); 
            m_MaterialRemap.value = EditorPrefs.GetBool( SaveKeys.materialRemapKey, false); 
            m_OverrideExisting.value = EditorPrefs.GetBool( SaveKeys.overrideExistingKey, false);

            RefreshUIState();
        }

        void RefreshUIState()
        {
            // Prefab
            var applyOccurence = m_PrefabModeEnum.value.Equals(ReflectAssetsExtractor.PrefabExtractionMode.WithNestedPrefabs);
            m_PrefabsMinOccurence.SetEnabled(applyOccurence);

            // Materials
            var enabled = !m_MaterialModeEnum.value.Equals(ReflectAssetsExtractor.MaterialExtractionMode.None);
            m_MaterialRemap.SetEnabled(enabled);
            m_TextureModeEnum.SetEnabled(enabled);
            
            // Meshes
#if !FBX_EXPORTER_AVAILABLE
            var fbxExport = m_MeshModeEnum.value.Equals(ReflectAssetsExtractor.MeshExtractionMode.Fbx);

            if (fbxExport)
            {
                UIToolkitUtils.DisplayFlex(m_MeshErrorBox);
                m_ExtractButton.SetEnabled(false);
            }
            else
            {
                UIToolkitUtils.DisplayNone(m_MeshErrorBox);
                m_ExtractButton.SetEnabled(true);
            }
#endif
            var overrideRisk = ExtractionFoldersHaveOverrideRisk();
            
            if (overrideRisk)
            {
                UIToolkitUtils.DisplayFlex(m_WarningBox);
            }
            else
            {
                UIToolkitUtils.DisplayNone(m_WarningBox);
            }
        }

        bool ExtractionFoldersHaveOverrideRisk()
        {
            if (!m_OverrideExisting.value)
                return false;
            
            return (ReflectAssetsExtractor.PrefabExtractionMode)m_PrefabModeEnum.value != ReflectAssetsExtractor.PrefabExtractionMode.None && FolderIsNotEmpty(m_PrefabExtractPath.value)
                   || (ReflectAssetsExtractor.PrefabExtractionMode)m_PrefabModeEnum.value == ReflectAssetsExtractor.PrefabExtractionMode.WithNestedPrefabs && FolderIsNotEmpty(m_NestedPrefabsExtractPath.value)
                   || (ReflectAssetsExtractor.MaterialExtractionMode)m_MaterialModeEnum.value != ReflectAssetsExtractor.MaterialExtractionMode.None && FolderIsNotEmpty(m_MaterialsExtractPath.value)
                   || (ReflectAssetsExtractor.TextureExtractionMode)m_TextureModeEnum.value != ReflectAssetsExtractor.TextureExtractionMode.None && FolderIsNotEmpty(m_TexturesExtractPath.value)
                   || (ReflectAssetsExtractor.MeshExtractionMode)m_MeshModeEnum.value != ReflectAssetsExtractor.MeshExtractionMode.None && FolderIsNotEmpty(m_MeshesExtractPath.value);
        }
        
        static bool FolderIsNotEmpty(string assetFolder)
        {
            if (string.IsNullOrEmpty(assetFolder))
                return false;
            
            var fullPath = assetFolder.Replace("Assets", Application.dataPath);
            
            if (string.IsNullOrEmpty(fullPath))
                return false;
            
            return Directory.Exists(fullPath) && Directory.EnumerateFiles(fullPath).Any(); // Top level directory only
        }

        void UpdateExtractFolders(Object prefab)
        {
            var prefabName = string.IsNullOrEmpty(prefab.name) ? $"Model_{prefab.GetInstanceID()}" : FileUtils.SanitizeName(prefab.name);

            var rootFolder = Path.Combine("Assets/Reflect/Extracted/", prefabName);

            m_PrefabExtractPath.value = rootFolder;
            m_NestedPrefabsExtractPath.value = Path.Combine(rootFolder, "Prefabs");
            m_MaterialsExtractPath.value = Path.Combine(rootFolder, "Materials");
            m_TexturesExtractPath.value = Path.Combine(rootFolder, "Textures");
            m_MeshesExtractPath.value = Path.Combine(rootFolder, "Meshes");
        }

        static TextField InitPathSelector(VisualElement ui, string type)
        {
            var path = ui.Q<TextField>("path");
            var button = ui.Q<Button>("select-folder-button");
            
            button.RegisterCallback<ClickEvent>(evt =>
            {
                var folder = Directory.Exists(path.value) ? path.value : Application.dataPath;
                
                var selectedPath = EditorUtility.OpenFolderPanel($"Select Folder For {type} Extraction", folder, null);

                if (selectedPath.Contains(Application.dataPath))
                {
                    path.value = selectedPath.Replace(Application.dataPath, "Assets");
                }
                else
                {
                    Debug.LogError("Only folders under the Assets folder are allowed.");
                }
            });

            return path;
        }

        public void OnBeforeSerialize()
        {
            m_SelectedSyncPrefab = m_SyncPrefabField.value as GameObject;
            
            m_PrefabExtractPathValue = m_PrefabExtractPath.value;
            m_NestedPrefabsExtractPathValue = m_NestedPrefabsExtractPath.value;
            m_MaterialsExtractPathValue = m_MaterialsExtractPath.value;
            m_TexturesExtractPathValue = m_TexturesExtractPath.value;
            m_MeshesExtractPathValue = m_MeshesExtractPath.value;
        }

        public void OnAfterDeserialize()
        {
            // Nothing
        }
    }
}