using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Labs.ModuleLoader;
using Unity.Labs.Utils;
using UnityEditor.Experimental.EditorVR.Utilities;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.InputNew;
using UnityEngine.XR;
using UnityObject = UnityEngine.Object;

namespace UnityEditor.Experimental.EditorVR.Core
{
#if UNITY_EDITOR
    [InitializeOnLoad]
#endif
    sealed class EditingContextManager : MonoBehaviour
    {
#pragma warning disable 649
        [SerializeField]
        UnityObject m_DefaultContext;
#pragma warning restore 649

        internal const string settingsPath = "ProjectSettings/EditingContextManagerSettings.asset";
        internal const string userSettingsPath = "Library/EditingContextManagerSettings.asset";

        const string k_AutoOpen = "EditorXR.EditingContextManager.AutoOpen";
        const string k_LaunchOnExitPlaymode = "EditorXR.EditingContextManager.LaunchOnExitPlaymode";

        IEditingContext m_CurrentContext;

        static InputManager s_InputManager;
        static List<IEditingContext> s_AvailableContexts;
        static EditingContextManagerSettings s_Settings;
        static UnityObject s_DefaultContext;

#if UNITY_EDITOR
        static bool s_AutoOpened;
        static bool s_UserWasPresent;
        static bool s_EnableXRFailed;
#endif

        string[] m_ContextNames;
        int m_SelectedContextIndex;

        readonly List<IEditingContext> m_PreviousContexts = new List<IEditingContext>();

        Rect m_ContextPopupRect = new Rect(5, 0, 100, 20); // Position will be set based on window size
        Rect m_ContextLabelRect = new Rect(5, 0, 100, 20); // Position will be set based on window size

        internal static EditingContextManager instance { get; private set; }

        internal static IEditingContext defaultContext
        {
            get
            {
                var availableContexts = GetAvailableEditingContexts();
                var context = availableContexts.Find(c => c.Equals(s_DefaultContext)) ?? availableContexts.First();

                var defaultContextName = settings.defaultContextName;
                if (!string.IsNullOrEmpty(defaultContextName))
                {
                    var foundContext = availableContexts.Find(c => c.name == defaultContextName);
                    if (foundContext != null)
                        context = foundContext;
                }

                return context;
            }
            set { settings.defaultContextName = value.name; }
        }

        internal IEditingContext currentContext
        {
            get { return m_CurrentContext; }
        }

        static EditingContextManagerSettings settings
        {
            get
            {
                if (!s_Settings)
                    s_Settings = LoadUserSettings();

                return s_Settings;
            }
        }

        static bool autoOpen
        {
            get { return EditorPrefs.GetBool(k_AutoOpen, true); }
            set { EditorPrefs.SetBool(k_AutoOpen, value); }
        }

#if UNITY_EDITOR
        static EditingContextManager()
        {
            VRView.viewEnabled += OnVRViewEnabled;
            VRView.viewDisabled += OnVRViewDisabled;
            EditorApplication.update += ReopenOnExitPlaymode;
            EditorApplication.delayCall += OnAutoOpenStateChanged;
        }
#endif

        static void OnVRViewEnabled()
        {
            Resources.UnloadUnusedAssets();
            InitializeInputManager();
            if (!Application.isPlaying)
            {
                instance = EditorXRUtils.CreateGameObjectWithComponent<EditingContextManager>();
                instance.transform.SetParent(ModuleLoaderCore.instance.GetModuleParent().transform);
            }
        }

        static void OnVRViewDisabled()
        {
#if UNITY_EDITOR
            s_AutoOpened = false;
#endif
            UnityObjectUtils.Destroy(instance.gameObject);
            if (s_InputManager)
                UnityObjectUtils.Destroy(s_InputManager.gameObject);
        }

#if UNITY_EDITOR
        [MenuItem("Window/EditorXR %e", false)]
        internal static void ShowEditorVR()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                return;

