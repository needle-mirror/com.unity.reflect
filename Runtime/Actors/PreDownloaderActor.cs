using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.ActorFramework;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    [Actor("b98734be-8837-4a8b-baab-c045b22c087f")]
    public class PreDownloaderActor
    {
#pragma warning disable 649
        Settings m_Settings;
        RpcOutput<DownloadSyncModel> m_DownloadSyncModelOutput;
        EventOutput<DownloadProgressed> m_DownloadedProgressedOutput;
#pragma warning restore 649

        int m_NbRunningDownloads;
        int m_NbAssets;
        Dictionary<EntryGuid, EntryData> m_PendingDownloads = new Dictionary<EntryGuid, EntryData>();

        [NetInput]
        void OnEntryDataChanged(NetContext<EntryDataChanged> ctx)
        {
            var delta = ctx.Data.Delta;
            
            foreach (var added in delta.Added)
                m_PendingDownloads.Add(added.Id, added);

            foreach (var removed in delta.Removed)
                m_PendingDownloads.Remove(removed.Id);

            foreach (var changed in delta.Changed)
            {
                m_PendingDownloads.Remove(changed.Prev.Id);
                m_PendingDownloads.Add(changed.Next.Id, changed.Next);
            }

            m_NbAssets += delta.Added.Count - delta.Removed.Count;
            m_DownloadedProgressedOutput.Broadcast(new DownloadProgressed(m_NbAssets - m_PendingDownloads.Count, m_NbAssets));

            TryEnqueueDownloads();
        }

        void TryEnqueueDownloads()
        {
            while (m_NbRunningDownloads < m_Settings.NbConcurrentDownloads && m_PendingDownloads.Count > 0)
            {
                ++m_NbRunningDownloads;
                var entry = m_PendingDownloads.First().Value;
                m_PendingDownloads.Remove(entry.Id);
                var rpc = m_DownloadSyncModelOutput.Call(this, (object)null, entry, new DownloadSyncModel(entry));
                rpc.Success((self, ctx, entry, _) =>
                {
                    --m_NbRunningDownloads;
                    m_DownloadedProgressedOutput.Broadcast(new DownloadProgressed(m_NbAssets - m_PendingDownloads.Count, m_NbAssets));
                    self.TryEnqueueDownloads();
                });
                rpc.Failure((self, ctx, entry, ex) =>
                {
                    if (ex is OperationCanceledException)
                        return;
                    if (ex is InsufficientMemoryException)
                    {
                        m_PendingDownloads.Add(entry.Id, entry);
                        return;
                    }

                    Debug.LogException(ex);
                });
            }
        }

        public class Settings : ActorSettings
        {
            public int NbConcurrentDownloads = 128;

            public Settings()
                : base(Guid.NewGuid().ToString()) { }
        }
    }
}
