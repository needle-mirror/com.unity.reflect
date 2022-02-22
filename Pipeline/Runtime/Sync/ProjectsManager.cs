using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect;
using Unity.Reflect.IO;

namespace UnityEngine.Reflect.Pipeline
{
    public class ProjectsManager : IDisposable
    {
        enum TaskStatus
        {
            None,
            Queued,
            Started,
            Canceled,
            Completed,
        }
        
        abstract class TaskQueue<T> where T : ReflectTask
        {
            public event Action<Project, TaskStatus> statusChanged;
            public event ProgressChanged progressChanged;

            public int maxSimultaneousTasks { get; set; } = 1;

            readonly Dictionary<string, T> m_Tasks = new Dictionary<string, T>();
            readonly Dictionary<string, TaskStatus> m_Statues = new Dictionary<string, TaskStatus>();

            Queue<Project> m_Queue = new Queue<Project>();
            
            public bool StartTask(Project project)
            {
                if (m_Tasks.TryGetValue(project.projectId, out var task))
                {
                    if (task != null && task.IsRunning)
                        return false;
                }
                
                if (CurrentTaskCount() >= maxSimultaneousTasks)
                {
                    m_Queue.Enqueue(project);
                    SetStatus(project, TaskStatus.Queued);
                    Debug.Log($"{this}: Simultaneous task limit reached. Queuing project '{project.name}'");
                    return false;
                }

                if (task == null)
                {
                    m_Tasks[project.projectId] = task = CreateTask(project, OnCompleted, OnCanceled, OnProgressChanged);
                }
                
                SetStatus(project, TaskStatus.Started);
                task.Run();
                return true;
            }

            protected abstract T CreateTask(Project project, 
                Action<Project> completedCallback, 
                Action<Project> canceledCallback,
                ProgressChanged progressChangedCallback);

            void OnProgressChanged(Project project, int progress, int total)
            {
                progressChanged?.Invoke(project, progress, total);
            }

            void OnCanceled(Project project)
            {
                SetStatus(project, TaskStatus.Canceled);
                DisposeTask(project);
                StartNext();
            }

            void OnCompleted(Project project)
            {
                SetStatus(project, TaskStatus.Completed);
                DisposeTask(project);
                StartNext();
            }

            void DisposeTask(Project project)
            {
                if (m_Tasks.TryGetValue(project.projectId, out var task))
                {
                    task.Dispose();
                    m_Tasks[project.projectId] = null;
                }
            }

            public TaskStatus GetStatus(Project project)
            {
                if (m_Statues.TryGetValue(project.projectId, out var status))
                    return status;

                return TaskStatus.None;
            }

            void StartNext()
            {
                if (m_Queue.Count > 0)
                {
                    var project = m_Queue.Dequeue();
                    StartTask(project);
                }
            }

            void SetStatus(Project project, TaskStatus status)
            {
                m_Statues[project.projectId] = status;
                statusChanged?.Invoke(project, status);
            }

            int CurrentTaskCount()
            {
                return m_Tasks.Values.Count(d => d is {IsRunning: true});
            }
            
            public void Dispose()
            {
                foreach (var task in m_Tasks.Values)
                {
                    task?.Dispose();
                }

                m_Tasks.Clear();
            }
        }

        class DownloaderQueue : TaskQueue<ProjectDownloader>
        {
            IUpdateDelegate m_UpdateDelegate;
            UnityUser m_User;
            PlayerStorage m_PlayerStorage;

            public DownloaderQueue(IUpdateDelegate updateDelegate, UnityUser user, PlayerStorage playerStorage)
            {
                m_UpdateDelegate = updateDelegate;
                m_User = user;
                m_PlayerStorage = playerStorage;
            }
            
            protected override ProjectDownloader CreateTask(Project project,
                Action<Project> completedCallback,
                Action<Project> canceledCallback,
                ProgressChanged progressChangedCallback)
            {
                var settings = new ProjectDownloaderSettings { project = project };
                settings.projectDownloadCompleted += completedCallback;
                settings.projectDownloadCanceled += canceledCallback;
                settings.projectDownloadProgressChanged += progressChangedCallback;
                    
                return new ProjectDownloader(settings, m_UpdateDelegate, m_User, m_PlayerStorage);
            }
        }
        
        class DeleterQueue : TaskQueue<ProjectDeleter>
        {
            IUpdateDelegate m_UpdateDelegate;
            PlayerStorage m_PlayerStorage;

