using Grpc.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Reflect;

namespace UnityEngine.Reflect
{
    class ProjectManagerWithProjectServerInternal : ProjectManagerInternal
    {
        public event Action onAuthenticationFailure;

        public ProjectManagerWithProjectServerInternal(string storageRoot, bool useServerFolder, bool useProjectNameAsRootFolder)
            : base(storageRoot, useServerFolder, useProjectNameAsRootFolder)
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

        public IEnumerator RefreshProjectListCoroutine()
        {
            var user = ProjectServerEnvironment.UnityUser;
            if (user == null)
            {
                yield break;
            }

            // Use ContinueWith to make sure the task doesn't throw
            var task = Task.Run(() => ProjectServerEnvironment.Client.ListProjects(user.AccessToken)).ContinueWith(t => t);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            var listTask = task.Result;
            if (listTask.IsFaulted)
            {
                Debug.LogError($"Project list refresh failed: {listTask.Exception}");
                if (listTask.Exception.InnerExceptions.OfType<RpcException>().Where(x => x.StatusCode.Equals(StatusCode.Unauthenticated) || x.StatusCode.Equals(StatusCode.PermissionDenied)).FirstOrDefault() != null)
                {
                    onAuthenticationFailure?.Invoke();
                }
                yield break;
            }

            var onlineProjects = listTask.Result.Select(p => new Project(p)).ToList();
            var onlineProjectIds = onlineProjects.Select(p => p.serverProjectId).ToArray();

            m_UserProjects[user.UserId] = onlineProjectIds;
            SaveUserProjectList();

            foreach (var entry in Projects)
            {
                if (!onlineProjectIds.Contains(entry.serverProjectId))
                {
                    entry.isAvailableOnline = false;
                    UpdateProjectInternal(entry, false);
                }
            }

            onlineProjects.ForEach(p => UpdateProjectInternal(p, true));
        }

        public override void Update()
        {
        }
    }
}
