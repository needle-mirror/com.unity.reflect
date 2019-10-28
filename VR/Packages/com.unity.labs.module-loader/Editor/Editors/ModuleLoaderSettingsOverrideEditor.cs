using System.Collections.Generic;
using UnityEditor;

namespace Unity.Labs.ModuleLoader
{
    [CustomEditor(typeof(ModuleLoaderSettingsOverride))]
    public class ModuleLoaderSettingsOverrideEditor : Editor
    {
        ModuleLoaderSettingsEditor m_SettingsEditor;
        List<string> m_ExcludedTypes;
        SerializedProperty m_ExcludedTypesProperty;

        void OnEnable()
        {
            m_SettingsEditor = new ModuleLoaderSettingsEditor();
            m_ExcludedTypesProperty = serializedObject.FindProperty("m_ExcludedTypes");
            m_ExcludedTypes = ((ModuleLoaderSettingsOverride)target).ExcludedTypes;
        }

        public override void OnInspectorGUI()
        {
            m_SettingsEditor.DrawEnabledModules(m_ExcludedTypes, m_ExcludedTypesProperty);
        }
    }
}
