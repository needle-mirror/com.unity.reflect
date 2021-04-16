using System;
using Unity.Reflect;
using Unity.Reflect.IO;
using UnityEngine.Events;

namespace UnityEngine.Reflect.Pipeline
{
    public static class ReflectPipelineFactory
    {
        static ProjectListerSettings s_ProjectListerSettings;
        static ProjectsLister s_ProjectLister;
        static ProjectDeleterSettings s_ProjectDeleterSettings;
        static ProjectDeleter s_ProjectDeleter;
        static ProjectDownloaderSettings s_ProjectDownloaderSettings;
        static ProjectDownloader s_ProjectDownloader;

        public static ProjectListerSettings.ProjectsEvents projectsRefreshCompleted = new ProjectListerSettings.ProjectsEvents();
        
        public static event Action<Project> projectLocalDataDeleted;
        public static event Action<Project> projectDeleteCanceled;
        public static event Action<int, int, string> projectDeleteProgressChanged;
        
        public static event Action<Project> projectDataDownloaded;
        public static event Action<Project> projectDownloadCanceled;
        public static event Action<int, int, string> projectDownloadProgressChanged;

        public static UnityEvent manifestUpdated { get; }

        static ReflectPipelineFactory()
        {
            manifestUpdated = new UnityEvent();
        }

        public static void SetUser(UnityUser user, IUpdateDelegate updater, IProjectProvider client, PlayerStorage storage) // TODO Rename or move into a ProjectLister manager class
        {
            // ProjectLister
            s_ProjectListerSettings = new ProjectListerSettings();
            s_ProjectListerSettings.OnProjectsRefreshCompleted = new ProjectListerSettings.ProjectsEvents();
            s_ProjectListerSettings.OnProjectsRefreshStarted = new UnityEvent();
            s_ProjectListerSettings.OnProjectsRefreshCompleted.AddListener( (projects) =>
            {
                projectsRefreshCompleted?.Invoke(projects);
            });

            s_ProjectLister = new ProjectsLister(s_ProjectListerSettings) {client = client};
            s_ProjectLister.SetUpdateDelegate(updater);
            
            s_ProjectDeleterSettings = new ProjectDeleterSettings();
            s_ProjectDeleterSettings.projectLocalDataDeleted += OnProjectLocalDataDeleted;
            s_ProjectDeleterSettings.projectDeleteCanceled += OnProjectDeleteCanceled;
            s_ProjectDeleterSettings.projectDeleteProgressChanged += OnProjectDeleteProgressChanged;
            s_ProjectDeleter = new ProjectDeleter(s_ProjectDeleterSettings, storage) {client = client};
            s_ProjectDeleter.SetUpdateDelegate(updater);
            
            
            s_ProjectDownloaderSettings = new ProjectDownloaderSettings();
            s_ProjectDownloaderSettings.projectDataDownloaded += OnProjectDataDownloaded;
            s_ProjectDownloaderSettings.projectDownloadCanceled += OnProjectDownloadCanceled;
            s_ProjectDownloaderSettings.projectDownloadProgressChanged += OnProjectDownloadProgressChanged;
            s_ProjectDownloader = new ProjectDownloader(s_ProjectDownloaderSettings, updater, user, storage);
        }

        static void OnProjectDeleteProgressChanged(int progress, int total, string message)
        {
            projectDeleteProgressChanged?.Invoke(progress, total, message);
        }

        static void OnProjectLocalDataDeleted(Project project)
        {
            projectLocalDataDeleted?.Invoke(project);
        }

        static void OnProjectDeleteCanceled(Project project)
        {
            projectDeleteCanceled?.Invoke(project);
        }
        
        static void OnProjectDownloadProgressChanged(int progress, int total, string message)
        {
            projectDownloadProgressChanged?.Invoke(progress, total, message);
        }

        static void OnProjectDataDownloaded(Project project)
        {
            projectDataDownloaded?.Invoke(project);
        }

        static void OnProjectDownloadCanceled(Project project)
        {
            projectDownloadCanceled?.Invoke(project);
        }
        
        public static void ClearUser() 
        {
            s_ProjectLister?.Dispose();
            s_ProjectLister = null;
            s_ProjectListerSettings = null;
            
            s_ProjectDeleter?.Dispose();
            s_ProjectDeleter = null;
            s_ProjectDeleterSettings = null;
            
            s_ProjectDownloader?.Dispose();
            s_ProjectDownloader = null;
            s_ProjectDownloaderSettings = null;
        }

        public static void RefreshProjects()
        {
            s_ProjectLister?.Run();
        }

        public static void DownloadProject(Project project)
        {
            s_ProjectDownloaderSettings.downloadProject = project;
            s_ProjectDownloader?.Run();
        }

        public static void DisposeDownloadProject()
        {
            s_ProjectDownloader.Dispose();
        }

        public static void DeleteProjectLocally(Project project)
        {
            s_ProjectDeleterSettings.deleteProject = project;
            s_ProjectDeleter?.Run();
        }

        public static bool HasLocalData(Project project)
        {
            //TODO Need to find other way to get playerStorage or move this method to other 
            var storage = new PlayerStorage(UnityEngine.Reflect.ProjectServer.ProjectDataPath, true, false);
            return storage.HasLocalData(project);
        }
    }
}
