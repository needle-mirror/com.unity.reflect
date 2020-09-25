using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;

namespace UnityEngine.Reflect.Pipeline
{
    [CustomEditor(typeof(PipelineAsset))]
    public class PipelineAssetEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel); 

            var nodeProperties = serializedObject.FindProperty("m_Nodes");

            for (var i = 0; i < nodeProperties.arraySize; ++i)
            {
                var property = nodeProperties.GetArrayElementAtIndex(i);
                
                var displayName = property.managedReferenceFullTypename;
                displayName = displayName.Substring(displayName.LastIndexOf('.') + 1); // TODO Use Regex
                displayName = displayName.Replace("Node", string.Empty);

                displayName = ObjectNames.NicifyVariableName(displayName);

                var properties = GetVisibleChildren(property);

                if (properties.Any())
                {
                    property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, displayName);
                    
                    if (property.isExpanded)
                    {
                        ++EditorGUI.indentLevel;

                        foreach (var serializedProperty in properties)
                        {
                            EditorGUILayout.PropertyField(serializedProperty);
                        }

                        --EditorGUI.indentLevel;
                    }
                }
                
                serializedObject.ApplyModifiedProperties();
            }
        }

        static bool CanDisplayProperty(SerializedProperty serializedProperty)
        {
            // TODO Investigate better solution for that
            var type = serializedProperty.type;

            var hide = type.EndsWith("Input") || type.EndsWith("Output") || type.EndsWith("Param");

            return !hide;
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

                    if (CanDisplayProperty(currentProperty))
                        yield return currentProperty;
                }
                while (currentProperty.NextVisible(false));
            }
        }
    }
}
