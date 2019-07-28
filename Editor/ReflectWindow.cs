using System;
using System.IO;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Reflect.Services;

class ReflectWindow : EditorWindow
{
    [SerializeField]
    string m_ImportFolder = "Reflect";
    
    ProjectManagerInternal m_ProjectManagerInternal;
    
    Vector2 m_ScrollPosition;

    string m_TaskInProgressName;
    float m_TaskProgress;

    bool m_ShowAllProjects;

    DateTime m_LastUpdate;
    const double k_UpdateIntervalMs = 1000.0;

    string m_FullImportFolder;
    
    static GUIStyle s_HeaderStyle;

    [MenuItem("Window/Reflect")]
    static void OpenWindow()
    {
        var window = GetWindow<ReflectWindow>("Reflect");
        window.Show();
    }

    void OnEnable()
    {        
        StartProjectDiscovery();

        EditorApplication.update += OnEditorUpdate;
    }
    
    void OnDisable()
    {        
        StopProjectDiscovery();
        
        EditorApplication.update -= OnEditorUpdate;
    }

    void StartProjectDiscovery()
    {
        if (m_ProjectManagerInternal == null)
        {
            m_FullImportFolder = Path.Combine(Application.dataPath, m_ImportFolder);
            m_ProjectManagerInternal = new ProjectManagerInternal(m_FullImportFolder, true);
            
            m_ProjectManagerInternal.progressChanged += (f, s) =>
            {
                m_TaskProgress = f;
                m_TaskInProgressName = s;
            };
        
            m_ProjectManagerInternal.taskCompleted += () => {
                m_TaskInProgressName = null;
                m_TaskProgress = 0.0f;
                AssetDatabase.Refresh(); };

            m_TaskInProgressName = null;
            m_TaskProgress = 0.0f;
        }

        m_ProjectManagerInternal.OnEnable();
        
        m_ProjectManagerInternal.StartDiscovery();
    }

    void StopProjectDiscovery()
    {
        if (m_ProjectManagerInternal != null)
        {
            m_ProjectManagerInternal.StopDiscovery();

            m_ProjectManagerInternal.OnDisable();

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
            m_ProjectManagerInternal.Update();
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

        if (m_ProjectManagerInternal == null)
        {
            StartProjectDiscovery();
        }

        m_ShowAllProjects = EditorGUILayout.Toggle("Show All Projects", m_ShowAllProjects);
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Projects", s_HeaderStyle);
        
        var projects = m_ProjectManagerInternal.Projects;

        m_ScrollPosition = EditorGUILayout.BeginScrollView(m_ScrollPosition);
        
        EditorGUILayout.BeginVertical();

        var projectFound = false;
        
        var taskInProgress = !string.IsNullOrEmpty(m_TaskInProgressName);
        
        if (projects.Any())
        {
            foreach (var project in projects)
            {
                if (!m_ShowAllProjects && !IsCurrentProject(project))
                    continue;

                projectFound = true;
                
                using (new EditorGUI.DisabledScope(taskInProgress))
                {
                    if (ProjectGUI(project))
                    {
                        EditorCoroutineUtility.StartCoroutine(m_ProjectManagerInternal.DownloadProjectLocally(project.serverProjectId, true), this);
                    }
                }
            }
        }
        
        if (!projectFound)
        {
            EditorGUILayout.LabelField("Gathering available projects...");
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
            
                var id = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>( path ).GetInstanceID();
            
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

    static bool IsCurrentProject(Project project)
    {
        return Application.dataPath.Contains(project.name); // Hack. Instead, get the project id from the Hub and compare or add a ProjectManager.GetProject(id) API.
    }
}