using UnityEditor;

namespace UnityEngine.Reflect.Pipeline
{
    [CustomEditor(typeof(ReflectPipeline), true)]
    public class ReflectPipelineEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var pipelineProperty = serializedObject.FindProperty("pipelineAsset");

            if (pipelineProperty == null)
                return;

            if (pipelineProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(new GUIContent("No PipelineAsset assigned"));
                return;
            }
            
            var editor = CreateEditorWithContext(new[] { pipelineProperty.objectReferenceValue }, target);

            EditorGUILayout.Space();

            editor.OnInspectorGUI();
        }
    }
}
