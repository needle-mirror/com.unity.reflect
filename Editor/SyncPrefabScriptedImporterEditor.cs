using System;
using UnityEditor.AssetImporters;
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
            serializedObject.Update();
            
            if (m_MaterialRemapsProperty == null)
            {
                m_MaterialRemapsProperty = serializedObject.FindProperty("m_MaterialRemaps");
            }
            
            if (s_BoldStyle == null)
            {
                s_BoldStyle = new GUIStyle("Foldout") { fontStyle = FontStyle.Bold };
            }

            if (GUILayout.Button(new GUIContent("Extract Assets", "Generate editable assets from this SyncPrefab.")))
            {
                var assetPath = ((ScriptedImporter)target).assetPath;
                ReflectAssetsExtractorWindow.ShowWindow(AssetDatabase.LoadAssetAtPath<GameObject>(assetPath));
            }
            
            m_MaterialRemapsProperty.isExpanded = EditorGUILayout.Foldout(m_MaterialRemapsProperty.isExpanded , "Remapped Materials", s_BoldStyle);

            if (m_MaterialRemapsProperty.isExpanded)
            {
                var size = m_MaterialRemapsProperty.arraySize;

                for (int i = 0; i < size; ++i)
                {
                    EditorGUILayout.BeginHorizontal();

                    var item = m_MaterialRemapsProperty.GetArrayElementAtIndex(i);
                    EditorGUILayout.PropertyField(item.FindPropertyRelative("remappedMaterial"), new GUIContent(item.FindPropertyRelative("syncMaterialName").stringValue));

                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space();

            serializedObject.ApplyModifiedProperties();
            ApplyRevertGUI();
        }
    }
}
