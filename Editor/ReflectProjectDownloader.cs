using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

class ReflectProjectDownloader
{
    public event Action<Exception> onError;
    public event Action<float> onProgressChanged;

    static readonly string k_TemporaryFolder = ".download";

    object m_ProgressLock = new object();
    float m_Progress;

    readonly string m_Folder;

    public ReflectProjectDownloader(string folder)
    {
        m_Folder = folder;
    }

    public IEnumerator Download(Project project)
    {
        IPlayerClient client = null;
        try
        {
            client = Player.CreateClient(project, ProjectServerEnvironment.UnityUser, ProjectServerEnvironment.Client);
        }
        catch (ConnectionException ex)
        {
            var unityProject = (UnityProject)project;
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

            throw new ConnectionException(message, ex);
        }
        
        var task = Task.Run(() => DownloadProject(client, project, m_Folder));

        var lastProgress = 0.0f;

        onProgressChanged?.Invoke(0.0f);
        
        do
        {
            lock (m_ProgressLock)
            {
                if (lastProgress != m_Progress)
                {
                    lastProgress = m_Progress;
                    onProgressChanged?.Invoke(m_Progress);
                }
            }

            yield return null;

        } while (!task.IsCompleted);

        if (task.IsFaulted)
        {
            onError?.Invoke(task.Exception);
        }
        
        onProgressChanged?.Invoke(1.0f);
    }

    async Task DownloadProject(IPlayerClient client, Project project, string folder)
    {
        lock (m_ProgressLock)
        {
            m_Progress = 0.0f;
        }
        
        var storage = new PlayerStorage(folder, false, true);
        
        try
        {
            var manifestEntries = (await client.GetManifestsAsync()).ToArray();

            onProgressChanged?.Invoke(0.0f);

            var total = manifestEntries.Length;
            var projectPercent = 1.0f / total;

            var localManifests = new Dictionary<string, SyncManifest>();

            IEnumerable<SourceProject> localSourceProjects = null;

            try
            {
                localSourceProjects = storage.LoadProjectManifests(project).ToArray();
            }
            catch (ReflectVersionException)
            {
                if (manifestEntries.Length == 0)
                {
                    throw new Exception($"Cannot open project {project.name} because it has been exported with a different version of Unity Reflect.");
                }
            }

            if (localSourceProjects != null)
            {
                foreach (var sourceProject in localSourceProjects)
                {
                    localManifests.Add(sourceProject.sourceId, sourceProject.manifest);
                }
            }

            storage.SaveProjectData(project);

            for (int i = 0; i < total; ++i)
            {
                var manifestEntry = manifestEntries[i];

                localManifests.TryGetValue(manifestEntry.SourceId, out var oldManifest);

                await DownloadManifestDiff(client, oldManifest, manifestEntry.Manifest, project, manifestEntry.SourceId, storage);
            }
        }
        finally
        {
            client?.Dispose();
        }

        lock (m_ProgressLock)
        {
            m_Progress = 1.0f;
        }
    }

    async Task DownloadManifestDiff(IPlayerClient client, SyncManifest oldManifest, SyncManifest newManifest,
        UnityProject project, string sourceId, PlayerStorage storage)
    {
        List<ManifestEntry> entries;

        if (oldManifest == null)
        {
            var content = newManifest.Content;
            entries = content.Values.ToList();
        }
        else
        {
            ParseManifest(oldManifest.Content, newManifest.Content, out var modified, out var deleted);
            entries = modified.ToList();

            // TODO Handle deleted models
        }

        var destinationFolder = storage.GetSourceProjectFolder(project, sourceId);
        var downloadFolder = Path.Combine(destinationFolder, k_TemporaryFolder);

        var progress = new ProjectManagerInternal.DownloadProgress();
        progress.SetTotal(entries.Count);
            
        var tasks = entries.Select(entry => ProjectManagerInternal.DownloadAndStore(client, sourceId, entry, newManifest, downloadFolder, progress)).ToList();

        // Don't forget the manifest itself
        tasks.Add(ProjectManagerInternal.RunFileIOOperation(() =>
        {
            newManifest.EditorSave(downloadFolder);
            return Task.CompletedTask;
        }));
        
                    
        // Wait for all download to finish
        var task = Task.WhenAll(tasks);

        while (!task.IsCompleted)
        {
            lock (m_ProgressLock)
            {
                m_Progress = progress.percent;
            }

            await Task.Delay(200);
        }

        // TODO Handle errors in the DownloadProgress
        
        // Backward compatibility with local viewer cache that have SyncPrefab as a file.
        var prefabPath = SyncInstance.GetPrefabPath(downloadFolder);

        if (prefabPath == null)
        {
            var prefabName = sourceId + SyncPrefab.Extension;
            var syncPrefab =  SyncInstance.GenerateSyncPrefabFromManifest(prefabName, downloadFolder, newManifest);
            var fullPath = Path.Combine(downloadFolder, prefabName);

            PlayerFile.Save(syncPrefab, fullPath);
        }

        // Delete SyncInstances since they cannot be imported
        var instancesFolder = Path.Combine(downloadFolder, "instances");
        if (Directory.Exists(instancesFolder))
        {
            Directory.Delete(instancesFolder, true);
        }
        
        // Move all content from temporary download folder to the final destination
        MoveDirectory(downloadFolder, destinationFolder);
    }
    
    // Directory.Move does not support partial moves / overrides.
    static void MoveDirectory(string source, string target)
    {
        var sourcePath = FormatPath(source);
        var targetPath = FormatPath(target);
        var files = Directory.EnumerateFiles(sourcePath, "*", SearchOption.AllDirectories).GroupBy(Path.GetDirectoryName);

        foreach (var folder in files)
        {
            var targetFolder = FormatPath(folder.Key).Replace(sourcePath, targetPath);
            Directory.CreateDirectory(targetFolder);
            foreach (var file in folder)
            {
                var targetFile = Path.Combine(targetFolder, Path.GetFileName(file));

                if (System.IO.File.Exists(targetFile))
                {
                    System.IO.File.Delete(targetFile);
                }

                System.IO.File.Move(file, targetFile);
            }
        }
        Directory.Delete(source, true);
    }
    
    static string FormatPath(string path)
    {
        return path.Replace("\\", "/").TrimEnd('/', ' ');
    }

    static void ParseManifest(IReadOnlyDictionary<PersistentKey, ManifestEntry> oldContent, IReadOnlyDictionary<PersistentKey, ManifestEntry> newContent,
        out IList<ManifestEntry> changed, out IList<ManifestEntry> removed)
    {
        changed = new List<ManifestEntry>();
        removed = new List<ManifestEntry>();

        foreach (var couple in oldContent)
        {
            var persistentKey = couple.Key;

            // Ignore SyncObjectInstances since we need to re-download them anyway to construct the Prefab
            if (persistentKey.IsKeyFor<SyncObjectInstance>())
                continue;

            if (newContent.TryGetValue(persistentKey, out var newData))
            {
                if (!Compare(couple.Value, newData))
                {
                    changed.Add(newData);
                }
            }
            else
            {
                removed.Add(couple.Value);
            }
        }

        foreach (var couple in newContent)
        {
            var persistentKey = couple.Key;

            if (persistentKey.IsKeyFor<SyncObjectInstance>() || !oldContent.ContainsKey(persistentKey))
            {
                changed.Add(couple.Value);
            }
        }
    }

    static bool Compare(ManifestEntry first, ManifestEntry second)
    {
        return first.Hash == second.Hash
            && string.Equals(first.ModelPath, second.ModelPath, StringComparison.CurrentCultureIgnoreCase);
    }
}