using System.Collections.Generic;
using Unity.Labs.Utils;
using UnityEditor;
using UnityEngine;
#if UNITY_2019_1_OR_NEWER
using UnityEngine.UIElements;
#else
using UnityEngine.Experimental.UIElements;
#endif

namespace Unity.Labs.ModuleLoader
{
    [CustomEditor(typeof(ModuleLoaderCore))]
    public class ModuleLoaderCoreEditor : Editor
    {
        ModuleLoaderCoreDrawer m_ModuleLoaderCoreDrawer;

        void OnEnable()
        {
            m_ModuleLoaderCoreDrawer = new ModuleLoaderCoreDrawer(serializedObject);
        }

        public override void OnInspectorGUI()
        {
            m_ModuleLoaderCoreDrawer.InspectorGUI(serializedObject);
        }
    }

    public class ModuleLoaderSettingsProvider : ScriptableSettingsProvider<ModuleLoaderCore>
    {
        const string k_MenuPath = "Project/Module Loader";

        ModuleLoaderCoreDrawer m_ModuleLoaderDrawer;

        public ModuleLoaderSettingsProvider(string path, SettingsScope scope = SettingsScope.Project) : base(path, scope) { }

        public override void OnActivate(string searchContext, VisualElement rootElement)
        {
            m_ModuleLoaderDrawer = new ModuleLoaderCoreDrawer(serializedObject);
        }

        public override void OnGUI(string searchContext)
        {
            m_ModuleLoaderDrawer.InspectorGUI(serializedObject);
        }

        [SettingsProvider]
        public static SettingsProvider CreateModuleLoaderSettingsProvider()
        {
            return new ModuleLoaderSettingsProvider(k_MenuPath)
            {
                keywords = new HashSet<string>(new[] { "module", "reload", "reload modules" })
            };
        }
    }

    public class ModuleLoaderCoreDrawer
    {
        ModuleLoaderSettingsEditor m_SettingsEditor;

        SerializedObject m_DebugSettings;

        SerializedProperty m_FunctionalityInjectionModuleLoggingProperty;
        SerializedProperty m_ModuleHideFlagsProperty;

        SerializedProperty m_SettingsOverrideProperty;
        SerializedProperty m_PlatformOverridesProperty;
        bool m_ShowModuleLoadOrder;
        bool m_ShowModuleUnloadOrder;
        bool m_ShowModuleBehaviorCallbackOrder;
        bool m_ShowModuleSceneCallbackOrder;
        bool m_ShowModuleBuildCallbackOrder;
        bool m_ShowModuleAssetCallbackOrder;

        public ModuleLoaderCoreDrawer(SerializedObject serializedObject)
        {
            m_DebugSettings = new SerializedObject(ModuleLoaderDebugSettings.instance);
            m_FunctionalityInjectionModuleLoggingProperty = m_DebugSettings.FindProperty("m_FunctionalityInjectionModuleLogging");
            m_ModuleHideFlagsProperty = m_DebugSettings.FindProperty("m_ModuleHideFlags");

            m_SettingsOverrideProperty = serializedObject.FindProperty("m_SettingsOverride");
            m_PlatformOverridesProperty = serializedObject.FindProperty("m_PlatformOverrides");

            m_SettingsEditor = new ModuleLoaderSettingsEditor();
        }

        public void InspectorGUI(SerializedObject serializedObject)
        {
            serializedObject.Update();
            m_DebugSettings.Update();

            var core = (ModuleLoaderCore)serializedObject.targetObject;

            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Reload Modules"))
                    core.ReloadModules();

                GUILayout.FlexibleSpace();
            }

            EditorGUILayout.LabelField("Debug Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_FunctionalityInjectionModuleLoggingProperty, new GUIContent("FI Module Logging"));
            EditorGUILayout.PropertyField(m_ModuleHideFlagsProperty);

            m_DebugSettings.ApplyModifiedProperties();

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Module Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(m_SettingsOverrideProperty);
            EditorGUILayout.PropertyField(m_PlatformOverridesProperty, true);

            var currentOverride = core.currentOverride;
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Current override", currentOverride, typeof(ModuleLoaderSettingsOverride), false);
            }

            SerializedProperty excludedTypesProperty;
            if (currentOverride)
            {
                var so = new SerializedObject(currentOverride);
                excludedTypesProperty = so.FindProperty("m_ExcludedTypes");
            }
            else
            {
                excludedTypesProperty = serializedObject.FindProperty("m_ExcludedTypes");
            }

            var modified = m_SettingsEditor.DrawEnabledModules(core.excludedTypes, excludedTypesProperty);

            DrawModules(ref m_ShowModuleLoadOrder, core.modules, "Load Order");
            DrawModules(ref m_ShowModuleUnloadOrder, core.moduleUnloads, "Unload Order");
            DrawModules(ref m_ShowModuleBehaviorCallbackOrder, core.behaviorCallbackModules, "Behavior Callback Order");
            DrawModules(ref m_ShowModuleSceneCallbackOrder, core.sceneCallbackModules, "Scene Callback Order");
            DrawModules(ref m_ShowModuleBuildCallbackOrder, core.buildCallbackModules, "Build Callback Order");
            DrawModules(ref m_ShowModuleAssetCallbackOrder, core.assetCallbackModules, "Asset Callback Order");

            modified |= serializedObject.ApplyModifiedProperties();

            if (modified)
                core.ReloadModules();
        }

        void DrawModules<T>(ref bool show, List<T> modules, string label)
        {
            show = EditorGUILayout.Foldout(show, label, true);
            if (!show)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (var module in modules)
                {
                    m_SettingsEditor.DrawModule(module.GetType());
                }
            }
        }
    }
}
