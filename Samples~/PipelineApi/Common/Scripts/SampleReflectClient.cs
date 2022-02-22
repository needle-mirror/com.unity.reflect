using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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
            // moving one step up from Assets folder allows us to also find the .SampleData directly in the package for testing purposes
            m_DataFolder = Directory.EnumerateDirectories(Application.dataPath.Replace(@"/Assets", ""), ".SampleData", SearchOption.AllDirectories).FirstOrDefault();

            if (m_DataFolder == null)
            {
                Debug.LogError("Unable to find Samples data. Reflect Samples require local Reflect Model data in 'Reflect/Common/.SampleData'.");
            }
        }

        public async Task<IEnumerable<SyncManifest>> GetSyncManifestsAsync(CancellationToken token)
        {
            var syncManifestPath = Directory.EnumerateFiles(m_DataFolder, "*.manifest").FirstOrDefault();
            var syncManifest = await PlayerFile.LoadManifestAsync(syncManifestPath, token);

            return new [] { syncManifest };
        }

        public async Task<ISyncModel> GetSyncModelAsync(StreamKey streamKey, string hash, CancellationToken token)
        {
            var fullPath = Path.Combine(m_DataFolder, hash + PlayerFile.PersistentKeyToExtension(streamKey.key));
            return await PlayerFile.LoadSyncModelAsync(fullPath, streamKey.key, token);
        }
    }
}
