using Grpc.Core;
using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Reflect;
using Unity.Reflect.Source.Utils.Errors;
using UnityEngine.Events;

namespace UnityEngine.Reflect
{
    [Serializable]
    public class SharingLinkEvent : UnityEvent<SharingLinkInfo> { }

    [Serializable]
    public class ProjectInfoEvent : UnityEvent<UnityProject> { }
    
    [Serializable]
    public class ExceptionEvent : UnityEvent<Exception> {}

    [Serializable]
    public class SharedLinkExceptionEvent : UnityEvent<SharedLinkException> { }

    public enum SharedLinkException 
    {
        NotFoundException,
        ConnectionException,
        AccessDeniedException
    }

    public class LinkSharingManager : MonoBehaviour
    {
        public SharingLinkEvent sharingLinkCreated;
        public SharingLinkEvent setLinkPermissionDone;
        public ProjectInfoEvent linkSharingProjectInfoEvent;
        public ExceptionEvent linkCreatedExceptionEvent;
        public SharedLinkExceptionEvent projectInfoExceptionEvent;
        
        Task<UnityProject> m_ProjectInfoTask;
        Task<SharingLinkInfo> m_GetSharingLinkTask;
        Task<SharingLinkInfo> m_SetLinkPermissionTask;
        
        Coroutine m_GetProjectInfoCoroutine;
        Coroutine m_GetLinkTokenCoroutine;
        Coroutine m_SetLinkPermissionCoroutine;

        public void ProcessSharingToken(string cloudServiceAccessToken, string linkToken)
        {
            m_ProjectInfoTask = Task.Run(() => ProjectServer.Client.GetSharingProjectInfo(cloudServiceAccessToken, linkToken));

            if (m_GetProjectInfoCoroutine != null)
            {
                StopCoroutine(m_GetProjectInfoCoroutine);
            }
            m_GetProjectInfoCoroutine = StartCoroutine(WaitForGetSharingProjectInfo(m_ProjectInfoTask));
        }

        IEnumerator WaitForGetSharingProjectInfo (Task<UnityProject> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            if (task.IsFaulted)
            {
                // Throwing can be by design in ProjectServer, here we deescalate the exception based on InnerException details.
                // Access Denied
                var returnExceptionType = SharedLinkException.AccessDeniedException;
                if (task.Exception.InnerException is ConnectionException)
                {
                    // Connectivity Error
                    var connectionException = (ConnectionException)task.Exception.InnerException;
                    returnExceptionType = SharedLinkException.ConnectionException;
                    if (connectionException.GetBaseException() is RpcException) 
                    {
                        // Not Found
                        var rpcExcpetion = (RpcException)connectionException.GetBaseException();
                        if (rpcExcpetion.StatusCode.Equals(StatusCode.NotFound))
                        {
                            returnExceptionType = SharedLinkException.NotFoundException;
                        }
                    }
                }
                else
                {
                    Debug.LogError($"Get Project Info failed. {task.Exception}");
                }
                projectInfoExceptionEvent?.Invoke(returnExceptionType);
            }
            else
            {
                var projectInfo = task.Result;
                linkSharingProjectInfoEvent?.Invoke(projectInfo);
            }

            m_ProjectInfoTask = null;
        }
        
        public void GetSharingLinkInfo(string cloudServiceAccessToken, Project projectDataActiveProject)
        {
            if (!projectDataActiveProject.IsConnectedToServer)
                return;

            m_GetSharingLinkTask = Task.Run(() =>
                ProjectServer.Client.GetSharingLinkInfo(cloudServiceAccessToken, projectDataActiveProject.UnityProject));

            if (m_GetLinkTokenCoroutine != null)
            {
                StopCoroutine(m_GetLinkTokenCoroutine);
            }

            m_GetLinkTokenCoroutine = StartCoroutine(WaitForGetSharingLinkToken(m_GetSharingLinkTask));
        }

        public void SetSharingLinkPermission(string cloudServiceAccessToken, Project projectDataActiveProject, LinkPermission permission)
        {
            m_SetLinkPermissionTask = Task.Run(() =>
                ProjectServer.Client.UpdateSharingLinkPermission(cloudServiceAccessToken, projectDataActiveProject.UnityProject, permission));

            if (m_SetLinkPermissionCoroutine != null)
            {
                StopCoroutine(m_SetLinkPermissionCoroutine);
            }

            m_SetLinkPermissionCoroutine = StartCoroutine(WaitForSetLinkPermission(m_SetLinkPermissionTask));
        }
        
        IEnumerator WaitForGetSharingLinkToken(Task<SharingLinkInfo> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            if (task.IsFaulted)
            {
                Debug.LogError($"Get Sharing Link failed: {task.Exception}");
                linkCreatedExceptionEvent?.Invoke(task.Exception);
            }
            else
            {
                var sharingLink = task.Result;
                sharingLinkCreated?.Invoke(sharingLink);
            }

            m_GetSharingLinkTask = null;
        }

        IEnumerator WaitForSetLinkPermission(Task<SharingLinkInfo> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsFaulted)
            {
                Debug.LogError("Setting Link Permission failed");
            }
            else
            {
                var sharingLink = task.Result;
                setLinkPermissionDone?.Invoke(sharingLink);
            }

            m_SetLinkPermissionTask = null;
        }
    }
}
