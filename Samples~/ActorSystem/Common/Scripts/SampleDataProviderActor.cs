using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Actors.Samples
{
    [Actor]
    public class SampleDataProviderActor
    {
#pragma warning disable 649
        IOComponent m_IO;
#pragma warning restore 649
        
        List<UpdateTracker> m_UpdateTrackers = new List<UpdateTracker>();
        
        Dictionary<EntryGuid, List<Tracker>> m_Waiters = new Dictionary<EntryGuid, List<Tracker>>();

        readonly string m_ApplicationDataPath = Application.dataPath;

        string m_ProjectFolder;
        string ProjectFolder
        {
            get
            {
                if (m_ProjectFolder != null)
                    return m_ProjectFolder;
                
                m_ProjectFolder = Directory.EnumerateDirectories(m_ApplicationDataPath.Replace(@"/Assets", ""), ".SampleData", SearchOption.AllDirectories).FirstOrDefault();

                if (m_ProjectFolder == null)
                    Debug.LogError("Unable to find Samples data. Reflect Samples require local Reflect Model data in 'Reflect/Common/.SampleData'.");

                return m_ProjectFolder;
            }
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

            m_IO.StartJob(this, ctx, tracker,
                async (self, ctx, tracker, token) => await AcquireEntryAsync(tracker.Ctx.Data.EntryData, token),
                (self, ctx, tracker, syncModel) =>
                {
                    var trackers = self.m_Waiters[tracker.Ctx.Data.EntryData.Id];

                    foreach (var t in trackers)
                        t.Ctx.SendSuccess(syncModel);
                    trackers.Clear();
                },
                (self, ctx, tracker, ex) =>
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

            m_IO.StartJob(this, ctx, (object)null,
                async (self, ctx, userCtx, token) => await GetSyncManifestsAsync(),
                (self, ctx, userCtx, manifests) =>
                {
                    // Copy the lists so there is no race condition on future accesses
                    foreach (var tracker in self.m_UpdateTrackers)
                        tracker.Ctx.SendSuccess(manifests.ToList());
                    self.m_UpdateTrackers.Clear();
                },
                (self, ctx, userCtx, ex) =>
                {
                    foreach (var tracker in self.m_UpdateTrackers)
                        tracker.Ctx.SendFailure(ex);
                    self.m_UpdateTrackers.Clear();
                });
        }

        async Task<ISyncModel> AcquireEntryAsync(EntryData entry, CancellationToken token)
        {
            var fullPath = Path.Combine(ProjectFolder,
                entry.Hash + PlayerFile.PersistentKeyToExtension(entry.IdInSource));
            return await PlayerFile.LoadSyncModelAsync(fullPath, entry.IdInSource, token);
        }

        class Tracker
        {
            public RpcContext<GetSyncModel> Ctx;
        }

        async Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync()
        {
            var result = new List<SyncManifest>();

            foreach (var manifestFile in Directory.EnumerateFiles(ProjectFolder, "*.manifest", SearchOption.AllDirectories))
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
