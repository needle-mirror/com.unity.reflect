using System;
using System.Collections.Generic;
using System.IO;
using Grpc.Core;
using Unity.EditorCoroutines.Editor;
using Unity.Reflect;
using Unity.Reflect.Model;
using UnityEditor;
using UnityEngine;
using UnityEngine.Reflect;

static class DialogText
{
    public static readonly string title = "Reflect Material Upgrade";
    public static readonly string proceed = "Proceed";
    public static readonly string ok = "Ok";
    public static readonly string cancel = "Cancel";
    public static readonly string noSelectionMessage = "You must select at least one material.";
}

class ReflectWindow : EditorWindow
{
    [SerializeField]
    string m_ImportFolder = "Reflect";

    ProjectManagerInternal m_ProjectManagerInternal;
    ReflectProjectDownloader m_ProjectDownloader;

    Vector2 m_ScrollPosition;

    string m_TaskInProgressName;
    float m_TaskProgress;

    DateTime m_LastUpdate;
    const double k_UpdateIntervalMs = 1000.0;

    static GUIStyle s_HeaderStyle;

    EditorCoroutine m_RefreshProjectsCoroutine;

    bool m_IsFetchingProjects;

    Exception m_RefreshProjectsException;

    [UnityEditor.MenuItem("Window/Reflect/Reflect Window")]
    static void OpenWindow()
    {
        var window = GetWindow<ReflectWindow>("Reflect");
        window.Show();
    }
    
