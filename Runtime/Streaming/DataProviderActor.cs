using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect.Actor;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;

namespace Unity.Reflect.Streaming
{
    [Actor]
    public class DataProviderActor
    {
#pragma warning disable 649
        IOComponent m_IO;
#pragma warning restore 649
        
        IPlayerClient m_Client;
        UnityProject m_Project;
        PlayerStorage m_Storage;
        List<UpdateTracker> m_UpdateTrackers = new List<UpdateTracker>();
        
        Dictionary<Guid, List<Tracker>> m_Waiters = new Dictionary<Guid, List<Tracker>>();

        public void Inject(IPlayerClient client, UnityProject project)
        {
            m_Client = client;
            m_Project = project;

            m_Storage = new PlayerStorage(UnityEngine.Reflect.ProjectServer.ProjectDataPath, true, false);
        }

        [RpcInput]
        void OnGetSyncModel(RpcContext<GetSyncModel> ctx)
        {
            var tracker = new Tracker { Ctx = ctx };
            
            var resourceId = ctx.Data.EntryData.Id;
            if (!m_Waiters.TryGetValue(resourceId, out var trackers))
            {
                trackers = new List<Tracker>();
                m_Waiters.Add(resourceId, trackers);
            }

            trackers.Add(tracker);
            if (trackers.Count > 1)
                return;

            var job = m_IO.StartJob(this, ctx, tracker, async (self, ctx, tracker) => await AcquireEntryAsync(tracker.Ctx.Data.EntryData, default));
            job.Success((self, ctx, tracker, syncModel) =>
            {
                var trackers = self.m_Waiters[tracker.Ctx.Data.EntryData.Id];

                foreach (var t in trackers)
                    t.Ctx.SendSuccess(syncModel);
                trackers.Clear();
            });

            job.Failure((self, ctx, tracker, ex) =>
            {
                var trackers = self.m_Waiters[tracker.Ctx.Data.EntryData.Id];
                
                foreach (var t in trackers)
                    t.Ctx.SendFailure(ex);
                trackers.Clear();
            });
        }

        [RpcInput]
        void OnGetManifests(RpcContext<GetManifests> ctx)
        {
            m_UpdateTrackers.Add(new UpdateTracker{ Ctx = ctx });

            if (m_UpdateTrackers.Count > 1)
                return;

            var job = m_IO.StartJob(this, ctx, (object)null, async (self, ctx, userCtx) => await GetSyncManifestsAsync());
            job.Success((self, ctx, userCtx, manifests) =>
            {
                // Copy the lists so there is no race condition on future accesses
                foreach (var tracker in self.m_UpdateTrackers)
                    tracker.Ctx.SendSuccess(manifests.ToList());
                self.m_UpdateTrackers.Clear();
            });

            job.Failure((self, ctx, userCtx, ex) =>
            {
                foreach (var tracker in self.m_UpdateTrackers)
                    tracker.Ctx.SendFailure(ex);
                self.m_UpdateTrackers.Clear();
            });
        }

        async Task<ISyncModel> AcquireEntryAsync(EntryData entry, CancellationToken token)
        {
            return await GetSyncModelAsync(entry, token);
        }

        async Task<ISyncModel> GetSyncModelAsync(EntryData entry, CancellationToken token)
        {
            var fullPath = GetEntryFullPath(entry);

            if (!File.Exists(fullPath))
                await DownloadAndSave(entry, fullPath, token);

            return await ReadLocalEntryAsync(entry, token);
        }

        string GetEntryFullPath(EntryData entry)
        {
            var downloadFolder = GetSourceProjectFolder(entry.SourceId);
            var filename = entry.Hash + PlayerFile.SyncModelTypeToExtension(entry.EntryType);
            return Path.Combine(downloadFolder, filename);
        }

        string GetSourceProjectFolder(string sourceId)
        {
            return Path.Combine(m_Storage.GetProjectFolder(m_Project), sourceId);
        }

        async Task DownloadAndSave(EntryData entry, string fullPath, CancellationToken token)
        {
            // Todo: add cancellationToken
            var syncModel = await m_Client.GetSyncModelAsync(new PersistentKey(entry.EntryType, entry.IdInSource), entry.SourceId, entry.Hash);

            token.ThrowIfCancellationRequested();

            if (syncModel != null)
            {
                var directory = Path.GetDirectoryName(fullPath);

                Directory.CreateDirectory(directory);
                await PlayerFile.SaveAsync(syncModel, fullPath);
            }
        }

        async Task<ISyncModel> ReadLocalEntryAsync(EntryData entry, CancellationToken token)
        {
            // Todo: add cancellationToken
            return await PlayerFile.LoadSyncModelAsync(GetSourceProjectFolder(entry.SourceId), new PersistentKey(entry.EntryType, entry.IdInSource), entry.Hash);
        }

        class Tracker
        {
            public RpcContext<GetSyncModel> Ctx;
        }

        async Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync()
        {
            if (m_Client != null)
            {
                // Online Mode
                var result = await m_Client.GetManifestsAsync();

                if (result != null)
                {
                    var streamSources = new List<SyncManifest>();

                    foreach (var manifestAsset in result)
                    {
                        var manifest = manifestAsset.Manifest;
                        streamSources.Add(manifest);
                                
                        await SaveManifestAsync(manifest);
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
                    continue;
                
                var syncManifest = await PlayerFile.LoadManifestAsync(manifestFile);

                result.Add(syncManifest);
            }

            return result;
        }

        struct UpdateTracker
        {
            public RpcContext<GetManifests> Ctx;
        }
    }
}
