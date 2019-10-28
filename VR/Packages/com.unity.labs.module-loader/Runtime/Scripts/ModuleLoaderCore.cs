using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Labs.Utils;
using Unity.Labs.Utils.GUI;
using Unity.Labs.Utils.Internal;
using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.SceneManagement;
#endif

namespace Unity.Labs.ModuleLoader
{
    [ScriptableSettingsPath(SettingsPath)]
    public class ModuleLoaderCore : ScriptableSettings<ModuleLoaderCore>
    {
        [Flags]
        internal enum OverrideModes
        {
            Editor =   1 << 0,
            PlayMode = 1 << 1,
            Player =   1 << 2
        }

        [Flags]
        internal enum OverridePlatforms
        {
            Windows = 1 << 0,
            Mac     = 1 << 1,
            Linux   = 1 << 2,
            WebGL   = 1 << 3,
            Android = 1 << 4,
            IOS     = 1 << 5,
            Lumin   = 1 << 6,
            WSAPlayer = 1 << 7
        }

        [Serializable]
        internal class PlatformOverride
        {
#pragma warning disable 649
            [SerializeField]
            ModuleLoaderSettingsOverride m_Settings;

            [FlagsProperty]
            [SerializeField]
            OverridePlatforms m_Platforms;

            [FlagsProperty]
            [SerializeField]
            OverrideModes m_Modes;
#pragma warning restore 649

            public ModuleLoaderSettingsOverride settings { get { return m_Settings; } }
            public OverridePlatforms platforms { get { return m_Platforms; } }
            public OverrideModes modes { get { return m_Modes; } }
        }

        public const string UserSettingsFolder = "ModuleLoaderUserSettings";
        public const string SettingsPath = "ModuleLoaderSettings";

        const string k_ModuleParentName = "___MODULES___";

        GameObject m_ModuleParent;

#pragma warning disable 649
        [SerializeField]
        List<string> m_ExcludedTypes = new List<string>();

        [SerializeField]
        ModuleLoaderSettingsOverride m_SettingsOverride;

        [SerializeField]
        PlatformOverride[] m_PlatformOverrides = new PlatformOverride[0];
#pragma warning restore 649

        readonly List<IModule> m_Modules = new List<IModule>();

        internal readonly List<IModule> moduleUnloads = new List<IModule>();
        internal readonly List<IModuleBehaviorCallbacks> behaviorCallbackModules = new List<IModuleBehaviorCallbacks>();
        internal readonly List<IModuleSceneCallbacks> sceneCallbackModules = new List<IModuleSceneCallbacks>();
        internal readonly List<IModuleBuildCallbacks> buildCallbackModules = new List<IModuleBuildCallbacks>();
        internal readonly List<IModuleAssetCallbacks> assetCallbackModules = new List<IModuleAssetCallbacks>();

#if UNITY_EDITOR
        public static bool isSwitchingScenes { get; private set; }
        public static bool isBuilding { get; private set; }
        public static bool blockSceneCallbacks { private get; set; }
#endif
        public static bool isUnloadingModules { get; private set; }

        public List<IModule> modules { get { return m_Modules; } }

        internal List<string> excludedTypes
        {
            get
            {
                if (currentOverride != null)
                    return currentOverride.ExcludedTypes;

                return m_ExcludedTypes;
            }
        }

        internal ModuleLoaderSettingsOverride currentOverride
        {
            get
            {
                if (m_SettingsOverride)
                    return m_SettingsOverride;

                var currentPlatform = GetCurrentPlatform();
                foreach (var platformOverride in m_PlatformOverrides)
                {
                    if ((platformOverride.platforms & currentPlatform) == 0)
                        continue;

                    var modes = platformOverride.modes;
                    if (!CheckCurrentMode(modes))
                        continue;

                    return platformOverride.settings;
                }

                return null;
            }
        }

        public event Action ModulesLoaded;

        public bool ModulesAreLoaded { get; private set; }

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly List<Type> k_ModuleTypes = new List<Type>();

        internal static bool CheckCurrentMode(OverrideModes modes)
        {
#if UNITY_EDITOR
            if (Application.isPlaying && (modes & OverrideModes.PlayMode) != 0)
                return true;

            if (!Application.isPlaying && (modes & OverrideModes.Editor) != 0)
                return true;
#else
            if ((modes & OverrideModes.Player) != 0)
                return true;
#endif

            return false;
        }

