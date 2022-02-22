using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Utils;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using Unity.Reflect.Actors;
using Unity.Reflect.Source.Utils.Errors;

namespace UnityEngine.Reflect.Pipeline
{
    class ClientListener
    {
        readonly ConcurrentQueue<ConnectionStatus> m_StatusUpdates;
        readonly ConcurrentQueue<ManifestUpdatedEventArgs> m_ManifestUpdates;

        public event Action<IPlayerClient, bool> onConnectionStatusUpdated;
        public event Action<IPlayerClient, string> onManifestUpdated;

        public IPlayerClient client { get; private set; }

        public ClientListener(IPlayerClient client)
        {
            m_ManifestUpdates = new ConcurrentQueue<ManifestUpdatedEventArgs>();
            m_StatusUpdates = new ConcurrentQueue<ConnectionStatus>();

            this.client = client;
            this.client.ManifestUpdated += OnManifestUpdated;
            this.client.ConnectionStatusChanged += OnConnectionStatusChanged;
        }

        public void Dispose()
        {
            if (client == null)
                return;
            
            client.ManifestUpdated -= OnManifestUpdated;
            client.ConnectionStatusChanged -= OnConnectionStatusChanged;
            client.Dispose();
            client = null;
        }

        void OnConnectionStatusChanged(ConnectionStatus obj)
        {
            m_StatusUpdates.Enqueue(obj);
        }

        void OnManifestUpdated(object sender, ManifestUpdatedEventArgs e)
        {
            m_ManifestUpdates.Enqueue(e);
        }
        
        public void ProcessPendingEvents()
        {
            var isConnected = false;
            var received = false;
            
            while (m_StatusUpdates.TryDequeue(out var status))
            {
                received = true;
                isConnected = status == ConnectionStatus.Connected;
            }

            if (received)
            {
                Debug.Log($"Client connection updated on project '{client.Project.Name}', isConnected: {isConnected}");
                onConnectionStatusUpdated?.Invoke(client, isConnected);

                if (!isConnected)
                {
                    // TODO Clear other pending events
                }
            }

            var updatedSourceIds = new HashSet<string>();
            while (m_ManifestUpdates.TryDequeue(out var manifestEvent))
            {
                if (updatedSourceIds.Contains(manifestEvent.SourceId))
                    continue;

                updatedSourceIds.Add(manifestEvent.SourceId);
                onManifestUpdated?.Invoke(client, manifestEvent.SourceId);
                
                Debug.Log($"Manifest updated on project '{client.Project.Name}', Source Id: {manifestEvent.SourceId}");
            }
        }
    }

    public class AuthClient : IProjectProvider
    {
        public UnityUser user { get; }

        public AuthClient(UnityUser user)
        {
            this.user = user;
        }
        
        public async Task<IProjectProvider.ProjectProviderResult> ListProjects()
        {
            var results = await ProjectServer.Client.ListProjects(user);

            var allProjects = results.ToDictionary(GetProjectKey, r =>
                new Project(r));

            var storedUnityProjects = Storage.main.EnumerateStoredProjects();
            
            foreach (var storedProject in storedUnityProjects)
            {
                var key = GetProjectKey(storedProject);
                if (allProjects.TryGetValue(key, out var project))
                {
                    if (project.IsLocal)
                        continue;

                    // This remote project is also available offline
                    project.IsLocal = true;
                    project.DownloadedPublished = storedProject.LastPublished;
                }
                else
                {
                    // This local project is not connected to its server
                    allProjects.Add(key, new Project(storedProject) { IsLocal = true});
                }
            }

            return new IProjectProvider.ProjectProviderResult
            {
                Projects = allProjects.Values.ToList(),
                Status = results.Status,
                ErrorMessage = results.ErrorMessage
            };
        }
        
        static string GetProjectKey(UnityProject unityProject)
        {
            return $"{unityProject.ProjectId}__{unityProject.Host.ServerId}";
        }
    }
    
    public class ReflectClient : ISyncModelProvider, IDisposable
    {
        public Action manifestUpdated { get; set; }

        readonly UnityUser m_User;
        readonly PlayerStorage m_Storage;
        readonly UnityProject m_Project;
        readonly AccessToken m_AccessToken;
        readonly IPlayerClient m_Client;
        readonly string m_SpatialFolderPath;

        ClientListener m_Listener;

        public ReflectClient(IUpdateDelegate updateDelegate, UnityUser user, PlayerStorage storage, UnityProject project, AccessToken accessToken)
        {
            m_Project = project;
            m_User = user;
            m_Storage = storage;
            m_AccessToken = accessToken;
            m_SpatialFolderPath = Path.Combine(storage.GetProjectFolder(project), DataProviderActor.k_StorageSpatialFolder);

            m_Client = CreatePlayerClient();

            m_Listener = new ClientListener(m_Client); // TODO Should this class also be responsible to instantiate the Client?
            m_Listener.onManifestUpdated += (c, s) => manifestUpdated?.Invoke();

            updateDelegate.update += OnUpdate;
        }

