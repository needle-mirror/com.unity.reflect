using UnityEditor;

namespace Unity.Labs.ModuleLoader
{
    [CustomEditor(typeof(FunctionalityInjectionModule))]
    public class FunctionalityInjectionModuleEditor : Editor
    {
        FunctionalityInjectionModule m_FIModule;
        bool m_ShowIslands = true;

        SerializedProperty m_DefaultIslandProperty;

        void OnEnable()
        {
            m_FIModule = (FunctionalityInjectionModule)target;

            m_DefaultIslandProperty = serializedObject.FindProperty("m_DefaultIsland");
        }

        public override void OnInspectorGUI()
        {
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                EditorGUILayout.PropertyField(m_DefaultIslandProperty);
                m_ShowIslands = EditorGUILayout.Foldout(m_ShowIslands, "Current Islands");
                if (m_ShowIslands)
                    DrawCurrentIslands();

                if (check.changed)
                    serializedObject.ApplyModifiedProperties();
            }
        }

        void DrawCurrentIslands()
        {
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledGroupScope(true))
                {
                    EditorGUILayout.ObjectField("Active Island", m_FIModule.activeIsland, typeof(FunctionalityIsland), false);
                }

                foreach (var island in m_FIModule.islands)
                {
                    if (!island)
                        continue;

                    island.foldoutState = EditorGUILayout.Foldout(island.foldoutState, island.name);
                    if (!island.foldoutState)
                        continue;

                    FunctionalityIslandEditor.DrawProviders(island);
                }
            }
        }
    }
}