    [UnityEditor.MenuItem("Window/Reflect/Convert to current RenderPipeline", priority = 1100)]
    static void ConvertReflectToCurrentRenderPipeline()
    {
        var assetPaths = new List<string>();
        
        FindAssetWithExtension(SyncMaterial.Extension, assetPaths);
        FindAssetWithExtension(SyncObject.Extension, assetPaths); // Because some SyncObjects might be using the defaultMaterial
        FindAssetWithExtension(SyncPrefab.Extension, assetPaths); // TODO Investigate why it's not automatically triggered

        if (assetPaths.Count == 0)
        {
            EditorUtility.DisplayDialog(DialogText.title, DialogText.noSelectionMessage, DialogText.ok);
            return;
        }

        if (!EditorUtility.DisplayDialog(DialogText.title, $"Convert imported Reflect assets to {ReflectMaterialManager.converterName}", 
            DialogText.proceed, DialogText.cancel))
        {
            return;
        }

        AssetDatabase.StartAssetEditing();

        foreach (var assetPath in assetPaths)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.Default);
        }
        
        AssetDatabase.StopAssetEditing();
    }

    static void FindAssetWithExtension(string extension, List<string> assetPaths)
    {
        var fullPaths = Directory.GetFiles(Application.dataPath, $"*{extension}", SearchOption.AllDirectories);

        foreach (var path in fullPaths)
        {
            var assetPath = path.Replace(Application.dataPath, "Assets" + Path.DirectorySeparatorChar);
            assetPaths.Add(assetPath);
        }
    }

    void OnEnable()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;

        StopProjectDiscovery();
    }

    void StartProjectDiscovery()
    {
        if (m_RefreshProjectsCoroutine != null)
        {
            EditorCoroutineUtility.StopCoroutine(m_RefreshProjectsCoroutine);
        }

        if (m_ProjectManagerInternal == null)
        {
            var fullImportFolder = Path.Combine(Application.dataPath, m_ImportFolder);
            m_ProjectManagerInternal = new ProjectManagerInternal(fullImportFolder, false, true);
            
            m_ProjectDownloader = new ReflectProjectDownloader(fullImportFolder);

            m_ProjectDownloader.onProgressChanged += f =>
            {
                m_TaskProgress = f;
                
                if (m_TaskProgress < 1.0f)
                {
                    m_TaskInProgressName = "Downloading";
                }
                else
                {
                    m_TaskInProgressName = null;
                    m_TaskProgress = 0.0f;
                    AssetDatabase.Refresh();
                }
            };
            m_ProjectDownloader.onError += exception =>
            {
                var msg = exception is RpcException rpcException ? rpcException.Status.Detail : exception.ToString();
                Debug.LogError(msg);
            };

            m_ProjectManagerInternal.progressChanged += (f, s) =>
            {
                m_TaskProgress = f;
                m_TaskInProgressName = s;
            };

            m_ProjectManagerInternal.onError += exception =>
            {
                if (m_IsFetchingProjects)
                {
                    m_RefreshProjectsException = exception;
                }

                var msg = exception is RpcException rpcException ? rpcException.Status.Detail : exception.Message;
                Debug.LogError(msg);
            };

            m_ProjectManagerInternal.taskCompleted += () =>
            {
                m_TaskInProgressName = null;
                m_TaskProgress = 0.0f;
                AssetDatabase.Refresh(); };

            m_TaskInProgressName = null;
            m_TaskProgress = 0.0f;

            m_ProjectManagerInternal.onProjectsRefreshBegin += OnProjectRefreshBegin;
            m_ProjectManagerInternal.onProjectsRefreshEnd += OnProjectRefreshEnd;
        }

        m_RefreshProjectsCoroutine = EditorCoroutineUtility.StartCoroutine(m_ProjectManagerInternal.RefreshProjectListCoroutine(), this);
    }

    void OnProjectRefreshBegin()
    {
        m_RefreshProjectsException = null;
        m_IsFetchingProjects = true;
    }

    void OnProjectRefreshEnd()
    {
        m_IsFetchingProjects = false;
    }

    void StopProjectDiscovery()
    {
        if (m_ProjectManagerInternal != null)
        {
            m_ProjectManagerInternal.onProjectsRefreshBegin -= OnProjectRefreshBegin;
            m_ProjectManagerInternal.onProjectsRefreshEnd -= OnProjectRefreshEnd;

            m_ProjectManagerInternal = null;
        }
    }

    void OnEditorUpdate()
    {
        if (Application.isPlaying)
            return;

        var now = DateTime.Now;

        if ((now - m_LastUpdate).TotalMilliseconds >= k_UpdateIntervalMs)
        {
            m_LastUpdate = now;
            Repaint();
        }
    }

    void OnGUI()
    {
        if (s_HeaderStyle == null)
            s_HeaderStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };

        EditorGUILayout.Space();

        if (Application.isPlaying)
        {
            StopProjectDiscovery();

            EditorGUILayout.HelpBox("Reflect import features are unavailable during play mode.", MessageType.Info);
            return;
        }

        if (string.IsNullOrEmpty(CloudProjectSettings.accessToken))
        {
            EditorGUILayout.HelpBox("A valid Unity User session is required to access Reflect services. Please Signin with the Unity Hub.", MessageType.Info);
            return;
        }

        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.LabelField("Projects", s_HeaderStyle);

        using (new EditorGUI.DisabledScope(m_IsFetchingProjects))
        {
            if (GUILayout.Button("Refresh", GUILayout.Width(100)))
            {
                StopProjectDiscovery();
                StartProjectDiscovery();
            }
        }

        EditorGUILayout.EndHorizontal();

        if (m_ProjectManagerInternal == null)
        {
            StartProjectDiscovery();
        }

        if (m_RefreshProjectsException != null)
        {
            EditorGUILayout.HelpBox($"Error: {m_RefreshProjectsException.Message}", MessageType.Error);
            if (m_RefreshProjectsException is ProjectListRefreshException ex && ex.Status == UnityProjectCollection.StatusOption.ComplianceError)
            {
                if (GUILayout.Button("Learn more", GUILayout.Width(100)))
                {
                    Application.OpenURL(ProjectMenuManager.ReflectLandingPageUrl);
                }
            }

            GUIUtility.ExitGUI();
        }

        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);

        EditorGUILayout.BeginVertical();

        if (m_IsFetchingProjects)
        {
            EditorGUILayout.LabelField("Fetching....");
        }

        var taskInProgress = !string.IsNullOrEmpty(m_TaskInProgressName);

        foreach (var project in m_ProjectManagerInternal.Projects)
        {
            using (new EditorGUI.DisabledScope(taskInProgress))
            {
                if (ProjectGUI(project))
                {
                    EditorCoroutineUtility.StartCoroutine(m_ProjectDownloader.Download(project), this);
                }
            }
        }

        EditorGUILayout.EndVertical();

        EditorGUILayout.EndScrollView();

        var rect = EditorGUILayout.GetControlRect();

        using (new EditorGUI.DisabledScope(!taskInProgress))
        {
            EditorGUI.ProgressBar(rect, taskInProgress ? m_TaskProgress : 0.0f, taskInProgress ? m_TaskInProgressName : string.Empty);
        }
    }

    bool ProjectGUI(Project project)
    {
        var boxStyle = new GUIStyle(GUI.skin.box);

        EditorGUILayout.BeginVertical(boxStyle);

        EditorGUILayout.LabelField(project.name, s_HeaderStyle);
        EditorGUILayout.LabelField(project.description);

        var pressed = false;

        EditorGUILayout.BeginHorizontal();

        var connected = m_ProjectManagerInternal.IsProjectAvailableOnline(project);
        var hasLocalData = m_ProjectManagerInternal.IsProjectAvailableOffline(project);

        var str = hasLocalData ? "Update" : "Import";

        using (new EditorGUI.DisabledScope(!connected))
        {
            pressed = GUILayout.Button(str);
        }

        if (hasLocalData && GUILayout.Button("Locate"))
        {
            var path = m_ProjectManagerInternal.GetProjectFolder(project);

            try
            {
                path = "Assets" + path.Replace(Application.dataPath, "");

                var id = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path).GetInstanceID();

                EditorGUIUtility.PingObject(id);
            }
            catch (Exception)
            {
                Debug.LogError($"Unable to locate Reflect folder '{path}'");
            }
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();

        return pressed;
    }
}