            // Using a utility window improves performance by saving from the overhead of DockArea.OnGUI()
            EditorWindow.GetWindow<VRView>(true, "EditorXR", true);
        }

        [MenuItem("Window/EditorXR %e", true)]
        static bool ShouldShowEditorVR()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Application.isPlaying)
                return false;

            return PlayerSettings.GetVirtualRealitySupported(BuildTargetGroup.Standalone);
        }

#if UNITY_2018_3_OR_NEWER
        [SettingsProvider]
        static SettingsProvider CreateSettingsProvider()
        {
            var contextNames = GetEditingContextNames();
            if (string.IsNullOrEmpty(settings.defaultContextName))
                settings.defaultContextName = defaultContext.name;

            var selectedIndex = Array.IndexOf(contextNames, settings.defaultContextName);
            var provider = new SettingsProvider("Project/EditorXR/Context Manager", SettingsScope.Project)
            {
                label = "Context Manager",
                guiHandler = (searchContext) =>
                {
                    EditorGUILayout.LabelField("Global Settings", EditorStyles.boldLabel);

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        using (var changed = new EditorGUI.ChangeCheckScope())
                        {
                            selectedIndex = EditorGUILayout.Popup("Default Context", selectedIndex, contextNames);
                            if (changed.changed)
                            {
                                settings.defaultContextName = contextNames[selectedIndex];
                                SaveProjectSettings(settings);
                                GUIUtility.ExitGUI();
                            }
                        }

                        const float resetButtonWidth = 40f;
                        if (GUILayout.Button("Reset", EditorStyles.miniButton, GUILayout.Width(resetButtonWidth)))
                        {
                            ResetProjectSettings();
                            selectedIndex = 0;
                        }
                    }

                    // Auto open an EditorXR context
                    const string title = "Auto open";
                    const string tooltip = "Automatically open an EditorXR context when the HMD is being worn";

                    using (var change = new EditorGUI.ChangeCheckScope())
                    {
                        autoOpen = EditorGUILayout.Toggle(new GUIContent(title, tooltip), autoOpen);

                        if (change.changed)
                            OnAutoOpenStateChanged();

                        if (s_EnableXRFailed)
                        {
                            const float retryButtonWidth = 70f;
                            EditorGUILayout.HelpBox("Failed to initialize XR session. Check that your device and platform software are working properly.", MessageType.Warning);
                            if (GUILayout.Button("Retry", GUILayout.Width(retryButtonWidth)))
                            {
                                s_EnableXRFailed = false;
                                OnAutoOpenStateChanged();
                            }
                        }
                    }

                    var contextTypes = CollectionPool<List<Type>, Type>.GetCollection();
                    typeof(IEditingContext).GetImplementationsOfInterface(contextTypes);
                    foreach (var contextType in contextTypes)
                    {
                        var preferencesGUIMethod = contextType.GetMethod("PreferencesGUI", BindingFlags.Static | BindingFlags.NonPublic);
                        if (preferencesGUIMethod != null)
                        {
                            EditorGUILayout.Space();
                            EditorGUILayout.Space();
                            EditorGUILayout.LabelField(contextType.Name.Replace("Context", string.Empty), EditorStyles.boldLabel);
                            preferencesGUIMethod.Invoke(null, null);
                        }
                    }

                    CollectionPool<List<Type>, Type>.RecycleCollection(contextTypes);
                }
            };

            return provider;
        }
