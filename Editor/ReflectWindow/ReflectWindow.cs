using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Grpc.Core;
using Unity.EditorCoroutines.Editor;
using Unity.Reflect;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using Unity.Reflect.Utils;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Reflect;
using UnityEngine.Reflect.Pipeline;
using UnityEngine.UIElements;

namespace UnityEditor.Reflect
{
    class ReflectWindow : EditorWindow, IUpdateDelegate
    {
        public event Action<float> update;

        ReflectEditorDownloader m_ProjectDownloader;
        ProjectsLister m_ProjectsLister;
        PlayerStorage m_Storage;

        const string k_ReflectLandingPageUrl = "https://unity.com/products/unity-reflect";

        static readonly string k_PackagePath = "Packages/com.unity.reflect";
        static readonly string k_ResourcePath = $"{k_PackagePath}/Editor/{nameof(ReflectWindow)}";
        static readonly string k_LayoutPath = k_ResourcePath + "/Layouts";

        static readonly string k_ReflectEditorWindowLayoutPath = $"{k_LayoutPath}/{nameof(ReflectWindow)}.uxml";
        static readonly string k_ProjectListItemLayoutPath = $"{k_LayoutPath}/ReflectProjectListItem.uxml";

        VisualTreeAsset m_ProjectItem;
        ScrollView m_ProjectList;
        ProgressBar m_ProgressBar;
        VisualElement m_SearchingProjectIndicator;
        HelpBox m_InfoMessage;
        HelpBox m_ErrorMessage;
        Button m_LearnMoreButton;
        ToolbarSearchField m_SearchField;
        ToolbarMenu m_SortMenu;

        static class SaveKeys
        {
            public static readonly string sortKey = "ReflectWindow_SortType";
        }

        enum SortType
        {
            Imported = 0,
            Name = 1,
            NameDesc = 2,
            LastModified = 3,
            LastModifiedDesc = 4
        }

        SortType m_SortType = SortType.Imported;

        readonly Dictionary<SortType, string> k_SortTypes = new Dictionary<SortType, string>
        {
            { SortType.Imported, "Imported" },
            { SortType.Name, "Name ↓"},
            { SortType.NameDesc, "Name ↑"},
            { SortType.LastModifiedDesc, "Last modified ↓"},
            { SortType.LastModified, "Last modified ↑"}
        };

        struct ProjectItem
        {
            public readonly Project project;
            public readonly VisualElement visualElement;
            public readonly bool imported;

            public ProjectItem(Project project, VisualElement visualElement, bool imported)
            {
                this.project = project;
                this.visualElement = visualElement;
                this.imported = imported;
            }
        }
        
        List<ProjectItem> m_Projects;

        float m_LastTimeSinceStartup = 0f;

        [MenuItem("Window/Reflect/Reflect Importer", priority = 1000)]
        static void OpenWindow()
        {
            var window = GetWindow<ReflectWindow>("Reflect Importer");
            window.minSize = new Vector2(220.0f, 163.0f);
            window.Show();
        }

        void Initialize(VisualTreeAsset root, VisualTreeAsset projectItem)
        {
            m_ProjectItem = projectItem;

            var ui = root.CloneTree();
            rootVisualElement.Add(ui);

            // Make sure the content takes the full available space.
            ui.style.width = ui.style.height = new StyleLength(new Length(100, UnityEngine.UIElements.LengthUnit.Percent));

            ui.Q<Button>("refresh-button").clicked += DiscoverProjects;

            m_ProjectList = ui.Q<ScrollView>("projects-list");

            m_ProgressBar = ui.Q<ProgressBar>("progress-bar");
            
            m_SearchingProjectIndicator = ui.Q<VisualElement>("searching-indicator");

            m_ErrorMessage = ui.Q<HelpBox>("error-message");
            m_InfoMessage = ui.Q<HelpBox>("info-message");

            m_LearnMoreButton = ui.Q<Button>("learn-more-btn");
            m_LearnMoreButton.clicked += () => Application.OpenURL(k_ReflectLandingPageUrl);

            m_SearchField = ui.Q<ToolbarSearchField>("toolbarSearch");
            m_SearchField.RegisterValueChangedCallback(OnSearchValueChanged);
            
            m_SortMenu = ui.Q<ToolbarMenu>("toolbarSort");

            foreach (var sort in k_SortTypes)
            {
                m_SortMenu.menu.AppendAction(sort.Value, 
                    a => OnSortValueChanged(sort.Key, sort.Value), 
                    a => m_SortType == sort.Key ? DropdownMenuAction.Status.Checked : DropdownMenuAction.Status.Normal);
            }
            
                        
            var sortValue = EditorPrefs.GetInt(SaveKeys.sortKey, 0);

            if (Enum.IsDefined(typeof(SortType), sortValue))
            {
                m_SortType = (SortType)sortValue;
                OnSortValueChanged(m_SortType, k_SortTypes[m_SortType]);
            }
            else
            {
                var sort = k_SortTypes.First();
                OnSortValueChanged(sort.Key, sort.Value);
            }
        }

