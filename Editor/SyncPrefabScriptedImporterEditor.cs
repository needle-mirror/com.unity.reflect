using System;
using UnityEditor.Experimental.AssetImporters;
using UnityEngine;

namespace UnityEditor.Reflect
{   
    [CustomEditor(typeof(SyncPrefabScriptedImporter))]
    public class SyncPrefabScriptedImporterEditor : ScriptedImporterEditor
    {
        SerializedProperty m_MaterialRemapsProperty;
        static GUIStyle s_BoldStyle;

        public override void OnInspectorGUI()
        {
            if (m_MaterialRemapsProperty == null)
            {
                m_MaterialRemapsProperty = serializedObject.FindProperty("m_MaterialRemaps");
            }
            
            if (s_BoldStyle == null)
            {
                s_BoldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            }
            
            EditorGUILayout.LabelField("Remapped Materials", s_BoldStyle);

            var size = m_MaterialRemapsProperty.arraySize;

            for (int i = 0; i < size; ++i)
            {
                EditorGUILayout.BeginHorizontal();

                var item = m_MaterialRemapsProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(item.FindPropertyRelative("remappedMaterial"), new GUIContent(item.FindPropertyRelative("syncMaterialName").stringValue));
                
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.Space();
            ApplyRevertGUI();
        }
    }
}
