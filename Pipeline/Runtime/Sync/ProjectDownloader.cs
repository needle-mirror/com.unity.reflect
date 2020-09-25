using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.IO;

namespace UnityEngine.Reflect.Pipeline
{
    [Serializable]
    public class ProjectDownloaderSettings
    {
        public Project downloadProject;

        public event Action<Project> projectDataDownloaded;
        public event Action<Project> projectDownloadCanceled;
        public event Action<int, int, string> projectDownloadProgressChanged;

        public int maxTaskSize => k_MaxTaskSize;
        const int k_MaxTaskSize = 100;

        public void ProjectDownloadCompleted()
        {
            projectDataDownloaded?.Invoke(downloadProject);
        }

        public void ProjectDownloadCanceled()
        {
            projectDownloadCanceled?.Invoke(downloadProject);
        }

        public void ProgressChanged(int progress, int total, string message)
        {
            projectDownloadProgressChanged?.Invoke(progress, total, message);
        }
    }

    public class ProjectDownloader : ReflectTask
    {
        readonly ProjectDownloaderSettings m_Settings;
        readonly PlayerStorage m_PlayerStorage;
        readonly IUpdateDelegate m_UpdateDelegate;
        readonly UnityUser m_User;
        
        ReflectClient m_Client;
        
        DateTime m_Time;
        
        const string k_Downloading = "Downloading";

        public ProjectDownloader(ProjectDownloaderSettings settings, IUpdateDelegate updateDelegate, UnityUser user, PlayerStorage playerStorage)
        {
            m_Settings = settings;
            m_PlayerStorage = playerStorage;
            m_UpdateDelegate = updateDelegate;
            m_User = user;
            
            SetUpdateDelegate(updateDelegate);
        }

        public override void Run()
        {
            m_Time = DateTime.Now;
            m_Client = new ReflectClient(m_UpdateDelegate, m_User, m_PlayerStorage, m_Settings.downloadProject);
            base.Run();
        }

        protected override Task RunInternal(CancellationToken token)
        {
            m_PlayerStorage.SaveProjectData(m_Settings.downloadProject);
            return DownloadProject(token, m_Client);
        }
        
        protected override void UpdateInternal(float unscaledDeltaTime)
        {
            if (m_Task == null)
                return;
            
            if(m_TotalCount != 0)
                m_Settings.ProgressChanged(m_CurrentCount, m_TotalCount, k_Downloading);

            if (!m_Task.IsCompleted)
                return;

            m_Settings.ProjectDownloadCompleted(); 
            
            Debug.Log("Download Done " + (DateTime.Now - m_Time).TotalMilliseconds + " MS");

            m_Client?.Dispose();
            m_Task = null;
        }

        public override void Dispose()
        {
            if (m_Task != null && !m_Task.IsCompleted)
            {
                m_Settings.ProjectDownloadCanceled();
            }

            m_Client?.Dispose();
            base.Dispose();
        }

        int m_TotalCount;
        int m_CurrentCount;
        async Task DownloadProject(CancellationToken token, ReflectClient reflectClient)
        {
            m_CurrentCount = 0;
            m_TotalCount = 0;
            var manifests = await reflectClient.GetSyncManifestsAsync();

            m_TotalCount = manifests.Sum(e => e.Content.Count);

            var currentTasks = new List<Task>();
            foreach (var manifest in manifests)
            {
                foreach (var content in manifest.Content)
                {
                    token.ThrowIfCancellationRequested();
                    var streamKey = new StreamKey(manifest.SourceId, content.Key);
                    currentTasks.Add(reflectClient.DownloadSyncModelAsync(streamKey, content.Value.Hash));
                    if (currentTasks.Count < m_Settings.maxTaskSize)
                    {
                        continue;
                    }
            
                    var completedTask = await Task.WhenAny(currentTasks);
                    currentTasks.Remove(completedTask);
                    m_CurrentCount++;
                }
            }

            while (currentTasks.Count < 0)
            {
                token.ThrowIfCancellationRequested();
                var completedTask = await Task.WhenAny(currentTasks);
                currentTasks.Remove(completedTask);
                m_CurrentCount++;
            }
        }
    }
}