        void OnSortValueChanged(SortType sortType, string sortName)
        {
            m_SortMenu.text = $"Sort: {sortName}";
            m_SortType = sortType;
            EditorPrefs.SetInt(SaveKeys.sortKey, (int)m_SortType);
            PopulateProjectList();
        }

        void OnSearchValueChanged(ChangeEvent<string> evt)
        {
            PopulateProjectList();
        }

        void DiscoverProjects()
        {
            m_ProjectList?.Clear();
            StopProjectDiscovery();
            StartProjectDiscovery();
        }
        
        void BindProjectItem(ProjectItem projectItem)
        {
            var project = projectItem.project;
            var e = projectItem.visualElement;
            
            e.Q<Label>("name").text = project.name;

            if (project.UnityProject.Organization != null)
            {
                e.Q<Label>("description").text = $"{project.UnityProject.Organization.Name} ({project.description})";
            }
            else
            {
                e.Q<Label>("description").text = $"Location: {project.description}";
            }

            e.Q<Label>("last-modified").text = project.lastPublished.ToString("MMMM dd, yyyy");

            var importBtn = e.Q<Button>("import-btn");
            var updateBtn = e.Q<Button>("update-btn");
            var actions = e.Q<ToolbarMenu>("toolbar-actions");
            
            importBtn.clicked += () => ImportProject(project);
            updateBtn.clicked += () => ImportProject(project);
            
            actions.menu.AppendAction("Locate", action => { LocateProject(project); });
            actions.menu.AppendAction("Extract Assets", action => { ExtractAssets(project); });

            var nonImportedBtnSet = e.Q<VisualElement>("non-imported-btn-set");
            var importedBtnSet = e.Q<VisualElement>("imported-btn-set");

            var hasLocalData = projectItem.imported;

            if (hasLocalData)
            {
                UIToolkitUtils.DisplayNone(nonImportedBtnSet);
                UIToolkitUtils.DisplayFlex(importedBtnSet);
            }
            else
            {
                UIToolkitUtils.DisplayFlex(nonImportedBtnSet);
                UIToolkitUtils.DisplayNone(importedBtnSet);
            }
            
            importBtn.SetEnabled(!hasLocalData);
            updateBtn.SetEnabled(hasLocalData);
        }

        void ImportProject(Project project)
        {
            if (CheckLogin())
            {
                m_ProjectList.SetEnabled(false);
                
                // Get AccesssToken.
                AccessTokenManager accessTokenManager = AccessTokenManager.Create(project, this);
                accessTokenManager.CreateAccessToken(project, ProjectServer.UnityUser.AccessToken, accessToken =>
                {
                    Debug.Log(accessToken.SyncServiceAccessToken);
                    EditorCoroutineUtility.StartCoroutine(m_ProjectDownloader.Download(project, accessToken, () =>
                    {
                        AccessTokenManager.Remove(project);
                    }), this);
                } );
            }
        }

        static string GetAssetsImportFolder()
        {
            return Path.Combine(Application.dataPath, "Reflect", "Imported");
        }

        string GetProjectFolder(Project project)
        {
            var path = Path.Combine(m_Storage.rootFolder, GetProjectSubFolder(project));

            if (!Directory.Exists(path))
            {
                // Try to find the folder in case it's inside a subfolder
                var legacyFolder = Path.Combine(Application.dataPath, "Reflect");

                if (Directory.Exists(legacyFolder))
                {
                    var newPath = Directory
                        .EnumerateDirectories(legacyFolder, GetProjectSubFolder(project), SearchOption.AllDirectories)
                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(newPath))
                    {
                        path = newPath.Replace("\\", "/");
                    }
                }
            }

            return path;
        }

