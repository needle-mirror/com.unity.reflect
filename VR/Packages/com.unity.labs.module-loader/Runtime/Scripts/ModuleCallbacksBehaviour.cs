using UnityEngine;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Runtime hooks for ModuleLoader.  One of these must be in any scene which depends on modules for it to function properly
    /// </summary>
    public class ModuleCallbacksBehaviour : MonoBehaviour
    {
        void Awake()
        {
            var moduleLoaderCore = ModuleLoaderCore.instance;
            if (Application.isPlaying)
                moduleLoaderCore.ReloadModules();

            moduleLoaderCore.OnBehaviorAwake();
        }

        void OnEnable() { ModuleLoaderCore.instance.OnBehaviorEnable(); }

        void Start() { ModuleLoaderCore.instance.OnBehaviorStart(); }

        void Update() { ModuleLoaderCore.instance.OnBehaviorUpdate(); }

        void OnDisable() { ModuleLoaderCore.instance.OnBehaviorDisable(); }

        void OnDestroy()
        {
            ModuleLoaderCore.instance.OnBehaviorDestroy();
            ModuleLoaderCore.instance.UnloadModules();
        }
    }
}
