using System;
using System.Collections.Generic;
using System.Text;
using Unity.Labs.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Labs.ModuleLoader
{
    [ImmortalModule]
    [ScriptableSettingsPath(ModuleLoaderCore.SettingsPath)]
    public class FunctionalityInjectionModule : ScriptableSettings<FunctionalityInjectionModule>, IModuleBehaviorCallbacks
    {
        const string k_DefaultIslandPath = "Assets/Default Island.asset";

#pragma warning disable 649
        [SerializeField]
        FunctionalityIsland m_DefaultIsland;
#pragma warning restore 649

        bool m_Loaded;

        readonly HashSet<FunctionalityIsland> m_Islands = new HashSet<FunctionalityIsland>();

        public HashSet<FunctionalityIsland> islands { get { return m_Islands; } }

        public FunctionalityIsland defaultIsland { get { return m_DefaultIsland; } }
        public FunctionalityIsland activeIsland { get; private set; }

        public event Action<FunctionalityIsland> activeIslandChanged;

        // We have to set up islands before the other modules get loaded so they can use FI
        public void PreLoad()
        {
            if (!m_DefaultIsland)
            {
#if UNITY_EDITOR
                Debug.Log("You must set up a default functionality island. One has been created for you.");
                m_DefaultIsland = CreateInstance<FunctionalityIsland>();
                AssetDatabase.CreateAsset(m_DefaultIsland, k_DefaultIslandPath);
                AssetDatabase.SaveAssets();
                EditorUtility.SetDirty(this);
#else
                Debug.LogError("You must set up a default functionality island.");
                return;
#endif
            }

            m_DefaultIsland.Setup();
            m_Islands.Add(m_DefaultIsland);
            activeIsland = m_DefaultIsland;
        }

        public void LoadModule()
        {
            m_Loaded = true;
        }

        public void UnloadModule()
        {
            foreach (var island in m_Islands)
            {
                if (island)
                    island.Unload();
                else
                    Debug.LogError("Encountered a null island during Unload--this should only happen if you recently deleted a Functionality Island asset");
            }

            activeIsland = null;
            m_Islands.Clear();
            m_Loaded = false;
        }

        public void AddIsland(FunctionalityIsland island)
        {
            // Do not add the default island, as it will be added automatically
            if (island == m_DefaultIsland)
                return;

            Assert.IsFalse(m_Loaded, "It is too late to add a functionality island. All modules are loaded");
            Assert.IsNotNull(island, "Trying to add an island that is null");
            var wasAdded = m_Islands.Add(island);
            Assert.IsTrue(wasAdded, "Island has already been added");

            island.Setup();
        }

        public void SetActiveIsland(FunctionalityIsland island)
        {
            Assert.IsTrue(m_Islands.Contains(island), string.Format("Cannot set active island to {0}. It is not in the list of islands", island));
            activeIsland = island;

            if (activeIslandChanged != null)
                activeIslandChanged(island);
        }

        public string PrintStatus()
        {
            var sb = new StringBuilder();
            sb.Append(string.Format("Active Island: {0}\nCurrent Islands ({1}):\n", activeIsland.name, m_Islands.Count));
            foreach (var island in m_Islands)
            {
                sb.Append(island.PrintStatus());
            }

            return sb.ToString();
        }

        public void OnBehaviorAwake() {}

        public void OnBehaviorEnable() {}

        public void OnBehaviorStart() {}

        public void OnBehaviorUpdate() {}

        public void OnBehaviorDisable() {}

        public void OnBehaviorDestroy()
        {
            foreach (var island in m_Islands)
            {
                island.OnBehaviorDestroy();
            }
        }
    }
}
