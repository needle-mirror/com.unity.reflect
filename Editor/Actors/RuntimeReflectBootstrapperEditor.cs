using UnityEditor;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Actor
{
    [CustomEditor(typeof(RuntimeReflectBootstrapper))]
    public class RuntimeReflectBootstrapperEditor : Editor
    {
        Editor m_AssetEditor;
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            var assetProperty = serializedObject.FindProperty("Asset");

            if (assetProperty == null)
                return;

            if (assetProperty.objectReferenceValue == null)
            {
                EditorGUILayout.HelpBox(new GUIContent($"No {nameof(ActorSystemSetup)} asset assigned"));
                return;
            }

            if (!serializedObject.FindProperty(nameof(RuntimeReflectBootstrapper.EnableExperimentalActorSystem)).boolValue)
                return;

            CreateCachedEditorWithContext(new Object[] { assetProperty.objectReferenceValue }, target, typeof(ActorSystemSetupEditor), ref m_AssetEditor);

            EditorGUILayout.Space();
            m_AssetEditor.OnInspectorGUI();
            
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
