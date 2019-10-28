using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Labs.Utils;
using Unity.Labs.Utils.GUI;
using Unity.Labs.Utils.Internal;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;

namespace Unity.Labs.ModuleLoader
{
    [CreateAssetMenu(fileName = "Island", menuName = "ModuleLoader/Functionality Island")]
    public class FunctionalityIsland : ScriptableObject, ISerializationCallbackReceiver, IProvidesFunctionalityInjection
    {
        [Serializable]
        public class DefaultProvider
        {
#pragma warning disable 649
            [SerializeField]
            string m_ProviderTypeName;

            [SerializeField]
            string m_DefaultProviderTypeName;

            [SerializeField]
            GameObject m_DefaultProviderPrefab;
#pragma warning restore 649

            Type m_ProviderType;
            Type m_DefaultProviderType;

            public string providerTypeName { get { return m_ProviderTypeName; } }
            public string defaultProviderTypeName { get { return m_DefaultProviderTypeName; } }
            public GameObject defaultProviderPrefab { get { return m_DefaultProviderPrefab; } }

            public Type providerType
            {
                get
                {
                    if (m_ProviderType != null)
                        return m_ProviderType;

                    m_ProviderType = ReflectionUtils.FindType(t => t.FullName == m_ProviderTypeName);
                    return m_ProviderType;
                }
                internal set
                {
                    m_ProviderType = value;
                }
            }

            public Type defaultProviderType
            {
                get
                {
                    if (m_DefaultProviderType != null)
                        return m_DefaultProviderType;

                    m_DefaultProviderType = ReflectionUtils.FindType(t => t.FullName == m_DefaultProviderTypeName);
                    return m_DefaultProviderType;
                }
                internal set
                {
                    m_DefaultProviderType = value;
                }
            }
        }

        [Serializable]
        internal class PlatformOverride
        {
#pragma warning disable 649
            [FlagsProperty]
            [SerializeField]
            ModuleLoaderCore.OverridePlatforms m_Platforms;

            [FlagsProperty]
            [SerializeField]
            ModuleLoaderCore.OverrideModes m_Modes;

            [SerializeField]
            DefaultProvider[] m_DefaultProviders;
#pragma warning restore 649

            public ModuleLoaderCore.OverridePlatforms platforms { get { return m_Platforms; } }
            public ModuleLoaderCore.OverrideModes modes { get { return m_Modes; } }
            public DefaultProvider[] defaultProviders { get { return m_DefaultProviders; } }
        }

        public const string SetupDefaultProvidersProfilerLabel = "FunctionalityIsland.SetupDefaultProviders()";
        public const string InjectFunctionalityProfilerLabel = "FunctionalityIsland.InjectFunctionality()";

#pragma warning disable 649
        [SerializeField]
        DefaultProvider[] m_DefaultProviders;

        [SerializeField]
        PlatformOverride[] m_PlatformOverrides;
#pragma warning restore 649

        DefaultProvider[] m_EffectiveDefaultProviders;

        // Allows us to check whether the island is included in the list of islands in the FI module
        bool m_Setup;

        readonly Dictionary<Type, IFunctionalityProvider> m_Providers = new Dictionary<Type, IFunctionalityProvider>();
        readonly HashSet<IFunctionalityProvider> m_UniqueProviders = new HashSet<IFunctionalityProvider>();

        public Dictionary<Type, IFunctionalityProvider> providers { get { return m_Providers; } }
        public HashSet<IFunctionalityProvider> uniqueProviders { get { return m_UniqueProviders; } }
        public DefaultProvider[] defaultProviders { get { return m_DefaultProviders; } }

#if UNITY_EDITOR
        internal bool foldoutState { get; set; }
#endif

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        static readonly HashSet<Type> k_UniqueTypes = new HashSet<Type>();
        static readonly List<Type> k_SubscriberInterfaces = new List<Type>();
        static readonly List<Type> k_Implementors = new List<Type>();
        static readonly HashSet<string> k_DuplicateProviderTypes = new HashSet<string>();
        static readonly HashSet<ModuleLoaderCore.OverridePlatforms> k_DuplicatePlatforms = new HashSet<ModuleLoaderCore.OverridePlatforms>();
        static readonly Dictionary<GameObject, GameObject> k_NewProviderPrefabInstances = new Dictionary<GameObject, GameObject>();
        static readonly List<Type> k_Types = new List<Type>();
        static readonly List<string> k_TypeNames = new List<string>();
        static readonly List<IFunctionalitySubscriber> k_Subscribers = new List<IFunctionalitySubscriber>();

