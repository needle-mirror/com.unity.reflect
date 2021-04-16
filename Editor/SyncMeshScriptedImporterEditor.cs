using System;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityEditor.Reflect
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SyncMeshScriptedImporter))]
    public class SyncMeshScriptedImporterEditor : ScriptedImporterEditor
    {
        SerializedProperty m_GenerateLightmapUVsProperty;

        public override void OnInspectorGUI()
        {
            if (m_GenerateLightmapUVsProperty == null)
            {
                m_GenerateLightmapUVsProperty = serializedObject.FindProperty("m_GenerateLightmapUVs");
            }
            
            EditorGUILayout.PropertyField(m_GenerateLightmapUVsProperty, new GUIContent("Generate Lightmap UVs"));

            EditorGUILayout.Space();
            
            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}
