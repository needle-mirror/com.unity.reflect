using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Grpc.Core;
using Unity.Reflect.Services.Client.ProjectServer;
using UnityEditor;

namespace UnityEngine.Reflect
{
    class ProjectManagerWithProjectServerInternal : ProjectManagerInternal
    {
        private static CloudConfiguration.Environment m_ProjectServerEnvironment = CloudConfiguration.Environment.Production;

        private static readonly Dictionary<string, CloudConfiguration.Environment> k_ProjectServerEnvironmentMap = new Dictionary<string, CloudConfiguration.Environment>()
        {
            { "dev", CloudConfiguration.Environment.Test },
            { "staging", CloudConfiguration.Environment.Staging },
            { "production", CloudConfiguration.Environment.Production },
        };

        public event Action onAuthenticationFailure;

        static ProjectManagerWithProjectServerInternal()
        {
#if UNITY_EDITOR
            var asm = Assembly.GetAssembly(typeof(CloudProjectSettings));
            var unityConnect = asm.GetType("UnityEditor.Connect.UnityConnect");
            var instanceProperty = unityConnect.GetProperty("instance");
            var configurationProperty = unityConnect.GetProperty("configuration");

            var instance = instanceProperty.GetValue(null, null);
            var envValue = (string)configurationProperty.GetValue(instance, null);

            if (!k_ProjectServerEnvironmentMap.TryGetValue(envValue?.ToLower() ?? string.Empty, out m_ProjectServerEnvironment))
            {
                Debug.LogWarning($"Could not find cloud config environment, using production environment.");
            }
#endif
        }

        public ProjectManagerWithProjectServerInternal(string storageRoot, bool useProjectNameAsRootFolder)
            : base(storageRoot, useProjectNameAsRootFolder)
        {
        }

        public ProjectManagerWithProjectServerInternal()
            : base()
        {
        }

        public override void StartDiscovery()
        {            
        }

        public override void StopDiscovery()
        {
        }

        public override void OnEnable()
        {
        }

        public override void OnDisable()
        {
        }

        public IEnumerator RefreshProjectListCoroutine(string accessToken)
        {
#if UNITY_EDITOR            
            accessToken = CloudProjectSettings.accessToken;
#endif
            if (string.IsNullOrEmpty(accessToken))
            {
                onAuthenticationFailure?.Invoke();
                yield break;
            }

            var client = new ProjectServerClient(accessToken, m_ProjectServerEnvironment);
    
            // Use ContinueWith to make sure the task doesn't throw        
            var task = Task.Run(() => client.ListProjects()).ContinueWith(t => t);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            var listTask = task.Result;
            if (listTask.IsFaulted)
            {
                Debug.LogError($"Project list refresh failed: {listTask.Exception}");

                var rpcException = listTask.Exception.InnerExceptions.OfType<RpcException>().FirstOrDefault();
                if (rpcException != null)
                {
                    if (rpcException.StatusCode.Equals(StatusCode.Unauthenticated) || rpcException.StatusCode.Equals(StatusCode.PermissionDenied))
                    {
                        onAuthenticationFailure?.Invoke();
                    }
                }
                yield break;
            }

            var onlineProjectIds = new HashSet<string>(listTask.Result.Select(p => p.Id));
            foreach (var entry in Projects)
            {
                if (!onlineProjectIds.Contains(entry.serverProjectId))
                {
                    entry.channel = null;
                    UpdateProjectInternal(entry, false);
                }
            }

            foreach (var unityProject in listTask.Result)
            {
                var project = new Project(unityProject.Id, unityProject.ProjectId, unityProject.Name, unityProject.TargetChannel.Name, unityProject.TargetChannel);
                UpdateProjectInternal(project, true);
            }
        }

        public override void Update()
        {     
        }
    }
}
