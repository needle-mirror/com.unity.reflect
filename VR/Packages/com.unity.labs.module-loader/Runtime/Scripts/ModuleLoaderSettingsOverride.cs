using System.Collections.Generic;
using UnityEngine;

namespace Unity.Labs.ModuleLoader
{
    [CreateAssetMenu(fileName = "ModuleSettings", menuName = "ModuleLoader/Settings Override")]
    public class ModuleLoaderSettingsOverride : ScriptableObject
    {
        [SerializeField]
        List<string> m_ExcludedTypes = new List<string>();

        public List<string> ExcludedTypes { get { return m_ExcludedTypes; } }
    }
}
