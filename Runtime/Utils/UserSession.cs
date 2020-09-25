#if PIPELINE_API
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Unity.Reflect;
using UnityEngine;
using UnityEngine.Reflect;

[Serializable]
public class UserSession
{
    public UnityUser m_User;

    private string m_SessionToken;
    private LoginManager m_Manager;
    private Task<UnityUser> m_Task;

    public UserSession(LoginManager manager)
    {
        m_Manager = manager;
        m_Manager.tokenUpdated?.AddListener(OnTokenUpdated);
    }

    private void OnTokenUpdated(string newToken)
    {
        m_SessionToken = newToken;
        if (!string.IsNullOrEmpty(m_SessionToken))
        {
            if (m_Task != null)
            {
                m_Manager.StopCoroutine("WaitForSessionCompletion");
            }
            m_Task = Task.Run(() => ProjectServer.Client.GetUserInfo(newToken));
            m_Manager.StartGetUserInfo(m_Task);
        }
        else
        {
            m_Manager.StartSendLogoutEvent();
        }
    }

    internal void OnCompletedTask(Task<UnityUser> task)
    {
        if (task.IsFaulted)
        {
            Debug.LogError($"Get User Info failed: {task.Exception}");
            var reason = task.Exception.InnerExceptions.OfType<RpcException>().FirstOrDefault();
            if (reason.StatusCode.Equals(StatusCode.Unauthenticated) || reason.StatusCode.Equals(StatusCode.PermissionDenied))
            {
                m_Manager.authenticationFailed?.Invoke(reason.Message);
            }
        }
        else
        {
            m_User = task.Result;
            m_Manager.userLoggedIn?.Invoke(m_User);
        }
        m_Task = null;
    }
}
#endif