        static void GetDefaultProviderTypesBatch(DefaultProvider[] providers)
        {
            k_TypeNames.Clear();
            k_Types.Clear();

            foreach (var row in providers)
            {
                k_TypeNames.Add(row.defaultProviderTypeName);
            }

            ReflectionUtils.FindTypesByFullNameBatch(k_TypeNames, k_Types);

            for(var i = 0; i < k_Types.Count; i++)
            {
                providers[i].defaultProviderType = k_Types[i];
            }
        }

        static void GetProviderTypesBatch(DefaultProvider[] providers)
        {
            k_TypeNames.Clear();
            k_Types.Clear();

            foreach (var row in providers)
            {
                k_TypeNames.Add(row.providerTypeName);
            }

            ReflectionUtils.FindTypesByFullNameBatch(k_TypeNames, k_Types);

            for(var i = 0; i < k_Types.Count; i++)
            {
                providers[i].providerType = k_Types[i];
            }
        }

        internal void Setup()
        {
            // Use a separate variable for the effective default providers so that overriding m_DefaultProviders does
            // not affect serialization
            m_EffectiveDefaultProviders = m_DefaultProviders;

            // Check if a platform override exists for the current platform
            if (m_PlatformOverrides != null)
            {
                var currentPlatform = ModuleLoaderCore.GetCurrentPlatform();
                foreach (var platformOverride in m_PlatformOverrides)
                {
                    if ((platformOverride.platforms & currentPlatform) == 0)
                        continue;

                    var modes = platformOverride.modes;
                    if (!ModuleLoaderCore.CheckCurrentMode(modes))
                        continue;

                    m_EffectiveDefaultProviders = platformOverride.defaultProviders;
                }
            }

            // By default, objects that this island injects on will use this island as the functionality injection provider
            AddProvider(typeof(FunctionalityIsland), this);
            m_Setup = true;
        }

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (m_DefaultProviders != null)
                ValidateProviderArray(m_DefaultProviders);

            if (m_PlatformOverrides != null)
            {
                k_DuplicatePlatforms.Clear();
                foreach (var platformOverride in m_PlatformOverrides)
                {
                    var platforms = platformOverride.platforms;
                    ValidateProviderArray(platformOverride.defaultProviders, platforms);
                    if (!k_DuplicatePlatforms.Add(platforms))
                        Debug.LogWarning(string.Format("Duplicate instances of {0}", platforms), this);
                }
            }
        }

        void ValidateProviderArray(DefaultProvider[] defaultProviderArray, ModuleLoaderCore.OverridePlatforms? platform = null)
        {
            k_DuplicateProviderTypes.Clear();

            GetDefaultProviderTypesBatch(defaultProviderArray);
            GetProviderTypesBatch(defaultProviderArray);

            foreach (var row in defaultProviderArray)
            {
                var providerTypeName = row.providerTypeName;

                // Suppress warning when adding a new empty element
                if (providerTypeName == string.Empty)
                    continue;

                var providerType = row.providerType;
                if (providerType == null)
                    Debug.LogWarning(string.Format("Could not find provider type {0}", providerTypeName), this);

                var prefab = row.defaultProviderPrefab;
                if (prefab)
                {
                    // We can't call GetComponentsInChildren to check whether this prefab has the right component types
                }
                else
                {
                    var defaultProviderTypeName = row.defaultProviderTypeName;

                    // Suppress warning when adding a new empty element
                    if (defaultProviderTypeName == string.Empty)
                        continue;

                    var defaultProviderType = row.defaultProviderType;
                    if (defaultProviderType == null)
                        Debug.LogWarning(string.Format("Could not find default provider type {0}", defaultProviderTypeName), this);
                }

                if (!k_DuplicateProviderTypes.Add(providerTypeName))
                {
                    if (platform.HasValue)
                        Debug.LogWarning(string.Format("Duplicate instances of {0} in {1}", providerTypeName, platform), this);
                    else
                        Debug.LogWarning(string.Format("Duplicate instances of {0}", providerTypeName), this);
                }
            }
        }