        bool AlreadyImported(Project project)
        {
            var folder = GetProjectFolder(project);
            return Directory.Exists(folder);
        }

        static string GetProjectSubFolder(Project project)
        {
            return FileUtils.SanitizeName(project.name);
        }

        void LocateProject(Project project)
        {
            var path = GetProjectFolder(project);
            
            try
            {
                var assetPath = ToAssetsPath(path);
                var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

                EditorGUIUtility.PingObject(obj);
            }
            catch (Exception)
            {
                Debug.LogError($"Unable to locate Reflect Project {project.name} at folder '{path}'");
            }
        }
        
        void ExtractAssets(Project project)
        {
            var path = GetProjectFolder(project);

            GameObject asset = null;
            
            var syncPrefabPath = Directory.EnumerateFiles(path, $"*{SyncPrefab.Extension}", SearchOption.AllDirectories).FirstOrDefault();

            if (!string.IsNullOrEmpty(syncPrefabPath))
            {
                var assetPath = ToAssetsPath(syncPrefabPath);
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            }

            if (asset == null)
            {
                Debug.LogError($"Unable to find SyncPrefab for Reflect Project {project.name}");
                return;
            }
            
            ReflectAssetsExtractorWindow.ShowWindow(asset);
        }

        static string ToAssetsPath(string fullPath)
        {
            return Path.Combine("Assets/" + fullPath.Replace(Application.dataPath, string.Empty));
        }

        void RefreshProjects(IEnumerable<Project> projects)
        {
            if (m_Projects == null)
            {
                m_Projects = new List<ProjectItem>();
            }
            else
            {
                m_Projects.Clear(); // TODO Optimize, not need to delete and recreate everything each refresh.                
            }

            foreach (var project in projects)
            {
                var imported = m_Storage.HasData(project) || AlreadyImported(project);
                var projectItem = new ProjectItem(project,  m_ProjectItem.CloneTree(), imported);
                
                BindProjectItem(projectItem);
                m_Projects.Add(projectItem);
            }

            PopulateProjectList();
        }

        void PopulateProjectList()
        {
            if (m_Projects == null)
                return;
            
            var projectItems = ApplyFilters(m_Projects, m_SearchField.value);
            projectItems = ApplySorting(projectItems, m_SortType);

            m_ProjectList.Clear();
            foreach (var projectItem in projectItems)
            {
                m_ProjectList.Add(projectItem.visualElement);
            }
        }

        // TODO In a thread?
        static IEnumerable<ProjectItem> ApplyFilters(IEnumerable<ProjectItem> projectItems, string filter)
        {
            return string.IsNullOrWhiteSpace(filter) ? projectItems 
                : projectItems.Where(projectItem => Regex.IsMatch(projectItem.project.name, filter, RegexOptions.IgnoreCase)); // TODO Use fuzzy search instead?
        }
        
        // TODO In a thread?
        static IEnumerable<ProjectItem> ApplySorting(IEnumerable<ProjectItem> projectItems, SortType sortType)
        {
            switch (sortType)
            {
                case SortType.Name:
                    return projectItems.OrderBy(item => item.project.name);
                
                case SortType.NameDesc:
                    return projectItems.OrderByDescending(item => item.project.name);

                case SortType.LastModifiedDesc:
                    return projectItems.OrderByDescending(item => item.project.lastPublished);
                
                case SortType.LastModified:
                    return projectItems.OrderBy(item => item.project.lastPublished);
                
                case SortType.Imported:
                default:
                    return projectItems.OrderBy(item => !item.imported).ThenBy(item => item.project.name);
            }
        }

