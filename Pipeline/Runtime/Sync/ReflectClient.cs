using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;

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
        public PlayerStorage storage { get; }

        public AuthClient(UnityUser user, PlayerStorage storage)
        {
            this.user = user;
            this.storage = storage;
        }
        
        public Task<UnityProjectCollection> ListProjects()
        {
            return ProjectServer.Client.ListProjects(user, storage);
        }
    }
    
    public class ReflectClient : ISyncModelProvider, IDisposable
    {
        readonly UnityUser m_User;
        readonly PlayerStorage m_Storage;
        readonly UnityProject m_Project;

        ClientListener m_Listener;

        string m_CacheRoot;

        public ReflectClient(IUpdateDelegate updateDelegate, UnityUser user, IReflectStorage storage, UnityProject project)
        {
            m_Project = project;
            m_User = user;
            m_Storage = storage as PlayerStorage;

            m_CacheRoot = Path.Combine(Application.persistentDataPath, "ProjectData");

            InitializeProjectClientListener();

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

        public async Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync()
        {
            var client = m_Listener?.client;
            
            if (client != null)
            {
                // Online Mode
                var result = await client.GetManifestsAsync();

                if (result != null)
                {
                    var streamSources = new List<SyncManifest>();

                    foreach (var manifestAsset in result)
                    {
                        var manifest = manifestAsset.Manifest;
                        streamSources.Add(manifest);
                                
                        await SaveStreamSourceAsync(manifest);
                    }

                    return streamSources;
                }
            }
            else // Offline Mode
            {
                return await LoadStreamSourcesAsync(m_Project);
            }
            
            return null;
        }

        async Task SaveStreamSourceAsync(SyncManifest manifest)
        {
            var client = m_Listener.client;
            if (client != null)
            {
                await SaveManifestAsync(manifest);
            }
        }

        async Task SaveManifestAsync(SyncManifest manifest)
        {
            var folder = m_Storage.GetProjectFolder(m_Project);
            var fullPath = Path.Combine(folder, manifest.SourceId + ".manifest"); // TODO. Improve how sourceId is saved
            await PlayerFile.SaveManifestAsync(manifest, fullPath);
        }
        
        async Task<IEnumerable<SyncManifest>> LoadStreamSourcesAsync(UnityProject project)
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

        public Task<ISyncModel> GetSyncModelAsync(StreamKey streamKey, string hash)
        {
            // TODO Refactoring.
            // var fullPath = PlayerFile.GetFullPath(streamKey, hash);
            var downloadFolder = GetSourceProjectFolder(streamKey.source);
            var fullPath = GetSyncModelFullPath(streamKey, hash);

            return File.Exists(fullPath) ? PlayerFile.LoadSyncModelAsync(downloadFolder, streamKey.key, hash) : DownloadAndSave(streamKey, hash, fullPath);
        }

        public async Task DownloadSyncModelAsync(StreamKey streamKey, string hash)
        {
            // TODO Refactoring.
            // var fullPath = PlayerFile.GetFullPath(streamKey, hash);
            var fullPath = GetSyncModelFullPath(streamKey, hash);

            if (!File.Exists(fullPath))
            {
                await DownloadAndSave(streamKey, hash, fullPath);
            }
        }

        string GetSyncModelFullPath(StreamKey streamKey, string hash)
        {
            var downloadFolder = GetSourceProjectFolder(streamKey.source);
            var filename = hash + PlayerFile.PersistentKeyToExtension(streamKey.key);
            return Path.Combine(downloadFolder, filename);
        }

        public Action manifestUpdated { get; set; }

        async Task<ISyncModel> DownloadAndSave(StreamKey streamKey, string hash, string fullPath)
        {
            var client = m_Listener.client;
            var syncModel = await client.GetSyncModelAsync(streamKey.key, streamKey.source, hash);

            if (syncModel != null)
            {
                var directory = Path.GetDirectoryName(fullPath);

                Directory.CreateDirectory(directory);
                await PlayerFile.SaveAsync(syncModel, fullPath);
            }

            return syncModel;
        }
        
        string GetSourceProjectFolder(string sourceId)
        {
            var folder = Path.Combine(m_Project.ProjectId, sourceId);
            return Path.Combine(m_CacheRoot, m_Project.Host.ServerName, folder);
        }

        void InitializeProjectClientListener()
        {
            try
            {
                var client = Player.CreateClient(m_Project, m_User, ProjectServer.Client);
                m_Listener = new ClientListener(client); // TODO Should this class also be responsible to instantiate the Client?
                m_Listener.onManifestUpdated += (c, s) => manifestUpdated?.Invoke();
            }
            catch (ConnectionException ex)
            {
                var unityProject = m_Project;
                string message;
                if (unityProject.Host == UnityProjectHost.LocalService)
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
            }
        }
    }
}