        /// <summary>
        /// Set up functionality providers from the list of default providers
        /// This allows custom serialized data to be set up on prefabs for providers
        /// </summary>
        /// <param name="subscriberTypes">The types of subscribers that need providers</param>
        /// <param name="newProviders">(Optional) A list to which new providers will be added</param>
        public void SetupDefaultProviders(HashSet<Type> subscriberTypes, List<IFunctionalityProvider> newProviders = null)
        {
            Profiler.BeginSample(SetupDefaultProvidersProfilerLabel);
            if (subscriberTypes.Count == 0)
                return;

            CheckSetup();
            if (ModuleLoaderDebugSettings.instance.functionalityInjectionModuleLogging)
                Debug.LogFormat("Requiring default providers on: {0}", string.Join(", ",
                    (from type in subscriberTypes select type.Name).ToArray()));

            // Determine which provider interfaces are needed for the given subscribers
            var requiredProviders = new HashSet<Type>();
            foreach (var subscriberType in subscriberTypes)
            {
                GetRequiredProviders(subscriberType, requiredProviders);
            }

            k_NewProviderPrefabInstances.Clear();
            while (CheckMissingProviders(requiredProviders) > 0)
            {
                var providerAdded = false;
                foreach (var row in m_EffectiveDefaultProviders)
                {
                    var providerTypeName = row.providerTypeName;
                    var providerType = row.providerType;
                    if (providerType == null)
                    {
                        Debug.LogWarningFormat("Could not find type for {0} while setting up default providers", providerTypeName);
                        continue;
                    }

                    // Silently skip provider types that have already been overridden
                    if (m_Providers.ContainsKey(providerType))
                        continue;

                    if (!requiredProviders.Contains(providerType))
                        continue;

                    var prefab = row.defaultProviderPrefab;
                    if (prefab != null)
                    {
                        GameObject instance;
                        if (!k_NewProviderPrefabInstances.TryGetValue(prefab, out instance))
                        {
                            if (ModuleLoaderDebugSettings.instance.functionalityInjectionModuleLogging)
                                Debug.LogFormat("Functionality Injection Module creating default provider: {0}", prefab);

                            instance = GameObjectUtils.Instantiate(prefab);
                            k_NewProviderPrefabInstances[prefab] = instance;
                        }

                        var hasRequiredProvider = false;
                        var providersInPrefab = instance.GetComponentsInChildren<IFunctionalityProvider>();
                        foreach (var provider in providersInPrefab)
                        {
                            providerAdded = true;
                            var specificType = provider.GetType();
                            if (providerType.IsAssignableFrom(specificType))
                                hasRequiredProvider = true;

                            GetRequiredProviders(specificType, requiredProviders);
                            AddProvider(specificType, provider);
                        }

                        if (!hasRequiredProvider)
                            Debug.LogWarningFormat("Could not find a {0} on {1} while setting up default providers", providerTypeName, instance);

                        continue;
                    }

                    var defaultProviderTypeName = row.defaultProviderTypeName;
                    var defaultProviderType = row.defaultProviderType;
                    if (defaultProviderType == null)
                    {
                        Debug.LogWarningFormat("Cannot set up default provider {0}. Type cannot be found", defaultProviderTypeName);
                        continue;
                    }

                    if (!providerType.IsAssignableFrom(defaultProviderType))
                    {
                        Debug.LogWarningFormat("Cannot set up default provider. The given type {0} does not implement {1}", defaultProviderTypeName, providerType);
                        continue;
                    }

                    var vanillaProvider = GetOrCreateProviderInstance(defaultProviderType, providerType);
                    if (vanillaProvider == null)
                    {
                        Debug.LogWarningFormat("Cannot instantiate {0} as an IFunctionalityProvider.", defaultProviderTypeName);
                        continue;
                    }

                    if (newProviders != null)
                        newProviders.Add(vanillaProvider);

                    providerAdded = true;
                    GetRequiredProviders(defaultProviderType, requiredProviders);
                    AddProvider(defaultProviderType, vanillaProvider);
                }

                if (!providerAdded)
                    break;
            }

            InjectFunctionalityInDefaultProviders(k_NewProviderPrefabInstances, newProviders);
            ActivateProviderGameObjects();

            Profiler.EndSample();
        }

