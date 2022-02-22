using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Zip;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using Unity.Reflect.Utils;
using UnityEngine;
using UnityEngine.Reflect;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.Actors
{
    [Actor("5944b8e2-36bb-466b-9f1b-32de711e8ca8")]
    public class DataProviderActor
    {
#pragma warning disable 649
        Settings m_Settings;
        IOComponent m_IO;
        NetComponent m_Net;
#pragma warning restore 649

        static readonly TimeSpan k_BatchDelay = TimeSpan.FromMilliseconds(100);
        static readonly FileLock k_CacheFilesLock = new FileLock();

        const string k_TmpFolderName = "tmp";
        const string k_ArchiveFolderName = "archives";
        const string k_IdMappingFileName = "id_mapping.bin";
        const string k_HeaderMappingFileName = "header_mapping.bin";
        const string k_SyncModelFileName = "sync_models.bin";

        public static readonly string k_StorageSpatialFolder = "spatial";
        static readonly string k_StorageSpatialMappingExtension = "spatialmapping";
        static readonly string k_StorageSpatialManifestExtension = "spatialmanifest";
        static readonly string k_StorageSpatialProjectInfoFile = "spatial.data";

        ActorHandle m_Self;
        IPlayerClient m_Client;
        Project m_Project;

        bool m_NotEnoughMemoryRemaining;
        int m_NbRunningBatchRequests;

        string m_TmpFolderPath;
        string m_ArchiveFolderPath;
        string m_SyncModelFilePath;

        bool m_IsInReadMode;
        FileStream m_IdMappingWriteStream;
        FileStream m_SyncModelHeaderWriteStream;
        FileStream m_SyncModelWriteStream;
        FileLock m_SyncModelHeaderWriteLock = new FileLock();
        FileLock m_SyncModelWriteLock = new FileLock();
        
        Queue<RpcContext<PatchEntryIds, object>> m_PatchTrackers = new Queue<RpcContext<PatchEntryIds, object>>();
        List<UpdateTracker> m_UpdateTrackers = new List<UpdateTracker>();

        Dictionary<EntryKey, (EntryGuid Id, EntryStableGuid StableId)> m_KnownKeyToIds = new Dictionary<EntryKey, (EntryGuid EntryId, EntryStableGuid StableId)>();
        ConcurrentDictionary<EntryGuid, FileSyncModelHeader> m_InCacheSyncModels = new ConcurrentDictionary<EntryGuid, FileSyncModelHeader>();

        Dictionary<EntryGuid, TrackerCollection> m_Trackers = new Dictionary<EntryGuid, TrackerCollection>();
        Queue<DownloadFileRequest> m_DownloadRequests = new Queue<DownloadFileRequest>();
        Dictionary<EntryGuid, List<RpcContext<DownloadSyncModel>>> m_PendingDownloadWaiters = new Dictionary<EntryGuid, List<RpcContext<DownloadSyncModel>>>();

        ConcurrentQueue<FileStream> m_SyncModelReaders = new ConcurrentQueue<FileStream>();

        struct DownloadFileRequest
        {
            public EntryData Entry;

            public DownloadFileRequest(EntryData entry)
            {
                Entry = entry;
            }
        }

        class PrepareBatch { }
        class PostInitialize { }

        public void Initialize(ActorHandle self, IPlayerClient client, Project project)
        {
            m_Self = self;
            m_Client = client;
            m_Project = project;

            UpdateNbConcurrentTasks(m_Settings.NbConcurrentTasks);
            
            m_Net.Register<PostInitialize>(OnPostInitialize);
            m_Net.Register<PrepareBatch>(OnPrepareBatch);

            m_Net.Send(m_Self, new PostInitialize());
            m_Net.DelayedSend(k_BatchDelay, m_Self, new PrepareBatch());
        }

        public void Shutdown()
        {
            m_IdMappingWriteStream?.Dispose();
            m_SyncModelHeaderWriteStream?.Dispose();
            m_SyncModelWriteStream?.Dispose();

            while (m_SyncModelReaders.TryDequeue(out var stream))
                stream.Dispose();
        }

        [RpcInput]
        void OnPatchEntryIds(RpcContext<PatchEntryIds> ctx)
        {
            m_PatchTrackers.Enqueue(ctx);
            if (m_PatchTrackers.Count > 1)
                return;

            ProcessNextPatchEntryIdsRequest();
        }

        [RpcInput]
        void OnDownloadSyncModel(RpcContext<DownloadSyncModel> ctx)
        {
            if (FailIfNotEnoughMemory(ctx))
                return;

            var resourceId = ctx.Data.EntryData.Id;

            if (m_Trackers.TryGetValue(resourceId, out var trackers))
            {
                trackers.DownloadContexts.Add(ctx);
                return;
            }

            if (GetFilePath(ctx.Data.EntryData, out _))
            {
                ctx.SendSuccess(NullData.Null);
                return;
            }

            if (!m_PendingDownloadWaiters.TryGetValue(resourceId, out var waiters))
            {
                waiters = new List<RpcContext<DownloadSyncModel>>();
                m_PendingDownloadWaiters.Add(resourceId, waiters);
            }
            waiters.Add(ctx);
            m_PendingDownloadWaiters.Add(ctx.Data.EntryData.Id, waiters);
        }

        [RpcInput]
        void OnGetSyncModel(RpcContext<GetSyncModel> ctx)
        {
            if (FailIfNotEnoughMemory(ctx))
                return;

            var resourceId = ctx.Data.EntryData.Id;
            if (m_Trackers.TryGetValue(resourceId, out var trackers))
            {
                trackers.OpenContexts.Add(ctx);
                return;
            }

            trackers = new TrackerCollection();
            trackers.OpenContexts.Add(ctx);
            m_Trackers.Add(resourceId, trackers);

            if (GetFilePath(ctx.Data.EntryData, out var fullPath))
                StartReadSyncModelFromDisk(ctx, fullPath);
            else if (!m_IsInReadMode)
            {
                if (m_PendingDownloadWaiters.TryGetValue(resourceId, out var requests))
                {
                    m_PendingDownloadWaiters.Remove(resourceId);
                    trackers.DownloadContexts.AddRange(requests);
                }

                m_DownloadRequests.Enqueue(new DownloadFileRequest(ctx.Data.EntryData));
            }
            else
                ctx.SendFailure(new Exception("The project is opened in read mode."));
        }

        [RpcInput]
        void OnGetManifests(RpcContext<GetManifests> ctx)
        {
            if (FailIfNotEnoughMemory(ctx))
                return;

            m_UpdateTrackers.Add(new UpdateTracker{ Ctx = ctx });

            if (m_UpdateTrackers.Count > 1)
                return;

            ProcessGetManifestsRequest(ctx);
        }

        [RpcInput]
        void OnGetSpatialManifest(RpcContext<GetSpatialManifest, SpatialManifest> ctx)
        {
            m_IO.StartJob(this, ctx, (object)null,
                async (self, ctx, userCtx, token) => 
                    await GetSpatialManifestAsync(ctx.Data.NodeIds, ctx.Data.GetNodesOptions, token),
                (self, ctx, userCtx, spatialManifest) =>
                    ctx.SendSuccess(spatialManifest),
                (self, ctx, userCtx, ex) =>
                    ctx.SendFailure(ex));
        }

        [NetInput]
        void OnMemoryStateChanged(NetContext<MemoryStateChanged> ctx)
        {
            m_NotEnoughMemoryRemaining = ctx.Data.TotalAppMemory > ctx.Data.CriticalThreshold;
        }

        [EventInput]
        void OnUpdateSetting(EventContext<UpdateSetting<Settings>> ctx)
        {
            if (m_Settings.Id != ctx.Data.Id)
                return;
            
            var fieldName = ctx.Data.FieldName;
            var newValue = ctx.Data.NewValue;

            if (fieldName == nameof(Settings.NbConcurrentTasks))
                UpdateNbConcurrentTasks((int)newValue);
        }

        void OnPostInitialize(NetContext<PostInitialize> ctx)
        {
            m_Net.Suspend();

            m_IO.StartJob(this, ctx, (object)null,
                async (self, ctx, userCtx, token) =>
                {
                    var projectFolder = Storage.cache.GetProjectFolder(m_Project);
                    m_TmpFolderPath = Path.Combine(projectFolder, k_TmpFolderName);
                    m_ArchiveFolderPath = Path.Combine(projectFolder, k_ArchiveFolderName);

                    if (Directory.Exists(m_TmpFolderPath))
                        Directory.Delete(m_TmpFolderPath, true);

                    Directory.CreateDirectory(m_TmpFolderPath);
                    Directory.CreateDirectory(m_ArchiveFolderPath);

                    var mappingFilePath = Path.Combine(m_ArchiveFolderPath, k_IdMappingFileName);
                    try
                    {
                        m_IdMappingWriteStream = new FileStream(mappingFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    }
                    catch (IOException)
                    {
                        m_IsInReadMode = true;
                    }
                    
                    var headerMappingFilePath = Path.Combine(m_ArchiveFolderPath, k_HeaderMappingFileName);
                    try
                    {
                        m_SyncModelHeaderWriteStream = new FileStream(headerMappingFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    }
                    catch (IOException)
                    {
                        m_IsInReadMode = true;
                    }

                    m_SyncModelFilePath = Path.Combine(m_ArchiveFolderPath, k_SyncModelFileName);
                    try
                    {
                        m_SyncModelWriteStream = new FileStream(m_SyncModelFilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                    }
                    catch (IOException)
                    {
                        m_IsInReadMode = true;
                    }
                    
                    using (var readStream = new FileStream(mappingFilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var fileInfo = new FileInfo(mappingFilePath);
                        using (var memStream = new MemoryStream((int)fileInfo.Length))
                        {
                            await readStream.CopyToAsync(memStream);
                            memStream.Position = 0;
                            using (var reader = new BinaryReader(memStream, new UTF8Encoding(false, true), true))
                            {
                                while (TryReadNextIdMapping(reader, out var kv))
                                    m_KnownKeyToIds[kv.Key] = kv.Value;
                            }
                        }
                    }
                    
                    using (var readStream = new FileStream(headerMappingFilePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite))
                    {
                        var fileInfo = new FileInfo(headerMappingFilePath);
                        using (var memStream = new MemoryStream((int)fileInfo.Length))
                        {
                            await readStream.CopyToAsync(memStream);
                            memStream.Position = 0;
                            using (var reader = new BinaryReader(memStream, new UTF8Encoding(false, true), true))
                            {
                                while (TryReadNextSyncModelHeader(reader, out var header))
                                    m_InCacheSyncModels.TryAdd(header.EntryId, header);
                            }
                        }
                    }

                    for(var i = 0; i < m_Settings.NbConcurrentTasks; ++i)
                        m_SyncModelReaders.Enqueue(new FileStream(m_SyncModelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));

                    return NullData.Null;
                },
                (self, ctx, userCtx, res) => m_Net.Resume(),
                (self, ctx, userCtx, ex) =>
                {
                    m_Net.Resume();
                    if (!(ex is OperationCanceledException))
                        Debug.LogException(ex);
                });
        }

        void OnPrepareBatch(NetContext<PrepareBatch> ctx)
        {
            if (m_Client == null)
                return;

            while (m_NbRunningBatchRequests < m_Settings.NbConcurrentTasks && m_DownloadRequests.Count > 0)
            {
                var requests = new List<DownloadFileRequest>();
                while (m_DownloadRequests.Count > 0)
                {
                    var request = m_DownloadRequests.Dequeue();
                    requests.Add(request);
                }
                
                ++m_NbRunningBatchRequests;
                m_IO.StartJob(this, (object)null, requests,
                    async (self, _, requests, token) => await StartBatchAsync(requests, token),
                    (self, ctx, requests, res) => self.OnBatchDownloadSuccess(requests),
                    (self, ctx, requests, ex) => self.OnBatchDownloadFailure(requests, ex));
            }
            
            m_Net.DelayedSend(k_BatchDelay, m_Self, new PrepareBatch());
        }

        void ProcessNextPatchEntryIdsRequest()
        {
            while (true)
            {
                var ctx = m_PatchTrackers.Dequeue();

                var delta = new Delta<FileIdMappingEntry>();

                foreach (var entry in ctx.Data.Delta.Added)
                {
                    var key = new EntryKey(entry.Hash, new StreamKey(entry.SourceId, entry.IdInSource));
                    if (!m_KnownKeyToIds.TryGetValue(key, out var pair))
                    {
                        pair.Id = entry.Id;
                        pair.StableId = entry.StableId;
                        delta.Added.Add(new FileIdMappingEntry(false, key, pair));
                    }

                    // Remove/Add to make sure we use the same string instance for the EntryKey.
                    // The GC will collect back the memory for allocation done through IdMapping file reading.
                    m_KnownKeyToIds.Remove(key);
                    m_KnownKeyToIds.Add(key, pair);

                    entry.Id = pair.Id;
                    entry.StableId = pair.StableId;
                }

                foreach (var kv in ctx.Data.Delta.Changed)
                {
                    var oldKey = new EntryKey(kv.Prev.Hash, new StreamKey(kv.Prev.SourceId, kv.Prev.IdInSource));
                    m_KnownKeyToIds.Remove(oldKey);

                    var newKey = new EntryKey(kv.Next.Hash, new StreamKey(kv.Next.SourceId, kv.Next.IdInSource));
                    m_KnownKeyToIds.Add(newKey, (kv.Next.Id, kv.Next.StableId));

                    delta.Changed.Add((new FileIdMappingEntry(true, oldKey, (kv.Prev.Id, kv.Prev.StableId)), new FileIdMappingEntry(false, newKey, (kv.Next.Id, kv.Next.StableId))));
                }

                foreach (var entry in ctx.Data.Delta.Removed)
                {
                    // Never remove keys as they can be re-added
                    // We should remove them when live sync is working without doing Removed for items that were not really removed
                    //var key = new EntryKey(entry.Hash, new StreamKey(entry.SourceId, entry.IdInSource));
                    //m_KnownKeyToIds.Remove(key);
                    //delta.Removed.Add(new FileIdMappingEntry(true, key, (entry.Id, entry.StableId)));
                }

                if (!m_IsInReadMode)
                {
                    using (var writer = new BinaryWriter(m_IdMappingWriteStream, new UTF8Encoding(false, true), true))
                    {
                        foreach (var mapping in delta.Added)
                            WriteIdMappingEntry(mapping, writer);

                        foreach (var kv in delta.Changed)
                        {
                            // Todo: mark old as deleted
                            WriteIdMappingEntry(kv.Next, writer);
                        }

                        foreach (var kv in delta.Removed)
                        {
                            // Todo: mark as deleted
                        }
                    }
                }

                ctx.SendSuccess(NullData.Null);

                if (m_PatchTrackers.Count > 0)
                    continue;

                break;
            }
        }

        void OnBatchDownloadSuccess(List<DownloadFileRequest> requests)
        {
            --m_NbRunningBatchRequests;

            foreach (var request in requests)
            {
                var trackers = m_Trackers[request.Entry.Id];

                foreach(var ctx in trackers.DownloadContexts)
                    ctx.SendSuccess(NullData.Null);
                trackers.DownloadContexts.Clear();

                if (trackers.OpenContexts.Count > 0)
                {
                    GetFilePath(request.Entry, out var fullPath);
                    StartReadSyncModelFromDisk(trackers.OpenContexts[0], fullPath);
                }
                else
                    m_Trackers.Remove(request.Entry.Id);
            }
        }

        void OnBatchDownloadFailure(List<DownloadFileRequest> requests, Exception ex)
        {
            --m_NbRunningBatchRequests;

            foreach (var request in requests)
            {
                var trackers = m_Trackers[request.Entry.Id];

                foreach(var ctx in trackers.DownloadContexts)
                    ctx.SendFailure(ex);

                foreach(var ctx in trackers.OpenContexts)
                    ctx.SendFailure(ex);

                m_Trackers.Remove(request.Entry.Id);
            }
        }

        async Task<NullData> StartBatchAsync(List<DownloadFileRequest> requests, CancellationToken token)
        {
            var tmpFolder = Path.Combine(Storage.cache.GetProjectFolder(m_Project), k_TmpFolderName);
            var tmpArchivePath = Path.Combine(tmpFolder, $"{Guid.NewGuid()}.zip");

            var batch = requests
                .GroupBy(x => x.Entry.SourceId)
                .Select(x =>
                    new BatchedSyncModelSource(
                        x.Key,
                        x.Select(x => new BatchedSyncModel(x.Entry.IdInSource, x.Entry.Hash))));

            await m_Client.DownloadSyncModelBatchAsync(batch, tmpArchivePath, token);

            using (var zipToOpen = new FileStream(tmpArchivePath, FileMode.Open))
            {
                using (var archive = new ZipFile(zipToOpen))
                {
                    var folderToSourceNames = new Dictionary<string, string>();
                    foreach (ZipEntry zipEntry in archive)
                    {
                        if (zipEntry.IsDirectory || !zipEntry.Name.EndsWith(".sourceName", StringComparison.OrdinalIgnoreCase))
                            continue;

                        using (var sourceStream = archive.GetInputStream(zipEntry))
                        {
                            var memStream = new MemoryStream((int)zipEntry.Size);
                            await sourceStream.CopyToAsync(memStream, 1024, token);
                            var buf = memStream.GetBuffer();
                            var enc = new UTF8Encoding(true);
                            var preamble = enc.GetPreamble();
                            var offset = 0;
                            if (!preamble.Where((c, i) => c != buf[i]).Any())
                                offset = preamble.Length;
                            var sourceName = Encoding.UTF8.GetString(buf, offset, (int)zipEntry.Size - offset);
                            sourceName = sourceName.TrimEnd('\n');
                            folderToSourceNames.Add(
                                zipEntry.Name.Substring(0, zipEntry.Name.IndexOf("/")),
                                sourceName);
                        }
                    }

                    foreach (ZipEntry fileEntry in archive)
                    {
                        if (fileEntry.IsDirectory || fileEntry.Name.EndsWith(".sourceName", StringComparison.OrdinalIgnoreCase))
                            continue;

                        var paths = fileEntry.Name.Split('/');
                        var sourceId = folderToSourceNames[paths[0]];
                        var hash = paths[1].Substring(0, paths[1].IndexOf('.'));

                        var entry = requests.First(x => x.Entry.Hash == hash && x.Entry.SourceId == sourceId).Entry;

                        var header = await WriteSyncObjectInstanceFileToArchiveAsync(entry.Id, archive, fileEntry);
                        m_InCacheSyncModels.TryAdd(header.EntryId, header);

                        token.ThrowIfCancellationRequested();
                    }
                }
            }

            File.Delete(tmpArchivePath);
            return NullData.Null;
        }

        async Task<FileSyncModelHeader> WriteSyncObjectInstanceFileToArchiveAsync(EntryGuid entryId, ZipFile archive, ZipEntry zipEntry)
        {
            using (var input = archive.GetInputStream(zipEntry))
            {
                var payloadOffset = await m_SyncModelWriteLock
                    .LockAsync(async () =>
                    {
                        var payloadOffset = m_SyncModelWriteStream.Position;
                        await input.CopyToAsync(m_SyncModelWriteStream).ConfigureAwait(false);
                        await m_SyncModelWriteStream.FlushAsync(default).ConfigureAwait(false);
                        return payloadOffset;
                    }, default)
                    .ConfigureAwait(false);
                
                return await m_SyncModelHeaderWriteLock
                    .LockAsync(async () =>
                    {
                        var fileSize = (int)zipEntry.Size;
                        var header = new FileSyncModelHeader(false, entryId, payloadOffset, fileSize);
                        await WriteFileSyncModelHeaderAsync(header);
                        await m_SyncModelHeaderWriteStream.FlushAsync(default).ConfigureAwait(false);
                        return header;
                    }, default)
                    .ConfigureAwait(false);
            }
        }

        async Task WriteFileSyncModelHeaderAsync(FileSyncModelHeader header)
        {
            var buffer = new byte[sizeof(long) + sizeof(int)];
            CopyInt(header.PayloadOffset, buffer, 0);
            CopyInt(header.PayloadLength, buffer, sizeof(long));

            await m_SyncModelHeaderWriteStream.WriteAsync(new[] { (byte)(header.IsDeleted ? 1 : 0) }, 0, 1);
            await m_SyncModelHeaderWriteStream.WriteAsync(header.EntryId.GetUntypedGuid.ToByteArray(), 0, 16);
            await m_SyncModelHeaderWriteStream.WriteAsync(buffer, 0, buffer.Length);
        }

        static void CopyInt(long source, byte[] destination, int offset)
        {
            // We suppose the device to be little-endian
            destination[offset] = (byte) (source >> 0);
            destination[offset + 1] = (byte) (source >> 8);
            destination[offset + 2] = (byte) (source >> 16);
            destination[offset + 3] = (byte) (source >> 24);
            destination[offset + 4] = (byte) (source >> 32);
            destination[offset + 5] = (byte) (source >> 40);
            destination[offset + 6] = (byte) (source >> 48);
            destination[offset + 7] = (byte) (source >> 56);
        }

        static void CopyInt(int source, byte[] destination, int offset)
        {
            // We suppose the device to be little-endian
            destination[offset] = (byte) (source >> 0);
            destination[offset + 1] = (byte) (source >> 8);
            destination[offset + 2] = (byte) (source >> 16);
            destination[offset + 3] = (byte) (source >> 24);
        }

        bool FailIfNotEnoughMemory<T>(RpcContext<T> ctx)
            where T : class
        {
            if (m_NotEnoughMemoryRemaining)
            {
                ctx.SendFailure(new InsufficientMemoryException());
                return true;
            }

            return false;
        }

        void StartReadSyncModelFromDisk<T>(RpcContext<T, object> ctx, string fullPath)
            where T : MessageWithEntryData
        {
            m_IO.StartJob(this, ctx, fullPath,
                async (self, ctx, fullPath, token) => await GetSyncModelAsByteAsync(ctx.Data.EntryData, fullPath, token),
                (self, ctx, userCtx, data) =>
                {
                    var syncModel = DataModelUtils.ConvertFromModelToSyncModel(ctx.Data.EntryData.IdInSource, data, 0, data.Length);

                    var trackers = self.m_Trackers[ctx.Data.EntryData.Id];
                    
                    foreach (var streamCtx in trackers.OpenContexts)
                        streamCtx.SendSuccess(syncModel);

                    foreach(var cacheCtx in trackers.DownloadContexts)
                        cacheCtx.SendSuccess(null);
                    
                    self.m_Trackers.Remove(ctx.Data.EntryData.Id);
                },
                (self, ctx, userCtx, ex) =>
                {
                    var trackers = self.m_Trackers[ctx.Data.EntryData.Id];

                    foreach (var streamCtx in trackers.OpenContexts)
                        streamCtx.SendFailure(ex);

                    foreach(var cacheCtx in trackers.DownloadContexts)
                        cacheCtx.SendFailure(ex);
                    
                    self.m_Trackers.Remove(ctx.Data.EntryData.Id);
                });
        }

        void ProcessGetManifestsRequest(RpcContext<GetManifests> ctx)
        {
            m_IO.StartJob(this, ctx, (object)null,
                async (self, ctx, userCtx, token) => await GetSyncManifestsAsync(ctx.Data.GetManifestOptions, token),
                (self, ctx, userCtx, manifests) =>
                {
                    self.m_UpdateTrackers[0].Ctx.SendSuccess(manifests);
                    self.m_UpdateTrackers.RemoveAt(0);

                    if (self.m_UpdateTrackers.Count > 0)
                        ProcessGetManifestsRequest(self.m_UpdateTrackers[0].Ctx);
                },
                (self, ctx, userCtx, ex) =>
                {
                    self.m_UpdateTrackers[0].Ctx.SendFailure(ex);
                    self.m_UpdateTrackers.RemoveAt(0);

                    if (self.m_UpdateTrackers.Count > 0)
                        ProcessGetManifestsRequest(self.m_UpdateTrackers[0].Ctx);
                });
        }

        bool GetFilePath(EntryData entry, out string fullPath)
        {
            fullPath = null;

            if (m_InCacheSyncModels.TryGetValue(entry.Id, out _))
                return true;

            // Check if file is in storage.
            fullPath = Storage.main.GetSyncModelFullPath(m_Project, entry.SourceId, entry.EntryType, entry.Hash);

            if (!File.Exists(fullPath))
            {
                // If not in storage, check in cache.
                fullPath = Storage.cache.GetSyncModelFullPath(m_Project, entry.SourceId, entry.EntryType, entry.Hash);

                if (File.Exists(fullPath))
                {
                    var fi = new FileInfo(fullPath);
                    if (fi.Length == 0)
                        File.Delete(fullPath);
                }

                if (!File.Exists(fullPath))
                    return false;
            }

            return true;
        }

        async Task<byte[]> GetSyncModelAsByteAsync(EntryData entry, string fullPath, CancellationToken token)
        {
            // If there's no path, the data is in the archive
            if (fullPath == null)
            {
                var header = m_InCacheSyncModels[entry.Id];

                if (!m_SyncModelReaders.TryDequeue(out var stream))
                {
                    stream = new FileStream(m_SyncModelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    m_SyncModelReaders.Enqueue(stream);
                }

                stream.Seek(header.PayloadOffset, SeekOrigin.Begin);
                var b = new byte[header.PayloadLength];
                await stream.ReadAsync(b, 0, header.PayloadLength, token).ConfigureAwait(false);

                m_SyncModelReaders.Enqueue(stream);
                return b;
            }

            var fileInfo = new FileInfo(fullPath);
            var buffer = new byte[fileInfo.Length];

            await ReadFileAsync(fullPath, buffer, fileInfo.Length, token);
            return buffer;
        }

        static async Task ReadFileAsync(string fullPath, byte[] outBuffer, long fileSize, CancellationToken token)
        {
            using (var input = File.OpenRead(fullPath))
            {
                using (var messageStream = new MemoryStream(outBuffer))
                {
                    await input.CopyToAsync(messageStream, (int)fileSize, token).ConfigureAwait(false);
                }
            }
        }

        static bool TryReadNextIdMapping(BinaryReader reader, out FileIdMappingEntry entry)
        {
            entry = new FileIdMappingEntry();

            try
            {
                while (true)
                {
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        return false;

                    var isDeleted = reader.ReadBoolean();
                    var length = reader.ReadInt32();

                    if (isDeleted)
                    {
                        reader.BaseStream.Position += length;
                        continue;
                    }

                    var hash = reader.ReadString();
                    var source = reader.ReadString();
                    var typeName = reader.ReadString();
                    var type = ManifestActor.SyncModelTypes[typeName];
                    var name = reader.ReadString();
                    var entryId = reader.ReadBytes(16);
                    var stableId = reader.ReadBytes(16);

                    entry = new FileIdMappingEntry(false,
                        new EntryKey(hash, new StreamKey(source, new PersistentKey(type, name))),
                        (new EntryGuid(entryId), new EntryStableGuid(stableId)));

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        static bool TryReadNextSyncModelHeader(BinaryReader reader, out FileSyncModelHeader header)
        {
            header = new FileSyncModelHeader();

            try
            {
                while (true)
                {
                    if (reader.BaseStream.Position >= reader.BaseStream.Length)
                        return false;
                    
                    var isDeleted = reader.ReadBoolean();
                    var entryIdBytes = reader.ReadBytes(16);
                    var payloadOffset = reader.ReadInt64();
                    var payloadLength = reader.ReadInt32();

                    if (isDeleted)
                        continue;

                    header = new FileSyncModelHeader(false, new EntryGuid(entryIdBytes), payloadOffset, payloadLength);

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return false;
            }
        }

        static void WriteIdMappingEntry(FileIdMappingEntry entry, BinaryWriter writer)
        {
            writer.Write(false);
            var lengthOffset = writer.BaseStream.Position;
            writer.Write(-1);
            writer.Write(entry.Key.Hash);
            writer.Write(entry.Key.StreamKey.source);
            writer.Write(entry.Key.StreamKey.key.TypeName);
            writer.Write(entry.Key.StreamKey.key.Name);
            writer.Write(entry.Value.Id.GetUntypedGuid.ToByteArray());
            writer.Write(entry.Value.StableId.GetUntypedGuid.ToByteArray());

            // Now we know the length, rewrite over the -1 the size of the payload
            var endOffset = writer.BaseStream.Position;
            writer.BaseStream.Position = lengthOffset;
            writer.Write((int)(endOffset - lengthOffset - sizeof(int)));
            writer.BaseStream.Position = endOffset;
        }

        class TrackerCollection
        {
            public List<RpcContext<GetSyncModel, object>> OpenContexts = new List<RpcContext<GetSyncModel, object>>();
            public List<RpcContext<DownloadSyncModel, object>> DownloadContexts = new List<RpcContext<DownloadSyncModel, object>>();
        }

        async Task<List<SyncManifest>> GetSyncManifestsAsync(GetManifestOptions options, CancellationToken token)
        {
            if (m_Client != null)
            {
                // Online Mode
                var result = await m_Client.GetManifestsAsync(options, token);

                if (result == null)
                    throw new Exception("No manifest in the current project");

                var streamSources = new List<SyncManifest>();
                foreach (var manifestAsset in result)
                {
                    var manifest = manifestAsset.Manifest;
                    streamSources.Add(manifest);

                    await SaveManifestAsync(manifest);
                }

                streamSources = SyncManifest.PrioritizeDataSource(streamSources).ToList();
                return streamSources;
            }

            var manifests = await LoadManifestsAsync(token);
            if (!manifests.Any())
                throw new Exception("No manifest in the current project");

            return manifests;
        }

        async Task SaveManifestAsync(SyncManifest manifest)
        {
            var folder = Storage.cache.GetProjectFolder(m_Project);
            var fullPath = Path.Combine(folder, manifest.SourceId + ".manifest"); // TODO. Improve how sourceId is saved
            await PlayerFile.SaveManifestAsync(manifest, fullPath);
        }
        
        async Task<List<SyncManifest>> LoadManifestsAsync(CancellationToken token)
        {
            var folder = Storage.main.GetProjectFolder(m_Project);
            Directory.CreateDirectory(folder);

            var result = new List<SyncManifest>();

            foreach (var manifestFile in Directory.EnumerateFiles(folder, "*.manifest", SearchOption.AllDirectories))
            {
                if (manifestFile == null)
                    continue;

                var fi = new FileInfo(manifestFile);
                if (fi.Length == 0)
                {
                    File.Delete(manifestFile);
                    continue;
                }
                
                var syncManifest = await PlayerFile.LoadManifestAsync(manifestFile, token);

                result.Add(syncManifest);
            }

            return result;
        }

        async Task<SpatialManifest> GetSpatialManifestAsync(IEnumerable<SyncId> ids, GetNodesOptions getNodesOptions, CancellationToken token)
        {
            // TODO we could try the other storage when the first doesn't succeed
            var storage = m_Client != null ? Storage.cache : Storage.main;
            var spatialFolderPath = Path.Combine(storage.GetProjectFolder(m_Project), k_StorageSpatialFolder);
            return await LoadSpatialManifestAsync(ids, getNodesOptions, token, spatialFolderPath, m_Client);
        }

        public static async Task<SpatialManifest> LoadSpatialManifestAsync(IEnumerable<SyncId> ids, GetNodesOptions getNodesOptions, CancellationToken token, string spatialFolderPath, IPlayerClient client)
        {
            var mapping = await GetSpatialMapping(getNodesOptions.Version, token, spatialFolderPath);

            if (!ids.Any())
            {
                // No ids provided : we're looking for the root node

                if (client != null)
                {
                    // The PlayerClient is available, so we should call the rpc, in case there's a new versionId
                    var rootManifest = await client.GetNodesAsync(ids, getNodesOptions, token);

                    if (mapping == null || !rootManifest.VersionId.Equals(mapping.VersionId))
                    {
                        // The latest version is not present in the storage : we should store it
                        await SaveSpatialManifestInCacheAsync(rootManifest, mapping, spatialFolderPath, token);
                        SaveSpatialProjectInfoInCache(rootManifest.VersionId, spatialFolderPath, token);
                    }

                    return rootManifest;
                }
                else
                {
                    // Offline mode
                    if (mapping == null || string.IsNullOrEmpty(mapping.RootNodeId))
                        throw new Exception("The project does not exist in the storage. Download the project first.");

                    // We have the root ID, we can delegate to LoadFromList
                    var nodes = new List<SyncId>
                    {
                        new SyncId(mapping.RootNodeId)
                    };

                    return await LoadSpatialManifestFromList(nodes, mapping.VersionId, token, mapping, spatialFolderPath, client);
                }
            }
            else
            {
                return await LoadSpatialManifestFromList(ids, getNodesOptions.Version, token, mapping, spatialFolderPath, client);
            }
        }

        static async Task<SpatialMapping> GetSpatialMapping(string versionId, CancellationToken token, string spatialFolderPath)
        {
            var fileName = versionId;
            if (string.IsNullOrEmpty(versionId))
            {
                // The version is not provided : we should fetch the latest version stored
                var projectInfoPath = Path.Combine(spatialFolderPath, $"{k_StorageSpatialProjectInfoFile}");
                if (!File.Exists(projectInfoPath))
                    return null;

                var projectInfo = await k_CacheFilesLock.LockAsync(() => JsonUtility.FromJson<SpatialProjectInfo>(File.ReadAllText(projectInfoPath)), token);
                fileName = projectInfo.LatestVersion;
            }

            var mappingPath = Path.Combine(spatialFolderPath, $"{fileName}.{k_StorageSpatialMappingExtension}");
            if (!File.Exists(mappingPath))
                return null;

            return await k_CacheFilesLock.LockAsync(() => JsonUtility.FromJson<SpatialMapping>(File.ReadAllText(mappingPath)), token);
        }

        static async Task<SpatialManifest> LoadSpatialManifestFromList(IEnumerable<SyncId> ids, string versionId, CancellationToken token, SpatialMapping mapping, string spatialFolderPath, IPlayerClient client)
        {
            var (manifest, loadedNodes) = await LoadSpatialManifestFromStorageAsync(ids, token, mapping, spatialFolderPath);
            var missingNodes = ids.Except(loadedNodes).ToList();

            if (!missingNodes.Any())
                return manifest;

            // Some nodes can not be retrieved from storage ; we need to call the rpc
            if (client == null)
                return null;

            var options = new GetNodesOptions
            {
                Version = versionId
            };

            var missingNodesManifest = await client.GetNodesAsync(missingNodes, options, token);
            await SaveSpatialManifestInCacheAsync(missingNodesManifest, mapping, spatialFolderPath, token);

            return SpatialManifest.Merge(new List<SpatialManifest> { manifest, missingNodesManifest });
        }

        static async Task<(SpatialManifest, IEnumerable<SyncId>)> LoadSpatialManifestFromStorageAsync(IEnumerable<SyncId> ids, CancellationToken token, SpatialMapping mapping, string spatialFolderPath)
        {
            if (mapping == null)
                return (null, new List<SyncId>());

            var loadedSpatialManifestFiles = new HashSet<string>();
            var loadedNodes = new List<SyncId>();
            var loadedManifests = new List<SpatialManifest>();

            // Go through the mapping to list the spatialmanifest files we should load
            foreach (var nodeId in ids)
            {
                if (mapping.TryGetValue(nodeId.Value, out var fileName))
                {
                    if (!loadedSpatialManifestFiles.Contains(fileName))
                    {
                        var fullPathToManifest = Path.Combine(spatialFolderPath, $"{fileName}.{k_StorageSpatialManifestExtension}");

                        SpatialManifest manifest;
                        try
                        {
                            manifest = await PlayerFile.LoadSpatialManifestAsync(fullPathToManifest, token);
                        }
                        catch
                        {
                            // Failed to load spatial manifest from cache
                            continue;
                        }

                        loadedManifests.Add(manifest);
                    }

                    loadedSpatialManifestFiles.Add(fileName);
                    loadedNodes.Add(nodeId);
                }
            }

            return (SpatialManifest.Merge(loadedManifests), loadedNodes);
        }

        static async Task SaveSpatialManifestInCacheAsync(SpatialManifest manifest, SpatialMapping mapping, string spatialFolderPath, CancellationToken token)
        {
            Directory.CreateDirectory(spatialFolderPath);

            // Save the spatialmanifest file
            var fileName = CreateSpatialManifestFileName(manifest);
            var filePath = Path.Combine(spatialFolderPath, $"{fileName}.{k_StorageSpatialManifestExtension}");
            await k_CacheFilesLock.LockAsync(() => PlayerFile.SaveSpatialManifestAsync(manifest, filePath), token);
            

            // Add the spatialmanifest to the mapping
            if (mapping == null)
                mapping = new SpatialMapping();

            foreach (var node in manifest.SyncNodes)
            {
                mapping[node.Id.Value] = fileName;

                if (node.IsRoot)
                {
                    mapping.RootNodeId = node.Id.Value;
                    mapping.VersionId = manifest.VersionId;
                }
            }

            var mappingPath = Path.Combine(spatialFolderPath, $"{mapping.VersionId}.{k_StorageSpatialMappingExtension}");
            await k_CacheFilesLock.LockAsync(() => File.WriteAllText(mappingPath, JsonUtility.ToJson(mapping)), token);
        }

        static async void SaveSpatialProjectInfoInCache(string versionId, string spatialFolderPath, CancellationToken token)
        {
            var projectInfo = new SpatialProjectInfo {
                LatestVersion = versionId
            };

            var filePath = Path.Combine(spatialFolderPath, $"{k_StorageSpatialProjectInfoFile}");
            await k_CacheFilesLock.LockAsync(() => File.WriteAllText(filePath, JsonUtility.ToJson(projectInfo)), token);
        }

        static string CreateSpatialManifestFileName(SpatialManifest manifest)
        {
            // TODO we should use a deterministic name for the spatialmanifest files
            return Guid.NewGuid().ToString();
        }

        void UpdateNbConcurrentTasks(int newValue)
        {
            m_Settings.NbConcurrentTasks = m_IO.NbConcurrentTasks = newValue <= 0 ? SystemInfo.processorCount * 8 : newValue;
        }

        struct UpdateTracker
        {
            public RpcContext<GetManifests> Ctx;
        }
        
        struct FileIdMappingEntry
        {
            public bool IsDeleted;
            public EntryKey Key;
            public (EntryGuid Id, EntryStableGuid StableId) Value;

            public FileIdMappingEntry(bool isDeleted, EntryKey key, (EntryGuid Id, EntryStableGuid StableId) value)
            {
                IsDeleted = isDeleted;
                Key = key;
                Value = value;
            }
        }

        struct FileSyncModelHeader
        {
            public bool IsDeleted;
            public EntryGuid EntryId;
            public long PayloadOffset;
            public int PayloadLength;
            
            public FileSyncModelHeader(bool isDeleted, EntryGuid entryId, long payloadOffset, int payloadLength)
            {
                IsDeleted = isDeleted;
                EntryId = entryId;
                PayloadOffset = payloadOffset;
                PayloadLength = payloadLength;
            }
        }

        struct EntryKey : IEquatable<EntryKey>
        {
            public string Hash;
            public StreamKey StreamKey;

            public EntryKey(string hash, StreamKey streamKey)
            {
                Hash = hash;
                StreamKey = streamKey;
            }

            public bool Equals(EntryKey other) => Hash == other.Hash && StreamKey.Equals(other.StreamKey);
            public override bool Equals(object obj) => obj is EntryKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Hash != null ? Hash.GetHashCode() : 0) * 397) ^ StreamKey.GetHashCode();
                }
            }
            public static bool operator==(EntryKey left, EntryKey right) => left.Equals(right);
            public static bool operator !=(EntryKey left, EntryKey right) => !(left == right);
        }

        public class FileLock
        {
            readonly SemaphoreSlim m_Semaphore = new SemaphoreSlim(1, 1);
            public async Task LockAsync(Func<Task> worker, CancellationToken token)
            {
                await m_Semaphore.WaitAsync(token);
                try
                {
                    await worker();
                }
                finally
                {
                    m_Semaphore.Release();
                }
            }
            
            public async Task<T> LockAsync<T>(Func<Task<T>> worker, CancellationToken token)
            {
                await m_Semaphore.WaitAsync(token);
                try
                {
                    return await worker();
                }
                finally
                {
                    m_Semaphore.Release();
                }
            }

            public async Task LockAsync(Action worker, CancellationToken token)
            {
                await m_Semaphore.WaitAsync(token);
                try
                {
                    worker();
                }
                finally
                {
                    m_Semaphore.Release();
                }
            }

            public async Task<T> LockAsync<T>(Func<T> worker, CancellationToken token)
            {
                await m_Semaphore.WaitAsync(token);
                try
                {
                    return worker();
                }
                finally
                {
                    m_Semaphore.Release();
                }
            }
        }

        public class Settings : ActorSettings
        {
            [Tooltip("Number of tasks that may run simultaneously. A value <= 0 " +
                "will fallback to the number of cores of the machine.")]
            public int NbConcurrentTasks;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }

        [Serializable]
        public class SpatialMapping : SerializedDictionary<string, string>
        {
            [SerializeField] public string RootNodeId;
            [SerializeField] public string VersionId;
        }

        [Serializable]
        public class SpatialProjectInfo
        {
            [SerializeField] public string LatestVersion;
        }
    }
}