#endif

        static void OnAutoOpenStateChanged()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (autoOpen)
            {
                s_AutoOpened = false;
                s_UserWasPresent = false;
                s_EnableXRFailed = false;
                EditorApplication.update += OpenIfUserPresent;
            }
            else
            {
                EditorApplication.update -= OpenIfUserPresent;
                XRSettings.enabled = false;
                s_AutoOpened = false;
                s_UserWasPresent = false;
                s_EnableXRFailed = false;
            }
        }

        static void OpenIfUserPresent()
        {
            if (EditorApplication.isCompiling || Application.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (!ShouldShowEditorVR())
                return;

            if (!XRSettings.enabled)
            {
                XRSettings.enabled = true;
                if (!XRSettings.enabled)
                {
                    // Initialization failed, so don't keep trying
                    EditorApplication.update -= OpenIfUserPresent;
                    s_EnableXRFailed = true;
                    return;
                }
            }

            s_EnableXRFailed = false;

            if (EditorWindow.mouseOverWindow == null)
                return;

            var userPresent = VRView.GetIsUserPresent();
            var view = VRView.activeView;
            if (!s_UserWasPresent && userPresent && !view && !s_AutoOpened)
            {
                s_AutoOpened = true;
                EditorApplication.delayCall += ShowEditorVR;
            }
            else if (s_UserWasPresent && view && !userPresent && s_AutoOpened)
            {
                s_AutoOpened = false;
                EditorApplication.delayCall += view.Close;
            }

            s_UserWasPresent = userPresent;
        }

        // Life cycle management across playmode switches is an odd beast indeed, and there is a need to reliably relaunch
        // EditorVR after we switch back out of playmode (assuming the view was visible before a playmode switch). So,
        // we watch until playmode is done and then relaunch.
        static void ReopenOnExitPlaymode()
        {
            var launch = EditorPrefs.GetBool(k_LaunchOnExitPlaymode, false);
            if (!launch || !EditorApplication.isPlaying)
            {
                EditorPrefs.DeleteKey(k_LaunchOnExitPlaymode);
                EditorApplication.update -= ReopenOnExitPlaymode;
                if (launch)
                    EditorApplication.delayCall += ShowEditorVR;
            }
        }
#endif

#if UNITY_EDITOR
        static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode)
            {
                EditorPrefs.SetBool(k_LaunchOnExitPlaymode, true);
                var view = VRView.activeView;
                if (view)
                    view.Close();
            }
        }
#endif

        void OnEnable()
        {
            ModuleLoaderCore.instance.OnBehaviorEnable();

            ISetEditingContextMethods.getAvailableEditingContexts = GetAvailableEditingContexts;
            ISetEditingContextMethods.getPreviousEditingContexts = GetPreviousEditingContexts;
            ISetEditingContextMethods.setEditingContext = SetEditingContext;
            ISetEditingContextMethods.restorePreviousEditingContext = RestorePreviousContext;

#if UNITY_EDITOR
            if (runInEditMode)
            {
                // Force the window to repaint every tick, since we need live updating
                // This also allows scripts with [ExecuteInEditMode] to run
                EditorApplication.update += EditorApplication.QueuePlayerLoopUpdate;

                EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

                SetEditingContext(defaultContext);
            }
#endif
        }

        void OnDisable()
        {
            var moduleLoaderCore = ModuleLoaderCore.instance;
            moduleLoaderCore.OnBehaviorDisable();

            if (Application.isPlaying)
            {
                OnVRViewDisabled();
            }
#if UNITY_EDITOR
            else
            {
                EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

                EditorApplication.update -= EditorApplication.QueuePlayerLoopUpdate;

                VRView.afterOnGUI -= OnVRViewGUI;
            }
#endif

            if (m_CurrentContext != null)
            {
                defaultContext = m_CurrentContext;
                m_CurrentContext.Dispose();
            }

            s_AvailableContexts = null;

            SetEditingContext(null);

            ISetEditingContextMethods.getAvailableEditingContexts = null;
            ISetEditingContextMethods.getPreviousEditingContexts = null;
            ISetEditingContextMethods.setEditingContext = null;
            ISetEditingContextMethods.restorePreviousEditingContext = null;

            SaveUserSettings(settings);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                foreach (var module in moduleLoaderCore.modules)
                {
                    var behavior = module as MonoBehaviour;
                    if (behavior != null)
                        behavior.StopRunInEditMode();
                }
            }