        int CheckMissingProviders(HashSet<Type> requiredProviders)
        {
            var compareSet = new HashSet<Type>(requiredProviders);
            compareSet.ExceptWith(m_Providers.Keys);
            return compareSet.Count;
        }

        public void ActivateProviderGameObjects()
        {
            foreach (var provider in m_Providers)
            {
                var monoBehaviour = provider.Value as MonoBehaviour;
                if (monoBehaviour != null)
                    monoBehaviour.gameObject.SetActive(true);
            }
        }

        public void InjectFunctionalityInDefaultProviders(Dictionary<GameObject, GameObject> newProvidersIn,
            List<IFunctionalityProvider> newProvidersOut)
        {
            foreach (var provider in m_UniqueProviders)
            {
                InjectFunctionalitySingle(provider);
            }

            foreach (var kvp in newProvidersIn)
            {
                var provider = kvp.Value;
                foreach (var subscriber in provider.GetComponentsInChildren<IFunctionalitySubscriber>())
                {
                    InjectFunctionalitySingle(subscriber);
                }
            }

            if (newProvidersOut != null)
            {
                foreach (var kvp in newProvidersIn)
                {
                    newProvidersOut.AddRange(kvp.Value.GetComponentsInChildren<IFunctionalityProvider>());
                }
            }
        }

        public void InjectFunctionality(List<object> objects, List<IFunctionalityProvider> newProviders = null)
        {
            Profiler.BeginSample(InjectFunctionalityProfilerLabel);
            if (objects.Count == 0)
                return;

            CheckSetup();

            k_UniqueTypes.Clear();
            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                k_UniqueTypes.Add(obj.GetType());
            }

            var requiredProviders = CollectionPool<List<Type>, Type>.GetCollection();
            foreach (var type in k_UniqueTypes)
            {
                k_SubscriberInterfaces.Clear();
                type.GetGenericInterfaces(typeof(IFunctionalitySubscriber<>), k_SubscriberInterfaces);
                foreach (var @interface in k_SubscriberInterfaces)
                {
                    requiredProviders.Add(@interface.GetGenericArguments()[0]);
                }
            }

            RequireProviders(requiredProviders, newProviders);
            CollectionPool<List<Type>, Type>.RecycleCollection(requiredProviders);

            foreach (var obj in objects)
            {
                InjectFunctionalitySingle(obj);
            }

