using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.IO;

namespace UnityEngine.Reflect.Pipeline
{
    public class ProjectDeleterSettings
    {
        public Project project;

        public event Action<Project> projectDeleteCompleted;
        public event Action<Project> projectDeleteCanceled;
        
        public event ProgressChanged projectDeleteProgressChanged;

        public void InvokeDeleteCompleted()
        {
            projectDeleteCompleted?.Invoke(project);
        }

        public void InvokeDeleteCanceled()
        {
            projectDeleteCanceled?.Invoke(project);
        }

        public void InvokeProgressChanged(int progress, int total)
        {
            projectDeleteProgressChanged?.Invoke(project, progress, total);
        }
    }

    public class ProjectDeleter : ReflectTask
    {
        readonly ProjectDeleterSettings m_Settings;
        readonly PlayerStorage m_Storage;

        public ProjectDeleter(ProjectDeleterSettings settings, IUpdateDelegate updateDelegate, PlayerStorage storage)
        {
            m_Settings = settings;
            m_Storage = storage;
            SetUpdateDelegate(updateDelegate);
        }
        
        protected override Task RunInternal(CancellationToken token)
        {
            return DeleteProjectLocally(token, m_Settings.project);
        }

        protected override void UpdateInternal(float unscaledDeltaTime)
        {
            if (m_Task == null)
                return;

            if (m_TotalCount != 0)
                m_Settings.InvokeProgressChanged(m_CurrentCount, m_TotalCount);

            if (!m_Task.IsCompleted)
                return;

            m_Settings.InvokeDeleteCompleted(); 

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
    }
}