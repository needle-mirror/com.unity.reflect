using Unity.Labs.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.ModuleLoader
{
    [CustomEditor(typeof(ModuleLoaderDebugSettings))]
    public class ModuleLoaderDebugSettingsEditor : Editor
    {
        SerializedProperty m_FunctionalityInjectionModuleLoggingProperty;
        SerializedProperty m_ModuleHideFlagsProperty;

        void OnEnable()
        {
            m_FunctionalityInjectionModuleLoggingProperty = serializedObject.FindProperty("m_FunctionalityInjectionModuleLogging");
            m_ModuleHideFlagsProperty = serializedObject.FindProperty("m_ModuleHideFlags");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_FunctionalityInjectionModuleLoggingProperty);

            using (var changed = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_ModuleHideFlagsProperty);
                if (changed.changed)
                    ModuleLoaderCore.instance.GetModuleParent().SetHideFlagsRecursively((HideFlags)m_ModuleHideFlagsProperty.intValue);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
