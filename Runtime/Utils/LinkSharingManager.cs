using System;
using System.Collections;
using System.Threading.Tasks;
using Unity.Reflect;
using UnityEngine.Events;

namespace UnityEngine.Reflect
{
    [Serializable]
    public class SharingLinkEvent : UnityEvent<SharingLinkInfo> { }

    [Serializable]
    public class ProjectInfoEvent : UnityEvent<UnityProject> { }
    
    [Serializable]
    public class ExceptionEvent : UnityEvent<Exception> {}
    
    public class LinkSharingManager : MonoBehaviour
    {
        public SharingLinkEvent sharingLinkCreated;
        public SharingLinkEvent setLinkPermissionDone;
        public ProjectInfoEvent linkSharingProjectInfoEvent;
        public ExceptionEvent linkCreatedExceptionEvent;
        public ExceptionEvent projectInfoExceptionEvent;
        
        Task<UnityProject> m_ProjectInfoTask;
        Task<SharingLinkInfo> m_GetSharingLinkTask;
        Task<SharingLinkInfo> m_SetLinkPermissionTask;
        
        Coroutine m_GetProjectInfoCoroutine;
        Coroutine m_GetLinkTokenCoroutine;
        Coroutine m_SetLinkPermissionCoroutine;

        public void ProcessSharingToken(string accessToken, string linkToken)
        {
            m_ProjectInfoTask = Task.Run(() => ProjectServer.Client.GetSharingProjectInfo(accessToken, linkToken));

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
                Debug.LogError($"Get Project Info failed: {task.Exception}");
                projectInfoExceptionEvent?.Invoke(task.Exception);
            }
            else
            {
                var projectInfo = task.Result;
                linkSharingProjectInfoEvent?.Invoke(projectInfo);
            }

            m_ProjectInfoTask = null;
        }
        
        public void GetSharingLinkInfo(string accessToken, Project projectDataActiveProject)
        {
            m_GetSharingLinkTask = Task.Run(() =>
                ProjectServer.Client.GetSharingLinkInfo(accessToken, projectDataActiveProject.UnityProject));

            if (m_GetLinkTokenCoroutine != null)
            {
                StopCoroutine(m_GetLinkTokenCoroutine);
            }

            m_GetLinkTokenCoroutine = StartCoroutine(WaitForGetSharingLinkToken(m_GetSharingLinkTask));
        }

        public void SetSharingLinkPermission(string accessToken, Project projectDataActiveProject, LinkPermission permission)
        {
            m_SetLinkPermissionTask = Task.Run(() =>
                ProjectServer.Client.UpdateSharingLinkPermission(accessToken, projectDataActiveProject.UnityProject, permission));

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