        internal static OverridePlatforms GetCurrentPlatform()
        {
#if UNITY_EDITOR
            var activeBuildTarget = EditorUserBuildSettings.activeBuildTarget;
            switch (activeBuildTarget)
            {
                case BuildTarget.StandaloneOSX:
                    return OverridePlatforms.Mac;
                case BuildTarget.StandaloneWindows:
                    return OverridePlatforms.Windows;
                case BuildTarget.iOS:
                    return OverridePlatforms.IOS;
                case BuildTarget.Android:
                    return OverridePlatforms.Android;
                case BuildTarget.StandaloneWindows64:
                    return OverridePlatforms.Windows;
                case BuildTarget.WebGL:
                    return OverridePlatforms.WebGL;
                // BuildTarget.StandaloneLinux and BuildTarget.StandaloneLinuxUniversal removed in 2019.2
#if !UNITY_2019_2_OR_NEWER
                case BuildTarget.StandaloneLinux:
                case BuildTarget.StandaloneLinuxUniversal:
#endif
                case BuildTarget.StandaloneLinux64:
                    return OverridePlatforms.Linux;
                case BuildTarget.Lumin:
                    return OverridePlatforms.Lumin;
                case BuildTarget.WSAPlayer:
                    return OverridePlatforms.WSAPlayer;
                default:
                    throw new ArgumentOutOfRangeException("activeBuildTarget", activeBuildTarget, "Unknown platform");
            }
#else
            var platform = Application.platform;
            switch (platform)
            {
                case RuntimePlatform.OSXEditor:
                    return OverridePlatforms.Mac;
                case RuntimePlatform.OSXPlayer:
                    return OverridePlatforms.Mac;
                case RuntimePlatform.WindowsPlayer:
                    return OverridePlatforms.Windows;
                case RuntimePlatform.WindowsEditor:
                    return OverridePlatforms.Windows;
                case RuntimePlatform.IPhonePlayer:
                    return OverridePlatforms.IOS;
                case RuntimePlatform.Android:
                    return OverridePlatforms.Android;
                case RuntimePlatform.LinuxPlayer:
                    return OverridePlatforms.Linux;
                case RuntimePlatform.LinuxEditor:
                    return OverridePlatforms.Linux;
                case RuntimePlatform.WebGLPlayer:
                    return OverridePlatforms.WebGL;
                case RuntimePlatform.Lumin:
                    return OverridePlatforms.Lumin;
                case RuntimePlatform.WSAPlayerX64:
                case RuntimePlatform.WSAPlayerARM:
                case RuntimePlatform.WSAPlayerX86:
                    return OverridePlatforms.WSAPlayer;
                default:
                    throw new ArgumentOutOfRangeException("Application.platform", platform, "Unknown platform");
            }
#endif
        }

