using System;
using System.Runtime.CompilerServices;
using Unity.Reflect;

[assembly: InternalsVisibleTo("Unity.Reflect.Editor")]
namespace UnityEngine.Reflect
{
    [Serializable]
    public class Project
    {
        protected readonly UnityProject m_UnityProject;

        public static Project Empty { get; } = new Project(new UnityProject(string.Empty, string.Empty));
        public virtual string serverProjectId => $"{m_UnityProject.Host.ServerId}:{m_UnityProject.ProjectId}";
        public virtual string projectId => m_UnityProject.ProjectId;
        public virtual string name => m_UnityProject.Name;
        public virtual UnityProjectHost host => m_UnityProject.Host;
        public virtual string description => m_UnityProject.Host.ServerName;
        public virtual UnityProject UnityProject => m_UnityProject;
        public virtual DateTime lastPublished => m_UnityProject.LastPublished;

        public virtual DateTime DownloadedPublished { get; set; }

        public virtual bool IsLocal { get; set; }

        public virtual bool HasUpdate => IsLocal && m_UnityProject.LastPublished != DownloadedPublished;

        public virtual bool IsConnectedToServer => m_UnityProject.IsConnectedToServer;
        
        public virtual bool hasUpdate => m_UnityProject.LastPublished != DownloadedPublished;

        public Project(UnityProject unityProject)
        {
            m_UnityProject = unityProject;
        }

        public static implicit operator UnityProject(Project project)
        {
            return project.m_UnityProject;
        }
    }

    public class ProjectListRefreshException : Exception
    {
        public UnityProjectCollection.StatusOption Status { get; }

        public ProjectListRefreshException(string message, UnityProjectCollection.StatusOption status)
            : base(message)
        {
            Status = status;
        }
    }
}
