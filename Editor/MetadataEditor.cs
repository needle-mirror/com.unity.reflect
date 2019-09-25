using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEditor.Reflect
{
    [CustomEditor(typeof(Metadata))]
    public class MetadataEditor : Editor
    {
        static GUIStyle s_BoldFoldout;
            
        [Serializable]
        class ParameterGroup
        {
            public string name;
           
            public List<string> keys = new List<string>();
            public List<string> values = new List<string>();
            
            public bool visible;
        }

        List<ParameterGroup> m_ParameterGroups;
        
        public override void OnInspectorGUI()
        {
            var model = (Metadata) target;
            model.modelTag = (Metadata.Tag) EditorGUILayout.EnumPopup("Tag", model.modelTag);
            
            if (m_ParameterGroups == null)
            {
                m_ParameterGroups = new List<ParameterGroup>();
                foreach (var parameter in model.SortedByGroup())
                {
                    var group = new ParameterGroup { visible = true, name = parameter.Key, keys = parameter.Value.Keys.ToList(),
                        values = parameter.Value.Values.Select(p => p.value).ToList() };
                    
                    m_ParameterGroups.Add(group);
                }
            }

            if (s_BoldFoldout == null)
            {
                s_BoldFoldout = new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold };
            }

            foreach (var parameterGroups in m_ParameterGroups)
            {
                parameterGroups.visible = EditorGUILayout.Foldout(parameterGroups.visible, parameterGroups.name, true, s_BoldFoldout);
                if (parameterGroups.visible)
                {
                    EditorGUI.indentLevel++;
                    
                    for (int i = 0; i < parameterGroups.keys.Count; ++i)
                    {
                        EditorGUILayout.TextField(parameterGroups.keys[i], parameterGroups.values[i]);
                    }
                    
                    EditorGUI.indentLevel--;
                }
            }
        }
    }
}
