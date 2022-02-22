using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect.Pipeline
{
    public interface IHashProvider
    {
        string GetHash(StreamKey streamKey);
    }

    public interface IReflectRootNode
    {
        void Refresh();
    }
    
    [Serializable]
    public class ProjectStreamerNode : ReflectNode<ProjectStreamer>, IHashProvider, IReflectRootNode
    {
        public StreamAssetOutput assetOutput = new StreamAssetOutput();

        protected override ProjectStreamer Create(ReflectBootstrapper hook, ISyncModelProvider provider, IExposedPropertyTable resolver)
        {
           return new ProjectStreamer(provider, assetOutput);
        }

        public virtual void Refresh()
        {
            processor.Refresh();
        }

        public virtual string GetHash(StreamKey key)
        {
            return processor.GetHash(key);
        }
    }
    
    public class ProjectStreamer : ReflectTaskNodeProcessor
    {
        readonly ISyncModelProvider m_Client;

        readonly DataOutput<StreamAsset> m_AssetOutput;

        readonly Dictionary<string, IReadOnlyDictionary<PersistentKey, ManifestEntry>> m_Manifests;

        readonly ConcurrentQueue<IStream> m_PendingAdded;
        readonly ConcurrentQueue<IStream> m_PendingRemoved;
        readonly ConcurrentQueue<IStream> m_PendingModified;

        public ProjectStreamer(ISyncModelProvider client, DataOutput<StreamAsset> assetOutput)
        {
            m_Client = client;

            m_AssetOutput = assetOutput;

            m_Manifests = new Dictionary<string, IReadOnlyDictionary<PersistentKey, ManifestEntry>>();

            m_PendingAdded = new ConcurrentQueue<IStream>();
            m_PendingRemoved = new ConcurrentQueue<IStream>();
            m_PendingModified = new ConcurrentQueue<IStream>();
        }

        public void Refresh()
        {
            Run();
            m_AssetOutput.SendBegin();
        }

        protected override Task RunInternal(CancellationToken token)
        {
            return GetManifests(token);
        }

        protected override void UpdateInternal(float unscaledDeltaTime)
        {
            while (m_PendingRemoved.TryDequeue(out var stream))
            {
                var streamAsset = (StreamAsset)stream;
                var syncedData = new SyncedData<StreamAsset>(streamAsset.key, streamAsset);
                m_AssetOutput.SendStreamRemoved(syncedData);
            }
            
            while (m_PendingAdded.TryDequeue(out var stream))
            {
                var streamAsset = (StreamAsset)stream;
                var syncedData = new SyncedData<StreamAsset>(streamAsset.key, streamAsset);
                m_AssetOutput.SendStreamAdded(syncedData);
            }

            while (m_PendingModified.TryDequeue(out var stream))
            {
                var streamAsset = (StreamAsset)stream;
                var syncedData = new SyncedData<StreamAsset>(streamAsset.key, streamAsset);
                m_AssetOutput.SendStreamChanged(syncedData);
            }

            if (m_Task != null && m_Task.IsCompleted)
            {
                m_AssetOutput.SendEnd();
                m_Task = null;

                LogTimes();
            }
        }

        double m_GettingSourcesTime;
        double m_CompareManifests;
        double m_TotalTime;

        void LogTimes()
        {
            var msg = $"ProjectStreamer stats - Total: {m_TotalTime}, Getting sources: {m_GettingSourcesTime}, Compare manifests: {m_CompareManifests}";
            Debug.Log(msg);
        }

        static double GetElapsedTime(ref DateTime start)
        {
            var t = DateTime.Now;
            var ms = (t - start).TotalMilliseconds;
            start = t;
            return ms;
        }

        async Task GetManifests(CancellationToken token)
        {
            var time = DateTime.Now;
            var start = time;

            var manifests = await m_Client.GetSyncManifestsAsync(token);
            // Prioritize .DataSource manifests
            manifests = SyncManifest.PrioritizeDataSource(manifests);
            
            m_GettingSourcesTime = GetElapsedTime(ref time);

            foreach (var manifest in manifests)
            {
                token.ThrowIfCancellationRequested();

                // Parse SyncManifest
                var newManifest = manifest;

                m_Manifests.TryGetValue(manifest.SourceId, out var oldManifest);
                
                // Update the hash cache before sending events
                m_Manifests[manifest.SourceId] = newManifest.Content.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

                if (oldManifest != null)
                {
                    ComputeDiff(oldManifest, newManifest.Content, out var addedEntries, out var modifiedEntries, out var removedEntries);

                    foreach (var manifestEntry in addedEntries)
                    {
                        token.ThrowIfCancellationRequested();
                        
                        var reference = new StreamAsset(manifest.SourceId, manifestEntry.key, manifestEntry.entry.Hash, manifestEntry.entry.BoundingBox);
                        m_PendingAdded.Enqueue(reference);
                    }
                    
                    foreach (var manifestEntry in modifiedEntries)
                    {
                        token.ThrowIfCancellationRequested();
                        
                        var key = manifestEntry.key;

                        var reference = new StreamAsset(manifest.SourceId, key, manifestEntry.entry.Hash, manifestEntry.entry.BoundingBox);
                        m_PendingModified.Enqueue(reference);
                    }
                    
                    foreach (var manifestEntry in removedEntries)
                    {
                        token.ThrowIfCancellationRequested();
                        
                        var reference = new StreamAsset(manifest.SourceId, manifestEntry.key, manifestEntry.entry.Hash, manifestEntry.entry.BoundingBox);
                        m_PendingRemoved.Enqueue(reference);
                    }
                }
                else
                {
                    foreach (var manifestEntry in manifest.Content)
                    {
                        token.ThrowIfCancellationRequested();

                        if (!manifestEntry.Key.IsRootAsset)
                            continue;
                        
                        var reference = new StreamAsset(manifest.SourceId, manifestEntry.Key, manifestEntry.Value.Hash, manifestEntry.Value.BoundingBox);
                        m_PendingAdded.Enqueue(reference);
                    }
                }
            }
            
            m_CompareManifests = GetElapsedTime(ref time);
            
            m_TotalTime = GetElapsedTime(ref start);
        }

         struct ManifestDiffEntry
        {
            public PersistentKey key;
            public ManifestEntry entry;
            
            public ManifestDiffEntry(PersistentKey key, ManifestEntry entry)
            {
                this.key = key;
                this.entry = entry;
            }
        }

        static void ComputeDiff(IReadOnlyDictionary<PersistentKey, ManifestEntry> oldContent, IReadOnlyDictionary<PersistentKey, ManifestEntry> newContent,
            out IList<ManifestDiffEntry> added, out IList<ManifestDiffEntry> changed, out IList<ManifestDiffEntry> removed)
        {
            added = new List<ManifestDiffEntry>();
            changed = new List<ManifestDiffEntry>();
            removed = new List<ManifestDiffEntry>();

            foreach (var couple in oldContent)
            {
                var persistentKey = couple.Key;

                if (newContent.TryGetValue(persistentKey, out var newData))
                {
                    if (!Compare(couple.Value, newData))
                    {
                        changed.Add(new ManifestDiffEntry(persistentKey, newData));
                    }
                }
                else
                {
                    removed.Add(new ManifestDiffEntry(persistentKey, couple.Value));
                }
            }

            foreach (var couple in newContent)
            {
                var persistentKey = couple.Key;

                if (!oldContent.ContainsKey(persistentKey))
                {
                    added.Add(new ManifestDiffEntry(persistentKey, couple.Value));
                }
            }
        }

        static bool Compare(ManifestEntry first, ManifestEntry second)
        {
            return first.Hash == second.Hash && first.BoundingBox == second.BoundingBox;
        }

        public string GetHash(StreamKey streamKey)
        {
            return m_Manifests[streamKey.source][streamKey.key].Hash;
        }
    }
}