#endif
        }

        void Awake()
        {
            s_DefaultContext = m_DefaultContext;

            var availableContexts = GetAvailableEditingContexts();
            m_ContextNames = availableContexts.Select(c => c.name).ToArray();

            if (s_AvailableContexts.Count == 0)
                throw new Exception("You can't start EditorXR without at least one context. Try re-importing the package or use version control to restore the default context asset");

#if UNITY_EDITOR
            if (s_AvailableContexts.Count > 1)
                VRView.afterOnGUI += OnVRViewGUI;
#endif

            EditorXRUtils.hideFlags = ModuleLoaderDebugSettings.instance.moduleHideFlags;
            var moduleLoaderCore = ModuleLoaderCore.instance;
            if (Application.isPlaying)
                moduleLoaderCore.ReloadModules();

            moduleLoaderCore.OnBehaviorAwake();

            if (Application.isPlaying)
            {
                OnVRViewEnabled();
                instance = this;
                SetEditingContext((IEditingContext)m_DefaultContext);
            }
#if UNITY_EDITOR
            else
            {
                foreach (var module in moduleLoaderCore.modules)
                {
                    var behavior = module as MonoBehaviour;
                    if (behavior != null)
                        behavior.StartRunInEditMode();
                }
            }
#endif
        }

#if UNITY_EDITOR
        void OnVRViewGUI(VRView view)
        {
            const float paddingX = 5;
            var position = view.position;
            var height = position.height - m_ContextPopupRect.height * 2;
            var popupX = position.width - m_ContextPopupRect.width - paddingX;

            m_ContextPopupRect.x = popupX;
            m_ContextPopupRect.y = height;
            m_ContextLabelRect.x = popupX - m_ContextLabelRect.width;
            m_ContextLabelRect.y = height;

            GUI.Label(m_ContextLabelRect, "Editing Context:");
            m_SelectedContextIndex = EditorGUI.Popup(m_ContextPopupRect, m_SelectedContextIndex, m_ContextNames);
            if (GUI.changed)
            {
                SetEditingContext(s_AvailableContexts[m_SelectedContextIndex]);
                GUIUtility.ExitGUI();
            }
        }
#endif

        internal void SetEditingContext(IEditingContext context)
        {
            if (context == null)
                return;

            if (m_CurrentContext != null)
            {
                m_PreviousContexts.Insert(0, m_CurrentContext);

                if (m_CurrentContext.instanceExists)
                    m_CurrentContext.Dispose();
            }

            context.Setup();
            m_CurrentContext = context;

            m_SelectedContextIndex = s_AvailableContexts.IndexOf(context);
        }

        internal void RestorePreviousContext()
        {
            if (m_PreviousContexts.Count > 0)
                SetEditingContext(m_PreviousContexts.First());
        }

        public static List<IEditingContext> GetEditingContextAssets()
        {
#if UNITY_EDITOR
            var availableContexts = new List<IEditingContext>();
            var contextTypes = CollectionPool<List<Type>, Type>.GetCollection();
            typeof(IEditingContext).GetImplementationsOfInterface(contextTypes);
            var searchString = "t: " + string.Join(" t: ", contextTypes.Select(t => t.FullName).ToArray());
            CollectionPool<List<Type>, Type>.RecycleCollection(contextTypes);
            var assets = AssetDatabase.FindAssets(searchString);

            foreach (var asset in assets)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(asset);
                var context = AssetDatabase.LoadMainAssetAtPath(assetPath) as IEditingContext;
                availableContexts.Add(context);
            }
#else
            var availableContexts = DefaultScriptReferences.GetEditingContexts();
