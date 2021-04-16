using UnityEditor;

namespace UnityEngine.Reflect.Pipeline
{
    [CustomEditor(typeof(ReflectPipeline), true)]
    public class ReflectPipelineEditor : Editor
    {
        SerializedProperty m_PipelineAssetProperty;

        void OnEnable()
        {
            m_PipelineAssetProperty = serializedObject.FindProperty("pipelineAsset");
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var pipelineAsset = m_PipelineAssetProperty.objectReferenceValue;
            
            if (pipelineAsset == null)
            {
                EditorGUILayout.HelpBox(new GUIContent("No PipelineAsset assigned"));
                return;
            }
            
            var editor = CreateEditorWithContext(new[] { pipelineAsset }, target);

            EditorGUILayout.Space();

            editor.OnInspectorGUI();
            
            if (GUI.changed)
            {
                // If the component is part of a Prefab, we need to manually set the Scene dirty so ExposedReferences
                // are properly saved.
                if (PrefabUtility.IsPartOfPrefabInstance(serializedObject.targetObject))
                {
                    EditorUtility.SetDirty(serializedObject.targetObject);
                }
            }
        }
    }
}
