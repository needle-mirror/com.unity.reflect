using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.IO;

namespace UnityEngine.Reflect.Pipeline
{
    [Serializable]
    public class ProjectDeleterSettings
    {
        public Project deleteProject;

        public event Action<Project> projectLocalDataDeleted;
        public event Action<Project> projectDeleteCanceled;
        public event Action<int, int, string> projectDeleteProgressChanged;

        public void ProjectDeleteCompleted()
        {
            projectLocalDataDeleted?.Invoke(deleteProject);
        }

        public void ProjectDeleteCanceled()
        {
            projectDeleteCanceled?.Invoke(deleteProject);
        }

        public void ProgressChanged(int progress, int total, string message)
        {
            projectDeleteProgressChanged?.Invoke(progress, total, message);
        }
    }

    public class ProjectDeleter : ReflectTask
    {
        readonly ProjectDeleterSettings m_Settings;
        readonly PlayerStorage m_Storage;
        
        const string k_Deleting = "Deleting";

        public IProjectProvider client { get; set; }

        public ProjectDeleter(ProjectDeleterSettings settings, PlayerStorage storage)
        {
            m_Settings = settings;
            m_Storage = storage;
        }
        
        protected override Task RunInternal(CancellationToken token)
        {
            return DeleteProjectLocally(token, m_Settings.deleteProject);
        }
        
        protected override void UpdateInternal(float unscaledDeltaTime)
        {
            if (m_Task == null)
                return;

            if (m_TotalCount != 0)
                m_Settings.ProgressChanged(m_CurrentCount, m_TotalCount, k_Deleting);

            if (!m_Task.IsCompleted)
                return;

            m_Settings.ProjectDeleteCompleted(); 

            m_Task = null;
        }
        
        int m_TotalCount;
        int m_CurrentCount;
        Task DeleteProjectLocally(CancellationToken token, UnityProject project)
        {
            m_CurrentCount = 0;
            m_TotalCount = 0;
            
            var projectFolderPath = m_Storage.GetProjectFolder(project);
            if (!Directory.Exists(projectFolderPath))
            {
                Debug.LogWarning($"Cannot delete locally stored project '{project.ProjectId}'");
                return Task.CompletedTask;
            }

            // Deleting each file individually is slow. Instead, get all leaf directories and delete them one after the other.
            var projectDirectories = Directory
                .EnumerateDirectories(projectFolderPath, "*.*", SearchOption.AllDirectories)
                .Where(f => !Directory.EnumerateDirectories(f, "*.*", SearchOption.TopDirectoryOnly).Any()).ToList();

            m_TotalCount = projectDirectories.Count;

            for (int i = 0; i < projectDirectories.Count; ++i)
            {
                token.ThrowIfCancellationRequested();
                Directory.Delete(projectDirectories[i], true);
                m_CurrentCount++;
            }

            Directory.Delete(projectFolderPath, true);
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            base.Dispose();
            m_Settings.ProjectDeleteCanceled();
        }
    }
}