#endif

            return availableContexts;
        }

        internal static string[] GetEditingContextNames()
        {
            var availableContexts = GetEditingContextAssets();
            return availableContexts.Select(c => c.name).ToArray();
        }

        static List<IEditingContext> GetAvailableEditingContexts()
        {
            if (s_AvailableContexts == null)
                s_AvailableContexts = GetEditingContextAssets();

            return s_AvailableContexts;
        }

        List<IEditingContext> GetPreviousEditingContexts()
        {
            return m_PreviousContexts;
        }

        internal static EditingContextManagerSettings LoadProjectSettings()
        {
            EditingContextManagerSettings settings = ScriptableObject.CreateInstance<EditingContextManagerSettings>();
            if (File.Exists(settingsPath))
                JsonUtility.FromJsonOverwrite(File.ReadAllText(settingsPath), settings);

            return settings;
        }

        internal static EditingContextManagerSettings LoadUserSettings()
        {
            EditingContextManagerSettings settings;
            if (File.Exists(userSettingsPath)
                && File.GetLastWriteTime(userSettingsPath) > File.GetLastWriteTime(settingsPath))
            {
                settings = ScriptableObject.CreateInstance<EditingContextManagerSettings>();
                JsonUtility.FromJsonOverwrite(File.ReadAllText(userSettingsPath), settings);
            }
            else
                settings = LoadProjectSettings();

            return settings;
        }

        internal static void ResetProjectSettings()
        {
#if UNITY_EDITOR
            File.Delete(userSettingsPath);

            if (EditorUtility.DisplayDialog("Delete Project Settings?", "Would you like to remove the project-wide settings, too?", "Yes", "No"))
                File.Delete(settingsPath);
#endif
        }

        internal static void SaveProjectSettings(EditingContextManagerSettings settings)
        {
#if UNITY_EDITOR
            File.WriteAllText(settingsPath, JsonUtility.ToJson(settings, true));
#endif
        }

        internal static void SaveUserSettings(EditingContextManagerSettings settings)
        {
#if UNITY_EDITOR
            File.WriteAllText(userSettingsPath, JsonUtility.ToJson(settings, true));
#endif
        }

        static void InitializeInputManager()
        {
            // HACK: InputSystem has a static constructor that is relied upon for initializing a bunch of other components, so
            // in edit mode we need to handle lifecycle explicitly
            var managers = Resources.FindObjectsOfTypeAll<InputManager>();
            foreach (var m in managers)
            {
                UnityObjectUtils.Destroy(m.gameObject);
            }

            managers = Resources.FindObjectsOfTypeAll<InputManager>();

            if (managers.Length == 0)
            {
                // Attempt creating object hierarchy via an implicit static constructor call by touching the class
                InputSystem.ExecuteEvents();
                managers = Resources.FindObjectsOfTypeAll<InputManager>();

                if (managers.Length == 0)
                {
                    typeof(InputSystem).TypeInitializer.Invoke(null, null);
                    managers = Resources.FindObjectsOfTypeAll<InputManager>();
                }
            }

            Assert.IsTrue(managers.Length == 1, "Only one InputManager should be active; Count: " + managers.Length);

            s_InputManager = managers[0];
            var go = s_InputManager.gameObject;
            go.SetRunInEditModeRecursively(true);
            go.transform.SetParent(ModuleLoaderCore.instance.GetModuleParent().transform);

            // These components were allocating memory every frame and aren't currently used in EditorVR
            UnityObjectUtils.Destroy(s_InputManager.GetComponent<JoystickInputToEvents>());
            UnityObjectUtils.Destroy(s_InputManager.GetComponent<MouseInputToEvents>());
            UnityObjectUtils.Destroy(s_InputManager.GetComponent<KeyboardInputToEvents>());
            UnityObjectUtils.Destroy(s_InputManager.GetComponent<TouchInputToEvents>());
        }

        void Update()
        {
            ModuleLoaderCore.instance.OnBehaviorUpdate();
        }

        void OnDestroy()
        {
            var moduleLoaderCore = ModuleLoaderCore.instance;
            moduleLoaderCore.OnBehaviorDestroy();

            if (Application.isPlaying)
                moduleLoaderCore.UnloadModules();
        }
    }
}
