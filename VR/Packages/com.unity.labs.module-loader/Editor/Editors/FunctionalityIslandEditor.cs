using Unity.Labs.Utils;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.ModuleLoader
{
    [CustomEditor(typeof(FunctionalityIsland))]
    public class FunctionalityIslandEditor : Editor
    {
        static readonly GUIContent k_ShowAllProviderTypesContent = new GUIContent("Show All Provider Types", "Show all provider types in the left column, even those which do not have more than one implementation");
        internal static bool ShowAllProviderTypes;

        [SerializeField]
        bool m_ShowAllProviderTypes;

        FunctionalityIsland m_Island;
        bool m_ShowProviders = true;

        void OnEnable()
        {
            m_Island = (FunctionalityIsland)target;
            if (ShowAllProviderTypes)
                m_ShowAllProviderTypes = true;
        }

        public override void OnInspectorGUI()
        {
            m_ShowAllProviderTypes = EditorGUILayout.Toggle(k_ShowAllProviderTypesContent, m_ShowAllProviderTypes);
            ShowAllProviderTypes = m_ShowAllProviderTypes;
            DrawDefaultInspector();
            m_ShowProviders = EditorGUILayout.Foldout(m_ShowProviders, "Current Providers");
            if (m_ShowProviders)
                DrawProviders(m_Island);
        }

        public static void DrawProviders(FunctionalityIsland island)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (var row in island.providers)
                    {
                        var provider = row.Value;
                        var providerType = row.Key.GetNameWithGenericArguments();
                        var unityObject = provider as UnityObject;
                        if (unityObject)
                            EditorGUILayout.ObjectField(providerType, unityObject, typeof(UnityObject), true);
                        else
                            EditorGUILayout.LabelField(providerType, row.Value.GetType().GetNameWithGenericArguments());
                    }
                }
            }
        }
    }
}