            Profiler.EndSample();
        }

        public void InjectFunctionality(List<IFunctionalitySubscriber> objects, List<IFunctionalityProvider> newProviders = null)
        {
            Profiler.BeginSample(InjectFunctionalityProfilerLabel);
            if (objects.Count == 0)
                return;

            CheckSetup();

            k_UniqueTypes.Clear();
            foreach (var obj in objects)
            {
                if (obj == null)
                    continue;

                k_UniqueTypes.Add(obj.GetType());
            }

            var requiredProviders = CollectionPool<List<Type>, Type>.GetCollection();
            foreach (var type in k_UniqueTypes)
            {
                k_SubscriberInterfaces.Clear();
                type.GetGenericInterfaces(typeof(IFunctionalitySubscriber<>), k_SubscriberInterfaces);
                foreach (var @interface in k_SubscriberInterfaces)
                {
                    requiredProviders.Add(@interface.GetGenericArguments()[0]);
                }
            }

            RequireProviders(requiredProviders, newProviders);
            CollectionPool<List<Type>, Type>.RecycleCollection(requiredProviders);

            InjectFunctionalityGroup(objects);

            Profiler.EndSample();
        }

        /// <summary>
        /// Inject functionality on a set of objects, assuming that all required providers have been setup.
        /// </summary>
        /// <param name="objects"></param>
        public void InjectPreparedFunctionality(List<IFunctionalitySubscriber> objects)
        {
            if (objects.Count == 0)
                return;

            CheckSetup();
            InjectFunctionalityGroup(objects);
        }

        /// <summary>
        /// Inject functionality on a set of objects, assuming that all required providers have been setup.
        /// </summary>
        /// <param name="objects"></param>
        public void InjectPreparedFunctionality(List<object> objects)
        {
            if (objects.Count == 0)
                return;

            CheckSetup();
            InjectFunctionalityGroup(objects);
        }

        void InjectFunctionalityGroup(List<IFunctionalitySubscriber> objects)
        {
            foreach (var obj in objects)
            {
                foreach (var provider in m_UniqueProviders)
                {
                    provider.ConnectSubscriber(obj);
                }
            }
        }

        void InjectFunctionalityGroup(List<object> objects)
        {
            foreach (var obj in objects)
            {
                foreach (var provider in m_UniqueProviders)
                {
                    provider.ConnectSubscriber(obj);
                }
            }
        }

        public void InjectFunctionalitySingle(object obj)
        {
            if (obj == null)
                return;

            CheckSetup();

            foreach (var provider in m_UniqueProviders)
            {
                provider.ConnectSubscriber(obj);
            }
        }

        /// <summary>
        /// Inject functionality on an entire scene, assuming that
        /// </summary>
        /// <param name="scene"></param>
        public void InjectFunctionality(Scene scene)
        {
            CheckSetup();
            foreach (var go in scene.GetRootGameObjects())
            {
                InjectFunctionality(go);
            }
        }

        public void InjectFunctionality(GameObject go)
        {
            k_Subscribers.Clear();
            go.GetComponentsInChildren(k_Subscribers);
            InjectFunctionality(k_Subscribers);
        }

        public void AddProviders(List<IFunctionalityProvider> newProviders)
        {
            if (newProviders.Count == 0)
                return;

            CheckSetup();

            foreach (var provider in newProviders)
            {
                var type = provider.GetType();
                if (m_Providers.ContainsKey(type))
                {
                    Debug.LogWarning(string.Format("A provider for {0} already exists.", type));
                    continue;
                }

                AddProvider(type, provider);
            }

            foreach (var provider in m_UniqueProviders)
            {
                InjectFunctionalitySingle(provider);
            }
        }

        public void RemoveProviders(List<IFunctionalityProvider> providersToRemove)
        {
            CheckSetup();

            foreach (var provider in providersToRemove)
            {
                m_UniqueProviders.Remove(provider);

                foreach (var providerInterface in provider.GetType().GetInterfaces())
                {
                    var baseProviderType = typeof(IFunctionalityProvider);
                    if (providerInterface == baseProviderType || !baseProviderType.IsAssignableFrom(providerInterface))
                        continue;

                    IFunctionalityProvider existingProvider;
                    if (m_Providers.TryGetValue(providerInterface, out existingProvider) && provider == existingProvider)
                        m_Providers.Remove(providerInterface);
                }
            }
        }

        void RequireProviders(List<Type> providerTypes, List<IFunctionalityProvider> newProviders)
        {
            foreach (var providerType in providerTypes)
            {
                if (!providerType.IsInterface)
                {
                    Debug.LogWarning(string.Format("Type {0} is not an interface. Please provide the provider interface type, not the type which implements it.", providerType));
                    continue;
                }

                if (!typeof(IFunctionalityProvider).IsAssignableFrom(providerType))
                {
                    Debug.LogWarning(string.Format("Type {0} is not a functionality provider interface.", providerType));
                    continue;
                }

                if (m_Providers.ContainsKey(providerType))
                    continue;

                k_Implementors.Clear();
                providerType.GetImplementationsOfInterface(k_Implementors);
                var count = k_Implementors.Count;
                if (count == 0)
                {
                    Debug.LogWarning(string.Format("No providers found for {0}", providerType));
                    continue;
                }

                var firstProvider = k_Implementors[0];
                if (count > 1)
                    Debug.LogWarning(string.Format("More than one provider found for {0}. Using {1}", providerType, firstProvider));

                // Spawn or gain access to the class needed to support the functionality
                var provider = GetOrCreateProviderInstance(firstProvider, providerType);

                if (newProviders != null)
                    newProviders.Add(provider);

                AddProvider(firstProvider, provider);
            }

            foreach (var provider in m_UniqueProviders)
            {
                InjectFunctionalitySingle(provider);
            }

            ActivateProviderGameObjects();
        }

        public static IFunctionalityProvider GetOrCreateProviderInstance(Type implementorType, Type providerType)
        {
            IFunctionalityProvider provider;
            if (typeof(MonoBehaviour).IsAssignableFrom(implementorType))
            {
                var go = GameObjectUtils.Create(providerType.Name);
                go.SetActive(false);
                provider = (IFunctionalityProvider)go.AddComponent(implementorType);
            }
            else if (typeof(ScriptableSettingsBase).IsAssignableFrom(implementorType))
            {
                provider = (IFunctionalityProvider)ScriptableSettingsBase.GetInstanceByType(implementorType);
            }
            else if (typeof(ScriptableObject).IsAssignableFrom(implementorType))
            {
                Debug.LogError(string.Format("Trying to create {0}: Provider type {1} is a Scriptable Object. Functionality providers with " +
                    "serialized data must derive from ScriptableSettings for their data to survive reload.", providerType, implementorType));
                return null;
            }
            else
            {
                provider = (IFunctionalityProvider)Activator.CreateInstance(implementorType);
            }

            if (ModuleLoaderDebugSettings.instance.functionalityInjectionModuleLogging)
                Debug.LogFormat("Functionality Injection Module creating provider: {0}", provider);

            return provider;
        }

        public void AddProvider(Type providerType, IFunctionalityProvider provider)
        {
            if (!m_UniqueProviders.Add(provider))
                Debug.LogWarning(string.Format("Provider {0} of type {1} was added twice", provider, providerType));

            provider.LoadProvider();

            //One object may provide multiple functionalities
            foreach (var providerInterface in providerType.GetInterfaces())
            {
                if (providerInterface == typeof(IFunctionalityProvider))
                    continue;

                if (typeof(IFunctionalityProvider).IsAssignableFrom(providerInterface)
                    && !m_Providers.ContainsKey(providerInterface))
                    m_Providers.Add(providerInterface, provider);
            }
        }

        static void GetRequiredProviders(Type subscriberType, HashSet<Type> providerTypes)
        {
            foreach (var subscriberInterface in subscriberType.GetInterfaces())
            {
                // In case of derived subscriber types
                GetRequiredProviders(subscriberInterface, providerTypes);

                if (!typeof(IFunctionalitySubscriber).IsAssignableFrom(subscriberType))
                    continue;

                var genericArguments = subscriberInterface.GetGenericArguments();
                if (genericArguments.Length == 0)
                    continue;

                var firstArgument = genericArguments[0];
                if (!typeof(IFunctionalityProvider).IsAssignableFrom(firstArgument))
                    continue;

                providerTypes.Add(firstArgument);
            }
        }

        public void Unload()
        {
            CheckSetup();
            foreach (var provider in m_UniqueProviders)
            {
                if (provider != null)
                    provider.UnloadProvider();
                else
                    Debug.LogError("Encountered a null provider during Unload--this should never happen");
            }

            m_Providers.Clear();
            m_UniqueProviders.Clear();
        }

        public void CheckSetup()
        {
            if (!m_Setup)
                Debug.LogWarningFormat("You are trying to use {0} but it is not set up. Do you need to add it to the " +
                    "Functionality Injection Module?", this);
        }

        public string PrintStatus()
        {
            var sb = new StringBuilder();
            sb.Append(string.Format("{0}:\n", name));
            foreach (var kvp in m_Providers)
            {
                var providerInterface = kvp.Key.GetNameWithGenericArguments();
                var providerType = kvp.Value.GetType().GetNameWithGenericArguments();
                sb.Append(string.Format("\t{0}: {1}\n", providerInterface, providerType));
            }

            return sb.ToString();
        }

        public void LoadProvider() {}

        public void ConnectSubscriber(object obj)
        {
#if !FI_AUTOFILL
            var fiSubscriber = obj as IFunctionalitySubscriber<IProvidesFunctionalityInjection>;
            if (fiSubscriber != null)
                fiSubscriber.provider = this;
#endif
        }

        public void UnloadProvider() {}

        public void OnBehaviorDestroy()
        {
            // Disable all MonoBehavior providers so they send OnLoss events before objects are destroyed
            foreach (var provider in m_UniqueProviders)
            {
                var behavior = provider as MonoBehaviour;
                if (behavior)
                    behavior.enabled = false;
            }
        }
    }
}
