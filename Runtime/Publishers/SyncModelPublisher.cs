using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Reflect.Model;
using Unity.Reflect.Source.Utils.Errors;
using UnityEngine;

namespace Unity.Reflect.Viewer.Publishers
{
    /// <summary>
    /// This Class publishes sync models of a type <T> to its own source.
    /// This is useful to create features where you want to save and sync data relative to that type.
    /// </summary>
    public class SyncModelPublisher<T> : IDisposable where T : ISyncModel
    {
        IPublisherClient m_PublisherClient;
        readonly string m_Extension;

        public SyncModelPublisher()
        {
            m_Extension = typeof(T).Name;
        }

        ~SyncModelPublisher()
        {
            Dispose();
        }

        public void Dispose()
        {
            Disconnect();
        }

        public void Disconnect()
        {
            m_PublisherClient?.CloseAndWait();
        }

        public void UpdateProject(UnityProject project, UnityUser user)
        {
            Disconnect();

            try
            {
                m_PublisherClient = CreatePublisher(project, user);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                m_PublisherClient?.CloseAndWait();
            }

            if (m_PublisherClient == null)
            {
                Debug.LogError("Publisher failed to open");
                throw new NullReferenceException("Publisher Missing");
            }

            if (!m_PublisherClient.IsTypeSendable<T>())
            {
                Debug.LogError($"{m_Extension} not supported");
                throw new SyncModelNotSupportedException($"{m_Extension} not supported by the project host");
            }
        }

        IPublisherClient CreatePublisher(UnityProject project, UnityUser user)
        {
            // This is the public name of the Source Project you want to export (it doesn't have to be unique).
            var sourceName = $"{project.Name}_{m_Extension}";
            // This identifies the Source Project you want to export and must be unique and persistent over multiple publishing sessions.
            var sourceId = $"{project.ProjectId}_{m_Extension}";

            // Parse the version number. Start by removing any non-numbers.
            var versionString = Regex.Replace(Application.version, "[^0-9.]", "");
            if (!Version.TryParse(versionString, out var version))
            {
                Debug.LogWarning($"Failed to parse Application.version {versionString}");
                version = new Version(0, 0);
            }

            //Create the publisher settings for the client
            var settings = new PublisherSettings(project, user)
            {
                PluginName = $"{Application.productName}_{m_Extension}Publisher",
                PluginVersion = version,
                LengthUnit = LengthUnit.Meters,
                AxisInversion = AxisInversion.None
            };

            // Create a Publisher Client, that will allow us to publish data into the selected Unity Project.
            return Publisher.OpenClient(sourceName, sourceId, settings, false);
        }

        public void PerformUpdate(IList<T> syncModels)
        {
            // Start a transaction and attach it to the publisher client.
            // Note that the publisher client can only be attached to one transaction at a time.
            using (var transaction = m_PublisherClient.StartTransaction())
            {
                PerformUpdate(transaction, syncModels);

                transaction.Commit();
            }
        }

        public async Task PerformUpdateAsync(IList<T> syncModels)
        {
            using (var transaction = m_PublisherClient.StartTransaction())
            {
                PerformUpdate(transaction, syncModels);

                await transaction.CommitAsync();
            }
        }

        static void PerformUpdate(PublisherTransaction transaction, IList<T> syncModels)
        {
            for (var i = 0; i < syncModels.Count; ++i)
            {
                transaction.Send(syncModels[i]);
            }
        }

        public void Delete(IList<SyncId> deletedSyncModelIds, IList<T> remainingSyncModels)
        {
            using (var transaction = m_PublisherClient.StartTransaction())
            {
                Delete(transaction, deletedSyncModelIds, remainingSyncModels);

                transaction.Commit();
            }
        }

        public async Task DeleteAsync(IList<SyncId> deletedSyncModelIds, IList<T> remainingSyncModels)
        {
            using (var transaction = m_PublisherClient.StartTransaction())
            {
                Delete(transaction, deletedSyncModelIds, remainingSyncModels);

                await transaction.CommitAsync();
            }
        }

        static void Delete(PublisherTransaction transaction, IList<SyncId> deletedSyncModelIds,
            IList<T> remainingSyncModels)
        {
            for (var i = 0; i < deletedSyncModelIds.Count; ++i)
            {
                transaction.RemoveObjectInstance(deletedSyncModelIds[i]);
            }

            for (var i = 0; i < remainingSyncModels.Count; ++i)
            {
                transaction.Send(remainingSyncModels[i]);
            }
        }
    }
}