        void OnEnable()
        {
            var uiAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_ReflectEditorWindowLayoutPath);
            var uiProjectAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_ProjectListItemLayoutPath);

            Initialize(uiAsset, uiProjectAsset);

            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                OnPlayModeStateChanged(PlayModeStateChange.EnteredPlayMode);
            }
            else
            {
                DiscoverProjects();
            }

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += OnUpdate;
        }

        void OnDisable()
        {
            ProjectServer.Cleanup();
            StopProjectDiscovery();
            
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
        }

        void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                ShowInfo("Reflect import features are unavailable during play mode.");
                UIToolkitUtils.DisplayNone(m_SearchingProjectIndicator);
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                DiscoverProjects();
            }
        }

        void UpdateProgressBar(float v)
        {
            if (v < 1.0f)
            {
                UIToolkitUtils.Show(m_ProgressBar);
                m_ProgressBar.title = "Downloading";
                m_ProgressBar.value = v;
            }
            else
            {
                UIToolkitUtils.Hide(m_ProgressBar);
                m_ProgressBar.title = string.Empty;
                m_ProgressBar.value = 0.0f;
            }
        }

        void StartProjectDiscovery()
        {
            var fullImportFolder = GetAssetsImportFolder();

            if (m_Storage == null)
            {
                m_Storage = new PlayerStorage(fullImportFolder, false, true);
            }
            
            if (m_ProjectsLister == null)
            { 
                var authClient = new AuthClient(ProjectServer.UnityUser);

                m_ProjectsLister = new ProjectsLister(authClient);

                m_ProjectsLister.projectListingCompleted += OnProjectRefreshCompleted;
                m_ProjectsLister.onException += ShowError;
                
                m_ProjectsLister.SetUpdateDelegate(this);
            }
            
            if (m_ProjectDownloader == null)
            {
                m_ProjectDownloader = new ReflectEditorDownloader(m_Storage);

                m_ProjectDownloader.onProgressChanged += f =>
                {
                    UpdateProgressBar(f);
                    
                    if (f >= 1.0f)
                    {
                        AssetDatabase.Refresh();
                        m_ProjectList.SetEnabled(true);
                        
                        RefreshProjects(m_Projects.Select(item => item.project).ToList()); // TODO Optimize. We only need to update the status of the project items buttons.
                    }
                };

                m_ProjectDownloader.onError += ShowError;
            }

            if (!CheckLogin())
            {
                return;
            }

            OnProjectRefreshStarted();
            ProjectServer.Init();
            m_ProjectsLister.Run();
        }

        bool CheckLogin()
        {
            if (string.IsNullOrEmpty(CloudProjectSettings.accessToken))
            {
                ShowError("A valid Unity User session is required to access Reflect services. Please Signin with the Unity Hub.");
                return false;
            }

            return true;
        }
        
        void ShowError(string msg)
        {
            UIToolkitUtils.DisplayNone(m_SearchingProjectIndicator);
            
            m_ErrorMessage.text = msg;
            UIToolkitUtils.DisplayFlex(m_ErrorMessage);
            UIToolkitUtils.DisplayNone(m_LearnMoreButton);
        }

        void ShowError(Exception exception)
        {
            var msg = exception is RpcException rpcException ? rpcException.Status.Detail : ExtractInnerException(exception).Message;
            ShowError($"Error: {msg}");

            if (exception is ProjectListRefreshException ex && ex.Status == UnityProjectCollection.StatusOption.ComplianceError)
            {
                UIToolkitUtils.DisplayFlex(m_LearnMoreButton);
            }

            Debug.LogError(msg);
        }
        
        void HideError()
        {
            UIToolkitUtils.DisplayNone(m_ErrorMessage);
        }
        
        void ShowInfo(string msg)
        {
            m_InfoMessage.text = msg;
            UIToolkitUtils.DisplayFlex(m_InfoMessage);
        }
        
        void HideInfo()
        {
            UIToolkitUtils.DisplayNone(m_InfoMessage);
        }

        void OnProjectRefreshStarted()
        {
            UIToolkitUtils.DisplayFlex(m_SearchingProjectIndicator);
            HideError();
            HideInfo();
        }

        void OnProjectRefreshCompleted(IEnumerable<Project> projects)
        {
            UIToolkitUtils.DisplayNone(m_SearchingProjectIndicator);
            RefreshProjects(projects);
            
            StopProjectDiscovery();
        }

        void StopProjectDiscovery()
        {
            if (m_ProjectsLister != null)
            {
                m_ProjectsLister.RemoveUpdateDelegate(this);
                m_ProjectsLister.Dispose();
                m_ProjectsLister = null;
            }
        }

        void OnUpdate()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (m_LastTimeSinceStartup == 0f)
                {
                    m_LastTimeSinceStartup = (float)EditorApplication.timeSinceStartup;
                }
                
                update?.Invoke((float)EditorApplication.timeSinceStartup - m_LastTimeSinceStartup);
                m_LastTimeSinceStartup = (float) EditorApplication.timeSinceStartup;
            }
        }

        static Exception ExtractInnerException(Exception exception)
        {
            return exception.InnerException == null ? exception : ExtractInnerException(exception.InnerException);
        }
    }
}