            public DeleterQueue(IUpdateDelegate updateDelegate, PlayerStorage playerStorage)
            {
                m_UpdateDelegate = updateDelegate;
                m_PlayerStorage = playerStorage;
            }
            
            protected override ProjectDeleter CreateTask(Project project,
                Action<Project> completedCallback,
                Action<Project> canceledCallback,
                ProgressChanged progressChangedCallback)
            {
                var settings = new ProjectDeleterSettings { project = project };
                settings.projectDeleteCompleted += completedCallback;
                settings.projectDeleteCanceled += canceledCallback;
                settings.projectDeleteProgressChanged += progressChangedCallback;
                    
                return new ProjectDeleter(settings, m_UpdateDelegate, m_PlayerStorage);
            }
        }

        public enum Status
        {
            Unknown,
            QueuedForDownload,
            Downloading,
            Downloaded,
            QueuedForDelete,
            Deleting,
            Deleted
        }
    
        public event Action<Project, Status> projectStatusChanged;
        
        public event ProgressChanged projectDownloadProgressChanged;
        public event ProgressChanged projectDeleteProgressChanged;

        readonly DownloaderQueue m_DownloaderQueue;
        readonly DeleterQueue m_DeleterQueue;
        
        readonly Dictionary<string, Status> m_Statues = new Dictionary<string, Status>();
        
        public ProjectsManager(IUpdateDelegate updateDelegate, UnityUser user, PlayerStorage playerStorage)
        {
            m_DownloaderQueue = new DownloaderQueue(updateDelegate, user, playerStorage);
            m_DownloaderQueue.progressChanged += OnDownloadProgressChanged;
            m_DownloaderQueue.statusChanged += OnDownloadStatusChanged;

            m_DeleterQueue = new DeleterQueue(updateDelegate, playerStorage);
            m_DeleterQueue.progressChanged += OnDeleteProgressChanged;
            m_DeleterQueue.statusChanged += OnDeleteStatusChanged;
        }

        public void Download(Project project)
        {
            // TODO check for proper status
            m_DownloaderQueue.StartTask(project);
        }

        public void Delete(Project project)
        {
            // TODO check for proper status
            m_DeleterQueue.StartTask(project);
        }

        public Status GetStatus(Project project)
        {
            if (m_Statues.TryGetValue(project.projectId, out var status))
                return status;

            return Status.Unknown;
        }

        public void Dispose()
        {
            m_DownloaderQueue.Dispose();
            m_DeleterQueue.Dispose();
        }

        void SetStatus(Project project, Status newStatus)
        {
            if (m_Statues.TryGetValue(project.projectId, out var currentStatus))
            {
                if (currentStatus == newStatus)
                    return;
            }

            m_Statues[project.projectId] = newStatus;
            projectStatusChanged?.Invoke(project, newStatus);
        }
        
        void OnDeleteStatusChanged(Project project, TaskStatus taskStatus)
        {
            var status = Status.Unknown;
            
            switch (taskStatus)
            {
                case TaskStatus.Queued:
                    status = Status.QueuedForDelete;
                    break;
                
                case TaskStatus.Started:
                    status = Status.Deleting;
                    break;
                
                case TaskStatus.Completed:
                    status = Status.Deleted;
                    project.IsLocal = false;
                    break;
            }

            SetStatus(project, status);
        }

        void OnDeleteProgressChanged(Project project, int progress, int total)
        {
            SetStatus(project, Status.Deleting);
            projectDeleteProgressChanged?.Invoke(project, progress, total);
        }

        void OnDownloadStatusChanged(Project project, TaskStatus taskStatus)
        {
            var status = Status.Unknown;
            
            switch (taskStatus)
            {
                case TaskStatus.Queued:
                    status = Status.QueuedForDownload;
                    break;
                
                case TaskStatus.Started:
                    status = Status.Downloading;
                    break;

                case TaskStatus.Completed:
                    status = Status.Downloaded;
                    project.IsLocal = true;
                    
                    // TODO Load project.data to get actual publish date.
                    // Similar to PlayerStorage.GetLocalProjects
                    project.DownloadedPublished = project.lastPublished;
                    break;
            }

            SetStatus(project, status);
        }

        void OnDownloadProgressChanged(Project project, int progress, int total)
        {
            SetStatus(project, Status.Downloading);
            projectDownloadProgressChanged?.Invoke(project, progress, total);
        }
    }
}