        void OnUpdate(float unscaledDeltaTime)
        {
            m_Listener?.ProcessPendingEvents();
        }

        public void Dispose()
        {
            m_Listener?.Dispose();
        }

        public async Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync(GetManifestOptions options, CancellationToken token)
        {
            if (m_Client != null)
            {
                // Online Mode
                var result = await m_Client.GetManifestsAsync(options, token);

                if (result != null)
                {
                    var manifests = new List<SyncManifest>();

                    foreach (var manifestAsset in result)
                    {
                        var manifest = manifestAsset.Manifest;
                        manifests.Add(manifest);

                        await SaveManifestAsync(manifest);
                    }

                    return manifests;
                }
            }
            else // Offline Mode
            {
                return await LoadManifests(m_Project);
            }
            
            return null;
        }

        public Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync(CancellationToken token)
        {
            return GetSyncManifestsAsync(new GetManifestOptions(), token);
        }

        async Task SaveManifestAsync(SyncManifest manifest)
        {
            var folder = m_Storage.GetProjectFolder(m_Project);
            var fullPath = Path.Combine(folder, manifest.SourceId + ".manifest"); // TODO. Improve how sourceId is saved
            await PlayerFile.SaveManifestAsync(manifest, fullPath);
        }
        
        async Task<IEnumerable<SyncManifest>> LoadManifests(UnityProject project)
        {
            var folder = m_Storage.GetProjectFolder(project);

            var result = new List<SyncManifest>();

            foreach (var manifestFile in Directory.EnumerateFiles(folder, "*.manifest", SearchOption.AllDirectories))
            {
                if (manifestFile == null)
                {
                    continue;
                }
                
                var syncManifest = await PlayerFile.LoadManifestAsync(manifestFile);

                result.Add(syncManifest);
            }

            return result;
        }

        public Task<ISyncModel> GetSyncModelAsync(StreamKey streamKey, string hash, CancellationToken token)
        {
            var fullPath = m_Storage.GetSyncModelFullPath(m_Project, streamKey.source, streamKey.key, hash);

            if (File.Exists(fullPath))
            {
                var task = PlayerFile.LoadSyncModelAsync(fullPath, streamKey.key, token);
                
#if UNITY_ANDROID && !UNITY_EDITOR
                var result = task.Result;
                return Task.FromResult(result);
#else
                return task;
#endif
            }

            return DownloadAndSave(streamKey, hash, fullPath, token);
        }

        public async Task DownloadSyncModelAsync(StreamKey streamKey, string hash, CancellationToken token)
        {
            // TODO Refactoring.
            // var fullPath = PlayerFile.GetFullPath(streamKey, hash);
            var fullPath = GetSyncModelFullPath(streamKey, hash);

            if (!File.Exists(fullPath))
            {
                await DownloadAndSave(streamKey, hash, fullPath, token);
            }
        }

        string GetSyncModelFullPath(StreamKey streamKey, string hash)
        {
            return m_Storage.GetSyncModelFullPath(m_Project, streamKey.source, streamKey.key, hash);
        }

        async Task<ISyncModel> DownloadAndSave(StreamKey streamKey, string hash, string fullPath, CancellationToken token)
        {
            var syncModel = await m_Client.GetSyncModelAsync(streamKey.key, streamKey.source, hash, token);

            if (syncModel != null)
            {
                var directory = Path.GetDirectoryName(fullPath);

                Directory.CreateDirectory(directory);
                await PlayerFile.SaveAsync(syncModel, fullPath);
            }

            return syncModel;
        }

        public Task<SpatialManifest> DownloadSpatialManifestAsync(IEnumerable<SyncId> ids, GetNodesOptions getNodesOptions, CancellationToken token)
        {
            return DataProviderActor.LoadSpatialManifestAsync(ids, getNodesOptions, token, m_SpatialFolderPath, m_Client);
        }

        IPlayerClient CreatePlayerClient()
        {
            try
            {
                var httpClient = new ReflectRequestHandler();
                var client = Player.CreateClient(m_Project, m_User, ProjectServer.Client, m_AccessToken, httpClient);
                Debug.Log($"Establishing {client.Protocol.ToString()} connection to Sync server.");
                return client;
            }
            catch (ConnectionException ex)
            {
                var unityProject = m_Project;
                string message;
                if (unityProject.Host.IsLocalService)
                {
                    message = "A connection with your local server could not be established. Make sure the Unity Reflect Service is running.";
                }
                else
                {
                    if (unityProject.Host.ServerName == "Cloud")
                    {
                        message = $"A connection with Reflect Cloud could not be established.";
                    }
                    else
                    {
                        message = $"A connection with the server {unityProject.Host.ServerName} could not be established. This server may be outside your local network (LAN) or may not accept external connections due to firewall policies.";
                    }
                }

                ex = new ConnectionException(message, ex);
                //completeWithError(ex);
                //throw ex;
                return null;
            }
        }
    }
}
