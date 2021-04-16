using System;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace UnityEditor.Reflect
{
    [CanEditMultipleObjects]
    [CustomEditor(typeof(SyncObjectScriptedImporter))]
    public class SyncObjectScriptedImporterEditor : ScriptedImporterEditor
    {
        SerializedProperty m_ImportLightsProperty;

        public override void OnInspectorGUI()
        {
            if (m_ImportLightsProperty == null)
            {
                m_ImportLightsProperty = serializedObject.FindProperty("m_ImportLights");
            }
            
            EditorGUILayout.PropertyField(m_ImportLightsProperty, new GUIContent("Import Lights"));

            EditorGUILayout.Space();
            
            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}
