using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Reflect.Actor
{
    [CustomEditor(typeof(ActorSystemSetup))]
    public class ActorSystemSetupEditor : Editor
    {
        bool m_ShowDefaultInspector;

        GUIStyle m_FoldoutStyle;

        public override void OnInspectorGUI()
        {
            m_ShowDefaultInspector = EditorGUILayout.Toggle(new GUIContent("Display Default Inspector"), m_ShowDefaultInspector);
            if (m_ShowDefaultInspector)
            {
                base.OnInspectorGUI();
                return;
            }
            
            EditorGUILayout.LabelField("Asset Settings", EditorStyles.boldLabel);
            
            var setupsProp = serializedObject.FindProperty($"{nameof(ActorSystemSetup.ActorSetups)}");
            
            ++EditorGUI.indentLevel;
            for (var i = 0; i < setupsProp.arraySize; ++i)
            {
                var setupProp = setupsProp.GetArrayElementAtIndex(i);

                var displayNameProp = setupProp.FindPropertyRelative($"{nameof(ActorSetup.DisplayName)}").stringValue;
                var settingsProp = setupProp.FindPropertyRelative($"{nameof(ActorSetup.Settings)}");

                if (settingsProp.managedReferenceFullTypename.Contains(nameof(ActorSettings)))
                    continue;

                var properties = GetVisibleChildren(settingsProp);

                if (properties.Any())
                {
                    CreateFoldoutStyle();
                    settingsProp.isExpanded = EditorGUILayout.Foldout(settingsProp.isExpanded, displayNameProp, m_FoldoutStyle);
                    
                    if (settingsProp.isExpanded)
                    {
                        ++EditorGUI.indentLevel;
                        foreach (var serializedProperty in properties)
                            EditorGUILayout.PropertyField(serializedProperty);
                        --EditorGUI.indentLevel;
                    }
                }
            }
            --EditorGUI.indentLevel;
                
            serializedObject.ApplyModifiedProperties();
        }

        
        static IEnumerable<SerializedProperty> GetVisibleChildren(SerializedProperty serializedProperty)
        {
            var currentProperty = serializedProperty.Copy();
            var nextSiblingProperty = serializedProperty.Copy();
            {
                nextSiblingProperty.NextVisible(false);
            }
 
            if (currentProperty.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                        break;
                    yield return currentProperty;
                }
                while (currentProperty.NextVisible(false));
            }
        }

        void CreateFoldoutStyle()
        {
            if (m_FoldoutStyle == null)
            {
                m_FoldoutStyle = new GUIStyle(EditorStyles.foldout);
                m_FoldoutStyle.fontStyle = FontStyle.Bold; 
            }
        }
    }
}
