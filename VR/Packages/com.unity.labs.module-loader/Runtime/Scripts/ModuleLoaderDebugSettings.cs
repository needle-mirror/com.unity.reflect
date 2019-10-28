using Unity.Labs.Utils;
using Unity.Labs.Utils.GUI;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Labs.ModuleLoader
{
    [ScriptableSettingsPath(ModuleLoaderCore.UserSettingsFolder)]
    public class ModuleLoaderDebugSettings : ScriptableSettings<ModuleLoaderDebugSettings>
    {
#pragma warning disable 649
        [SerializeField]
        bool m_FunctionalityInjectionModuleLogging;

        [FlagsProperty]
        [SerializeField]
        HideFlags m_ModuleHideFlags = HideFlags.HideAndDontSave;
#pragma warning restore 649

        public bool functionalityInjectionModuleLogging { get { return m_FunctionalityInjectionModuleLogging; } }

        public HideFlags moduleHideFlags { get { return m_ModuleHideFlags; } }

        void OnValidate()
        {
            if ((m_ModuleHideFlags | HideFlags.DontSave) != m_ModuleHideFlags)
                Debug.LogWarning("You must have at least HideFlags.DontSave in module hide flags");

            m_ModuleHideFlags |= HideFlags.DontSave;
        }

        public void SetModuleHideFlags(HideFlags newHideFlags)
        {
            m_ModuleHideFlags = newHideFlags;
            ModuleLoaderCore.instance.GetModuleParent().hideFlags = newHideFlags;
#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
#endif
        }
    }
}
