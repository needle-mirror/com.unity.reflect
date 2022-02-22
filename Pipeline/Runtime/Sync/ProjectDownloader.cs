using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    public delegate void StatusChanged(Project project, ProjectsManager.Status status);
    public delegate void ProgressChanged(Project project, int progress, int total);
    
    public class ProjectDownloaderSettings
    {
        public Project project;

        public event Action<Project> projectDownloadCompleted;
        public event Action<Project> projectDownloadCanceled;

        
        public event ProgressChanged projectDownloadProgressChanged;

        public int maxTaskSize => k_MaxTaskSize;
        const int k_MaxTaskSize = 100;

        public void InvokeDownloadCompleted()
        {
            projectDownloadCompleted?.Invoke(project);
        }

        public void InvokeDownloadCanceled()
        {
            projectDownloadCanceled?.Invoke(project);
        }

        public void InvokeProgressChanged(int progress, int total)
        {
            projectDownloadProgressChanged?.Invoke(project, progress, total);
        }
    }

    public class ProjectDownloader : ReflectTask, IUpdateDelegate
    {
        readonly ProjectDownloaderSettings m_Settings;
        readonly PlayerStorage m_PlayerStorage;
        readonly IUpdateDelegate m_UpdateDelegate;
        readonly UnityUser m_User;
        
        ReflectClient m_Client;
        
        DateTime m_Time;
        bool m_IsDisposed;

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

            var accessTokenManager = AccessTokenManager.Create(m_Settings.project, this);
            accessTokenManager.CreateAccessToken(m_Settings.project, m_User.AccessToken, accessToken =>
            {
                m_Client = new ReflectClient(m_UpdateDelegate, m_User, m_PlayerStorage, m_Settings.project, accessToken);
                base.Run();
            } );
        }

        protected override Task RunInternal(CancellationToken token)
        {
            m_PlayerStorage.SaveProjectData(m_Settings.project);
            return DownloadProject(token);
        }

        protected override void UpdateInternal(float unscaledDeltaTime)
        {
            update?.Invoke(unscaledDeltaTime);

            if (m_Task == null)
                return;

            if (m_TotalCount != 0)
                m_Settings.InvokeProgressChanged(m_CurrentCount, m_TotalCount);

            if (!m_Task.IsCompleted)
                return;

            m_Settings.InvokeDownloadCompleted();

            Debug.Log("Download Done " + (DateTime.Now - m_Time).TotalMilliseconds + " MS");

            AccessTokenManager.Remove(m_Settings.project);

            m_Client?.Dispose();
            m_Task = null;
        }

        public override void Dispose()
        {
            if (m_IsDisposed)
                return;

            m_IsDisposed = true;
            m_Client?.Dispose();
            base.Dispose();

            if (m_Task != null && !m_Task.IsCompleted)
                m_Settings.InvokeDownloadCanceled();
        }

        int m_TotalCount;
        int m_CurrentCount;
        async Task DownloadProject(CancellationToken token)
        {
            var success = await DownloadSpatializedProject(token);
            if (!success)
            {
                await DownloadProjectLegacy(token);
            }
        }

        async Task<bool> DownloadSpatializedProject(CancellationToken token)
        {
            var currentManifest = await m_Client.DownloadSpatialManifestAsync(new List<SyncId>(), new GetNodesOptions(), token);
            if (currentManifest == null)
                return false;

            var getManifestOptions = new GetManifestOptions { IncludeSpatializedModels = false, IncludeModelPaths = false };
            await m_Client.GetSyncManifestsAsync(getManifestOptions, token);

            var manifests = new ConcurrentQueue<SyncManifest>();
            var cts = new CancellationTokenSource();

            currentManifest.SyncManifests.ForEach(x => manifests.Enqueue(x));
            var downloadTask = Task.Run(() => DownloadUntilStoppedAsync(manifests, cts.Token), token);

            try
            {
                var nextNodes = currentManifest.SyncNodes
                        .SelectMany(x => x.ChildNodeIds)
                        .Distinct()
                        .ToList();

                var getNodesOptions = new GetNodesOptions { Version = currentManifest.VersionId };
                do
                {
                    token.ThrowIfCancellationRequested();

                    var nbNodes = Math.Min(nextNodes.Count, 256); // 256 is hardcoded in the server for now
                    currentManifest = await m_Client.DownloadSpatialManifestAsync(nextNodes.GetRange(0, nbNodes), getNodesOptions, token);
                    currentManifest.SyncManifests.ForEach(x => manifests.Enqueue(x));
                    
                    nextNodes.RemoveRange(0, nbNodes);
                    nextNodes.AddRange(currentManifest.SyncNodes
                        .SelectMany(x => x.ChildNodeIds)
                        .Distinct());

                    m_TotalCount += currentManifest.SyncManifests.Sum(x => x.Content.Count);

                } while (nextNodes.Count > 0);
            }
            catch(Exception)
            {
                cts.Cancel();
                try
                {
                    await downloadTask;
                }
                catch
                {
                    // Discard
                }
                cts.Dispose();
                throw;
            }

            while (m_RunningTasks != 0)
            {
                if (downloadTask.IsCompleted)
                    break;

                await Task.Delay(10, token);
            }

            cts.Cancel();
            cts.Dispose();

            return true;
        }

        async Task DownloadProjectLegacy(CancellationToken token)
        {
            m_CurrentCount = 0;
            m_TotalCount = 0;

            var manifests = await m_Client.GetSyncManifestsAsync(token);
            m_TotalCount = manifests.Sum(e => e.Content.Count);

            var tasks = new List<Task>();
            await DownloadManifestsContent(tasks, manifests, token);

            await WaitForTasksInList(tasks, token);
        }

        volatile int m_RunningTasks;
        async Task DownloadUntilStoppedAsync(ConcurrentQueue<SyncManifest> manifests, CancellationToken token)
        {
            var tasks = new List<Task>();
            while (!token.IsCancellationRequested)
            {
                Task completedTask;
                while (manifests.TryDequeue(out var manifest))
                {
                    foreach (var content in manifest.Content)
                    {
                        token.ThrowIfCancellationRequested();
                        
                        var streamKey = new StreamKey(manifest.SourceId, content.Key);
                        tasks.Add(m_Client.DownloadSyncModelAsync(streamKey, content.Value.Hash, token));
                        ++m_RunningTasks;

                        if (tasks.Count >= m_Settings.maxTaskSize)
                        {
                            completedTask = await Task.WhenAny(tasks);
                            tasks.Remove(completedTask);
                            --m_RunningTasks;
                            ++m_CurrentCount;
                        }
                    }
                }

                if (tasks.Count > 0)
                {
                    completedTask = await Task.WhenAny(tasks);
                    tasks.Remove(completedTask);
                    --m_RunningTasks;
                }
                else
                    await Task.Delay(100, token);
            }
        }

        async Task DownloadManifestsContent(List<Task> tasks, IEnumerable<SyncManifest> manifests, CancellationToken token)
        {
            foreach (var manifest in manifests)
            {
                foreach (var content in manifest.Content)
                {
                    token.ThrowIfCancellationRequested();

                    var streamKey = new StreamKey(manifest.SourceId, content.Key);
                    tasks.Add(m_Client.DownloadSyncModelAsync(streamKey, content.Value.Hash, token));

                    if (tasks.Count >= m_Settings.maxTaskSize)
                    {
                        var completedTask = await Task.WhenAny(tasks);
                        tasks.Remove(completedTask);
                        m_CurrentCount++;
                    }
                }
            }
        }

        async Task WaitForTasksInList(List<Task> tasks, CancellationToken token)
        {
            while (tasks.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var completedTask = await Task.WhenAny(tasks);
                tasks.Remove(completedTask);
                m_CurrentCount++;
            }
        }

        public event Action<float> update;
    }
}