        protected override void OnLoaded()
        {
            // On first import, due to creation of ScriptableSettings assets, OnLoaded is called twice in a row
            UnloadModules();
#if UNITY_EDITOR
            // Ensure destruction of behavior modules on recompile and enter/exit play mode in case hide flags do not
            CompilationPipeline.assemblyCompilationStarted += CompilationPipelineOnAssemblyCompilationStarted;

            isSwitchingScenes = false;
            var tags = TagManager.GetRequiredTags();
            var layers = TagManager.GetRequiredLayers();

            foreach (var tag in tags)
            {
                TagManager.AddTag(tag);
            }

            foreach (var layer in layers)
            {
                TagManager.AddLayer(layer);
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorSceneManager.sceneOpening += OnSceneOpening;
#if UNITY_2018_2_OR_NEWER
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
#endif
            EditorSceneManager.newSceneCreated += OnNewSceneCreated;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.update += EditorUpdate;
#endif
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            SceneManager.activeSceneChanged += OnActiveSceneChanged;

            if (!Application.isPlaying)
                LoadModules();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorSceneManager.sceneOpening -= OnSceneOpening;
#if UNITY_2018_2_OR_NEWER
            EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChanged;
#endif
            EditorSceneManager.newSceneCreated -= OnNewSceneCreated;
            EditorSceneManager.sceneOpened -= OnSceneOpened;
            EditorApplication.update -= EditorUpdate;
#endif
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            UnloadModules();
        }

        public void ReloadModules()
        {
            UnloadModules();
            LoadModules();
        }

#if UNITY_EDITOR
        void CompilationPipelineOnAssemblyCompilationStarted(string assemblyPath)
        {
            UnloadModules();
        }

        static void EditorUpdate()
        {
            // Build errors and canceled builds will skip OnPostProcessBuild and may even not recompile scripts,
            // so we have to check the isBuilding state here.
            if (isBuilding)
            {
                isBuilding = false;
                if (s_Instance != null)
                    s_Instance.ReloadModules();
            }
        }

        void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            isSwitchingScenes = false;

            if (blockSceneCallbacks)
                return;

            if (isBuilding)
                return;

            if (mode == OpenSceneMode.Single)
                ReloadModules();

            foreach (var module in sceneCallbackModules)
            {
                try
                {
                    module.OnSceneOpened(scene, mode);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        void OnSceneOpening(string path, OpenSceneMode mode)
        {
            isSwitchingScenes = true;

            if (blockSceneCallbacks)
                return;

            if (isBuilding)
                return;

            foreach (var module in sceneCallbackModules)
            {
                try
                {
                    module.OnSceneOpening(path, mode);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (mode == OpenSceneMode.Single)
                UnloadModules();
        }

        void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode)
        {
            ReloadModules();

            foreach (var module in sceneCallbackModules)
            {
                try
                {
                    module.OnNewSceneCreated(scene, setup, mode);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        static void OnPlayModeStateChanged(PlayModeStateChange playModeStateChange)
        {
            if (s_Instance == null)
                return;

            switch (playModeStateChange)
            {
                case PlayModeStateChange.ExitingEditMode:
                    s_Instance.UnloadModules();
                    break;
                case PlayModeStateChange.EnteredEditMode:
                    s_Instance.ReloadModules();
                    break;
            }
        }

        public static void OnPreprocessBuild()
        {
            isBuilding = true;

            if (s_Instance == null)
                return;

            foreach (var module in s_Instance.buildCallbackModules)
            {
                try
                {
                    module.OnPreprocessBuild();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public static void OnProcessScene(Scene scene)
        {
            if (s_Instance == null)
                return;

            foreach (var module in s_Instance.buildCallbackModules)
            {
                try
                {
                    module.OnProcessScene(scene);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public static void OnPostprocessBuild()
        {
            isBuilding = false;

            if (s_Instance == null)
                return;

            foreach (var module in s_Instance.buildCallbackModules)
            {
                try
                {
                    module.OnPostprocessBuild();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public static string[] OnWillSaveAssets(string[] paths)
        {
            if (s_Instance == null)
                return paths;

            return s_Instance.assetCallbackModules.Aggregate(paths, (current, module) =>
            {
                var newPaths = paths;
                try
                {
                    newPaths = module.OnWillSaveAssets(current);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return newPaths;
            });
        }

        public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options)
        {
            if (s_Instance == null)
                return AssetDeleteResult.DidDelete;

            return s_Instance.assetCallbackModules.Aggregate(AssetDeleteResult.DidNotDelete, (current, module) =>
            {
                try
                {
                    current |= module.OnWillDeleteAsset(path, options);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }

                return current;
            });
        }
#endif

        public void OnBehaviorAwake()
        {
            foreach (var module in behaviorCallbackModules)
            {
                try
                {
                    module.OnBehaviorAwake();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            // We inject functionality again when the app is started in case there are any providers
            // that didn't exist before scene analysis
            var fiModule = GetModule<FunctionalityInjectionModule>();
            if (fiModule != null)
                InjectFunctionalityInModules(fiModule.activeIsland);
        }

        public void OnBehaviorEnable()
        {
            foreach (var module in behaviorCallbackModules)
            {
                try
                {
                    module.OnBehaviorEnable();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void OnBehaviorStart()
        {
            foreach (var module in behaviorCallbackModules)
            {
                try
                {
                    module.OnBehaviorStart();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void OnBehaviorUpdate()
        {
            foreach (var module in behaviorCallbackModules)
            {
                try
                {
                    module.OnBehaviorUpdate();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void OnBehaviorDisable()
        {
            foreach (var module in behaviorCallbackModules)
            {
                try
                {
                    module.OnBehaviorDisable();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void OnBehaviorDestroy()
        {
            foreach (var module in behaviorCallbackModules)
            {
                try
                {
                    module.OnBehaviorDestroy();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public void InjectFunctionalityInModules(FunctionalityIsland island)
        {
            foreach (var module in m_Modules)
            {
                island.InjectFunctionalitySingle(module);
            }
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            foreach (var module in sceneCallbackModules)
            {
                try
                {
                    module.OnSceneLoaded(scene, mode);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        void OnSceneUnloaded(Scene scene)
        {
            foreach (var module in sceneCallbackModules)
            {
                try
                {
                    module.OnSceneUnloaded(scene);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            UnloadModules();
        }

        void OnActiveSceneChanged(Scene oldScene, Scene newScene)
        {
            foreach (var module in sceneCallbackModules)
            {
                try
                {
                    module.OnActiveSceneChanged(oldScene, newScene);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }

        public static void GetModuleTypes(List<Type> moduleTypes)
        {
            typeof(IModule).GetImplementationsOfInterface(moduleTypes);
        }

        public void LoadModules()
        {
            k_ModuleTypes.Clear();
            GetModuleTypes(k_ModuleTypes);
            k_ModuleTypes.RemoveAll(type => excludedTypes.Contains(type.FullName));
            LoadModulesWithTypes(k_ModuleTypes);
        }

        public void UnloadModules()
        {
            isUnloadingModules = true;
            foreach (var module in moduleUnloads)
            {
                try
                {
                    module.UnloadModule();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            foreach (var module in moduleUnloads)
            {
                var behavior = module as MonoBehaviour;
                if (behavior != null)
                    DestroyImmediate(behavior.gameObject);
            }

            ClearModules();
            isUnloadingModules = false;

            // Destroy immediate so we don't end up destroying the parent after modules are loaded
            DestroyImmediate(GetModuleParent());

            ModulesAreLoaded = false;
        }

        void ClearModules()
        {
            m_Modules.Clear();
            moduleUnloads.Clear();
            behaviorCallbackModules.Clear();
            sceneCallbackModules.Clear();
            buildCallbackModules.Clear();
            assetCallbackModules.Clear();
        }

        public T GetModule<T>() where T : IModule
        {
            foreach (var module in m_Modules)
            {
                if (module is T)
                    return (T)module;
            }

            return default(T);
        }

        internal void LoadModulesWithTypes(List<Type> moduleTypes)
        {
            var moduleParent = GetModuleParent();
            var hideFlags = ModuleLoaderDebugSettings.instance.moduleHideFlags;
            moduleParent.hideFlags = hideFlags;
            var moduleParentTransform = moduleParent.transform;

            ClearModules();
            var moduleOrder = new Dictionary<IModule, int>();
            var moduleUnloadOrder = new Dictionary<IModule, int>();
            var behaviorOrder = new Dictionary<IModuleBehaviorCallbacks, int>();
            var sceneOrder = new Dictionary<IModuleSceneCallbacks, int>();
            var buildOrder = new Dictionary<IModuleBuildCallbacks, int>();
            var assetOrder = new Dictionary<IModuleAssetCallbacks, int>();
            var gameObjects = new List<GameObject>();
            foreach (var moduleType in moduleTypes)
            {
                IModule module;
                if (typeof(ScriptableSettingsBase).IsAssignableFrom(moduleType))
                {
                    module = (IModule)GetInstanceByType(moduleType);
                }
                else if (typeof(MonoBehaviour).IsAssignableFrom(moduleType))
                {
                    // Even without HideFlags, these objects won't show up in the hierarchy or get methods called on it
                    // in play mode because they are created too early
                    var go = new GameObject(moduleType.Name);
                    go.SetActive(false);
                    go.hideFlags = hideFlags;
                    go.transform.SetParent(moduleParentTransform);
                    module = (IModule)go.AddComponent(moduleType);
                    gameObjects.Add(go);
                }
                else
                {
                    module = (IModule)Activator.CreateInstance(moduleType);
                }

                if (module == null)
                {
                    Debug.LogError("Could not load module of type " + moduleType);
                    continue;
                }

                m_Modules.Add(module);
                moduleOrder[module] = 0;
                moduleUnloads.Add(module);
                moduleUnloadOrder[module] = 0;

                var behaviorModule = module as IModuleBehaviorCallbacks;
                if (behaviorModule != null)
                {
                    behaviorCallbackModules.Add(behaviorModule);
                    behaviorOrder[behaviorModule] = 0;
                }

                var sceneModule = module as IModuleSceneCallbacks;
                if (sceneModule != null)
                {
                    sceneCallbackModules.Add(sceneModule);
                    sceneOrder[sceneModule] = 0;
                }

                var buildModule = module as IModuleBuildCallbacks;
                if (buildModule != null)
                {
                    buildCallbackModules.Add(buildModule);
                    buildOrder[buildModule] = 0;
                }

                var assetModule = module as IModuleAssetCallbacks;
                if (assetModule != null)
                {
                    assetCallbackModules.Add(assetModule);
                    assetOrder[assetModule] = 0;
                }

                var attributes = (ModuleOrderAttribute[])moduleType.GetCustomAttributes(typeof(ModuleOrderAttribute), true);
                foreach (var attribute in attributes)
                {
                    if (attribute is ModuleBehaviorCallbackOrderAttribute)
                    {
                        if (behaviorModule != null)
                            behaviorOrder[behaviorModule] = attribute.order;
                    }
                    else if (attribute is ModuleSceneCallbackOrderAttribute)
                    {
                        if (sceneModule != null)
                            sceneOrder[sceneModule] = attribute.order;
                    }
                    else if (attribute is ModuleBuildCallbackOrderAttribute)
                    {
                        if (buildModule != null)
                            buildOrder[buildModule] = attribute.order;
                    }
                    else if (attribute is ModuleAssetCallbackOrderAttribute)
                    {
                        if (assetModule != null)
                            assetOrder[assetModule] = attribute.order;
                    }
                    else if (attribute is ModuleUnloadOrderAttribute)
                    {
                        moduleUnloadOrder[module] = attribute.order;
                    }
                    else
                    {
                        moduleOrder[module] = attribute.order;
                    }
                }
            }

            m_Modules.Sort((a, b) => moduleOrder[a].CompareTo(moduleOrder[b]));
            moduleUnloads.Sort((a, b) => moduleUnloadOrder[a].CompareTo(moduleUnloadOrder[b]));
            behaviorCallbackModules.Sort((a, b) => behaviorOrder[a].CompareTo(behaviorOrder[b]));
            sceneCallbackModules.Sort((a, b) => sceneOrder[a].CompareTo(sceneOrder[b]));
            buildCallbackModules.Sort((a, b) => buildOrder[a].CompareTo(buildOrder[b]));
            assetCallbackModules.Sort((a, b) => assetOrder[a].CompareTo(assetOrder[b]));

            var interfaces = new List<Type>();
            var dependencyArg = new object[1];
            foreach (var module in m_Modules)
            {
                var type = module.GetType();
                interfaces.Clear();
                type.GetGenericInterfaces(typeof(IModuleDependency<>), interfaces);
                foreach (var @interface in interfaces)
                {
                    foreach (var dependency in m_Modules)
                    {
                        var dependencyType = dependency.GetType();
                        if (dependencyType.IsAssignableFrom(@interface.GetGenericArguments()[0]))
                        {
                            dependencyArg[0] = dependency;
                            @interface.GetMethod("ConnectDependency").Invoke(module, dependencyArg);
                        }
                    }
                }
            }

            var fiModule = GetModule<FunctionalityInjectionModule>();
            if (fiModule != null)
            {
                var providers = new List<IFunctionalityProvider>();
                foreach (var module in m_Modules)
                {
                    var item = module as IFunctionalityProvider;
                    if (item != null)
                        providers.Add(item);
                }

                fiModule.PreLoad();
                foreach (var island in fiModule.islands)
                {
                    island.AddProviders(providers);
                }

                var activeIsland = fiModule.activeIsland;
                foreach (var module in m_Modules)
                {
                    activeIsland.InjectFunctionalitySingle(module);
                }
            }

            foreach (var gameObject in gameObjects)
            {
                gameObject.SetActive(true);
            }

            foreach (var module in m_Modules)
            {
                try
                {
                    module.LoadModule();
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }

            if (ModuleLoaderDebugSettings.instance.functionalityInjectionModuleLogging && fiModule != null)
                Debug.Log(fiModule.PrintStatus());

            if (ModulesLoaded != null)
                ModulesLoaded();

            ModulesAreLoaded = true;
        }

        public GameObject GetModuleParent()
        {
            if (m_ModuleParent)
                return m_ModuleParent;

            m_ModuleParent = GameObject.Find(k_ModuleParentName);
            if (!m_ModuleParent)
                m_ModuleParent = new GameObject(k_ModuleParentName);

            return m_ModuleParent;
        }
    }
}
