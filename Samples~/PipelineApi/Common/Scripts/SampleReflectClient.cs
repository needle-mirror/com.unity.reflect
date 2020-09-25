using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect.Pipeline;

namespace Unity.Reflect.Samples
{
    public class SampleSyncModelProvider : ISyncModelProvider
    {
        string m_DataFolder;

        public SampleSyncModelProvider()
        {
            m_DataFolder = Directory.EnumerateDirectories(Application.dataPath, ".SampleData", SearchOption.AllDirectories).FirstOrDefault();

            if (m_DataFolder == null)
            {
                Debug.LogError("Unable to find Samples data. Reflect Samples require local Reflect Model data in 'Reflect/Common/.SampleData'.");
            }
        }

        public async Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync()
        {
            var syncManifestPath = Directory.EnumerateFiles(m_DataFolder, "*.manifest").FirstOrDefault();
            var syncManifest = await PlayerFile.LoadManifestAsync(syncManifestPath);

            return new [] { syncManifest };
        }

        public async Task<ISyncModel> GetSyncModelAsync(StreamKey streamKey, string hash)
        {
            return await PlayerFile.LoadSyncModelAsync(m_DataFolder, streamKey.key, hash);
        }
    }
}
