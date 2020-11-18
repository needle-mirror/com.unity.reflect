using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.IO;
using Unity.Reflect.Model;

[assembly: InternalsVisibleTo("Unity.Reflect.Editor")]
namespace UnityEngine.Reflect
{
    [Serializable]
    public class Project
    {
        readonly UnityProject m_UnityProject;

        public static Project Empty { get; } = new Project(new UnityProject(UnityProjectHost.LocalService, string.Empty, string.Empty));
        public string serverProjectId => $"{m_UnityProject.Host.ServerId}:{m_UnityProject.ProjectId}";
        public string projectId => m_UnityProject.ProjectId;
        public string name => m_UnityProject.Name;
        public UnityProjectHost host => m_UnityProject.Host;
        public string description => m_UnityProject.Host.ServerName;
        public bool isAvailableOnline => m_UnityProject.Source == UnityProject.SourceOption.ProjectServer;

        public DateTime lastPublished => m_UnityProject.LastPublished;

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
