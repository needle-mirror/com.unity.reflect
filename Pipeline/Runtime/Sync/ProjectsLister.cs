using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace UnityEngine.Reflect.Pipeline
{
    public interface IStream
    {
    }

    public struct StreamAsset : IStream
    {
        public readonly StreamKey key;
        public readonly string hash;
        public readonly SyncBoundingBox boundingBox;

        public StreamAsset(string sourceId, PersistentKey key, string hash, SyncBoundingBox boundingBox)
        {
            this.key = new StreamKey(sourceId, key);
            this.hash = hash;
            this.boundingBox = boundingBox;
        }
    }
    
    public struct StreamInstance : IStream
    {
        public readonly StreamKey key;
        public readonly SyncObjectInstance instance;
        public readonly SyncBoundingBox boundingBox;

        public StreamInstance(StreamKey key, SyncObjectInstance instance, SyncBoundingBox boundingBox)
        {
            this.key = key;
            this.instance = instance;
            this.boundingBox = boundingBox;
        }
    }

    public interface IProjectListerSettings
    {
        void OnProjectListingStarted();
        void OnProjectListingCompleted(IEnumerable<Project> projects);
    }

    [Serializable]
    public class ProjectListerSettings : IProjectListerSettings
    {
        [Header("Events")]
        public UnityEvent OnProjectsRefreshStarted;
        
        [Serializable]
        public class ProjectsEvents : UnityEvent<List<Project>> { }
        public ProjectsEvents OnProjectsRefreshCompleted;

        void IProjectListerSettings.OnProjectListingStarted()
        {
            OnProjectsRefreshStarted.Invoke();
        }

        void IProjectListerSettings.OnProjectListingCompleted(IEnumerable<Project> projects)
        {
            OnProjectsRefreshCompleted.Invoke(projects.ToList());
        }
    }

    public class ProjectsLister : ReflectTask
    {
        readonly IProjectListerSettings m_Settings;

        public ProjectsLister(IProjectListerSettings settings)
        {
            m_Settings = settings;
        }

        Task m_SubTask;

        protected override Task RunInternal(CancellationToken token)
        {
            m_Settings.OnProjectListingStarted();
            
            m_SubTask = client.ListProjects();

            return m_SubTask;
        }

        protected override void UpdateInternal(float unscaledDeltaTime)
        {
            if (m_SubTask == null)
                return;
            
            if (!m_SubTask.IsCompleted)
                return;

            if (m_SubTask.IsFaulted)
                return;

            var result = GetResult(m_SubTask);
            
            m_Settings.OnProjectListingCompleted(result);

            m_SubTask = null;
            m_Task = null;
        }

        static IEnumerable<Project> GetResult(Task task)
        {
            var result = ((Task<UnityProjectCollection>)task).Result;
            
            if (result.Status == UnityProjectCollection.StatusOption.AuthenticationError)
            {
                //onAuthenticationFailure?.Invoke();
                Debug.LogError("Authentication Error : " + result.ErrorMessage);
                // NOTE: Keep on going, we may be able to display offline data
            }
            
            else if (result.Status == UnityProjectCollection.StatusOption.ComplianceError)
            {
                //onError?.Invoke(new ProjectListRefreshException(result.ErrorMessage, result.Status));
                Debug.LogException(new ProjectListRefreshException(result.ErrorMessage, result.Status));
                // NOTE: Keep on going, we may be able to display offline data
            }                
            else if (result.Status != UnityProjectCollection.StatusOption.Success)
            {
                // Log error with details but report simplified message in `onError` event
                //Debug.LogError($"Project list refresh failed: {result.ErrorMessage}");
                //onError?.Invoke(new ProjectListRefreshException($"Could not connect to Reflect cloud service, check your internet connection.", result.Status));
                Debug.LogException(new ProjectListRefreshException($"Could not connect to Reflect cloud service, check your internet connection.", result.Status));
                // NOTE: Keep on going, we may be able to display offline data
            }
            
            return result.Select(p => new Project(p));
        }

        public IProjectProvider client { get; set; }
    }
}
