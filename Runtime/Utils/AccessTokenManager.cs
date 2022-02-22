using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Reflect;


namespace UnityEngine.Reflect
{
    
    public class AccessTokenManager : IDisposable
    {
        AccessToken m_AccessToken;
        
        // CreateAccessToken
        Task<AccessToken> m_CreateAccessTokenTask;
        Task<AccessToken> m_CreateAccessTokenWithLinkTask;

        Action<AccessToken> m_CreateAccessTokenCallback;
        Action<AccessToken> m_CreateAccessTokenWithLinkCallback;

        public event Action<AccessToken> createAccessTokenEvent;
        public event Action<Exception> createAccessTokenExceptionEvent;
        
        public event Action<AccessToken> createAccessTokenWithLinkTokenEvent;
        public event Action<Exception> createAccessTokenWithLinkTokenExceptionEvent;


        // RefreshAccessToken
        Task<AccessToken> m_RefreshAccessTokenTask;
        public event Action<AccessToken> refreshAccessTokenEvent;
        public event Action<Exception> refreshAccessTokenExceptionEvent;
        
        
        // KeepAliveFloatingSeat 
        Task m_KeepAliveFloatingSeatTask;
        public event Action keepAliveFloatingSeatEvent;
        public event Action<Exception> keepAliveFloatingSeatExceptionEvent;
        
        
        // ReleaseFloatingSeat
        Task m_ReleaseFloatingSeatTask;
        Action m_ReleaseFloatingSeatCallback;

        public event Action releaseFloatingSeatEvent;
        public event Action<Exception> releaseFloatingSeatExceptionEvent;

        // General exception
        public event Action<Exception> accessTokenManagerExceptionEvent;

        float m_RefreshTimer;
        float m_KeepAliveTimer;

        IUpdateDelegate m_UpdateDelegate;
        bool m_Disposed = false;

        static Dictionary<string, AccessTokenManager> m_Cache = new Dictionary<string, AccessTokenManager>();

        
        public static AccessTokenManager Create(Project project, IUpdateDelegate updateDelegate)
        {
            AccessTokenManager accessTokenManager = new AccessTokenManager(updateDelegate);
            Add(accessTokenManager, project);
            return accessTokenManager;
        }

        internal static void Add(AccessTokenManager accessTokenManager, Project project)
        {
            if (!m_Cache.ContainsKey(project.serverProjectId))
            {
                m_Cache.Add(project.serverProjectId, accessTokenManager);
            }
            else
            {
                m_Cache[project.serverProjectId]?.Dispose();
                m_Cache[project.serverProjectId] = accessTokenManager;
            }
        }

        internal static AccessTokenManager Get(Project project)
        {
            m_Cache.TryGetValue(project.serverProjectId, out var accessToken);
            return accessToken;
        }

        public static void Remove(Project project)
        {
            var accessTokenManager = Get(project);
            accessTokenManager?.Dispose();
            if (m_Cache.ContainsKey(project.serverProjectId))
            {
                m_Cache.Remove(project.serverProjectId);
            }
        }

        internal AccessTokenManager(IUpdateDelegate updateDelegate)
        {
            m_UpdateDelegate = updateDelegate;
            m_UpdateDelegate.update += OnUpdate;
        }

        public void CreateAccessToken(Project project, string userLoginToken, Action<AccessToken> callback)
        {
            m_CreateAccessTokenTask = Task.Run(() => ProjectServer.Client.CreateAccessToken(project.UnityProject, userLoginToken));
            m_CreateAccessTokenCallback = callback;
        }
        
        internal void CreateAccessTokenWithLinkToken(string linkToken, string userLoginToken, Action<AccessToken> callback)
        {
            m_CreateAccessTokenWithLinkTask = Task.Run(() => ProjectServer.Client.CreateAccessToken(linkToken, userLoginToken));
            m_CreateAccessTokenWithLinkCallback = callback;
        }
        
        void RefreshAccessToken(string refreshToken)
        {
            m_RefreshAccessTokenTask = Task.Run(() => ProjectServer.Client.RefreshAccessToken(refreshToken));
        }
        
        void KeepAliveFloatingSeat(string cloudServicesAccessToken)
        {
            m_KeepAliveFloatingSeatTask = Task.Run(() => ProjectServer.Client.KeepAliveFloatingSeat(cloudServicesAccessToken));
        }

        internal void ReleaseFloatingSeat(Action callback)
        {
            if (m_AccessToken.FloatingSeatDuration == TimeSpan.Zero)
            {
                //if it's not floating seat
                callback?.Invoke();
            }
            else
            {
                m_ReleaseFloatingSeatTask = Task.Run(() => ProjectServer.Client.ReleaseFloatingSeat(m_AccessToken.CloudServicesAccessToken));
                m_ReleaseFloatingSeatCallback = callback;
            }
        }

        void OnUpdate(float unscaledDeltaTime)
        {
            try
            {
                if (m_CreateAccessTokenTask != null && m_CreateAccessTokenTask.IsCompleted)
                {
                    if (m_CreateAccessTokenTask.IsFaulted)
                    {
                        Debug.LogError($"Create Access Token failed: {m_CreateAccessTokenTask.Exception}");
                        var innerException = m_CreateAccessTokenTask.Exception?.InnerException ?? m_CreateAccessTokenTask.Exception;
                        createAccessTokenExceptionEvent?.Invoke(innerException);
                        Dispose();
                    }
                    else
                    {
                        // replace AccessToken only when we createAccessToken 
                        m_AccessToken = m_CreateAccessTokenTask.Result;
                        createAccessTokenEvent?.Invoke(m_AccessToken);
                        m_CreateAccessTokenCallback?.Invoke(m_AccessToken);
                        m_CreateAccessTokenCallback = null;
                    }

                    m_CreateAccessTokenTask = null;
                }

                if (m_CreateAccessTokenWithLinkTask != null && m_CreateAccessTokenWithLinkTask.IsCompleted)
                {
                    if (m_CreateAccessTokenWithLinkTask.IsFaulted)
                    {
                        Debug.LogError(
                            $"CreateAccessTokenWithLinkToken failed: {m_CreateAccessTokenWithLinkTask.Exception}");
                        var innerException = m_CreateAccessTokenWithLinkTask.Exception?.InnerException ?? m_CreateAccessTokenWithLinkTask.Exception;
                        createAccessTokenWithLinkTokenExceptionEvent?.Invoke(innerException);
                        Dispose();
                    }
                    else
                    {
                        // replace AccessToken only when we createAccessToken 
                        m_AccessToken = m_CreateAccessTokenWithLinkTask.Result;
                        createAccessTokenWithLinkTokenEvent?.Invoke(m_AccessToken);
                        m_CreateAccessTokenWithLinkCallback?.Invoke(m_AccessToken);
                        m_CreateAccessTokenWithLinkCallback = null;
                    }

                    m_CreateAccessTokenWithLinkTask = null;
                }

                if (m_RefreshAccessTokenTask != null && m_RefreshAccessTokenTask.IsCompleted)
                {
                    if (m_RefreshAccessTokenTask.IsFaulted)
                    {
                        Debug.LogError($"Refresh Access Token failed: {m_RefreshAccessTokenTask.Exception}");
                        var innerException = m_RefreshAccessTokenTask.Exception?.InnerException ?? m_RefreshAccessTokenTask.Exception;
                        refreshAccessTokenExceptionEvent?.Invoke(innerException);
                        
                        Dispose();
                    }
                    else
                    {
                        var accessToken = m_RefreshAccessTokenTask.Result;

                        // update AccessToken values 
                        m_AccessToken.UpdateAccessToken(accessToken);
                        refreshAccessTokenEvent?.Invoke(m_AccessToken);
                    }

                    m_RefreshAccessTokenTask = null;
                }

                if (m_KeepAliveFloatingSeatTask != null && m_KeepAliveFloatingSeatTask.IsCompleted)
                {
                    if (m_KeepAliveFloatingSeatTask.IsFaulted)
                    {
                        Debug.LogError($"KeepAliveFloatingSeat failed: {m_KeepAliveFloatingSeatTask.Exception}");
                        var innerException = m_KeepAliveFloatingSeatTask.Exception?.InnerException ?? m_KeepAliveFloatingSeatTask.Exception;
                        keepAliveFloatingSeatExceptionEvent?.Invoke(innerException);
                    }
                    else
                    {
                        keepAliveFloatingSeatEvent?.Invoke();
                    }

                    m_KeepAliveFloatingSeatTask = null;
                }

                if (m_ReleaseFloatingSeatTask != null && m_ReleaseFloatingSeatTask.IsCompleted)
                {
                    if (m_ReleaseFloatingSeatTask.IsFaulted)
                    {


                        Debug.LogError($"ReleaseFloatingSeat failed: {m_ReleaseFloatingSeatTask.Exception}");
                        var innerException = m_ReleaseFloatingSeatTask.Exception?.InnerException ?? m_ReleaseFloatingSeatTask.Exception;
                        releaseFloatingSeatExceptionEvent?.Invoke(innerException);
                    }
                    else
                    {
                        releaseFloatingSeatEvent?.Invoke();
                        m_ReleaseFloatingSeatCallback?.Invoke();
                        m_ReleaseFloatingSeatCallback = null;
                    }

                    m_ReleaseFloatingSeatTask = null;
                }

                if (m_AccessToken != null)
                {
                    if (m_AccessToken.AccessTokenExpiry != null && m_AccessToken.AccessTokenExpiry != TimeSpan.Zero)
                    {
                        m_RefreshTimer += unscaledDeltaTime;
                        if (m_RefreshTimer >= m_AccessToken.AccessTokenExpiry.TotalSeconds)
                        {
                            RefreshAccessToken(m_AccessToken.RefreshToken);
                            m_RefreshTimer = 0;
                        }
                    }

                    if (m_AccessToken.FloatingSeatDuration != null &&
                        m_AccessToken.FloatingSeatDuration != TimeSpan.Zero)
                    {
                        m_KeepAliveTimer += unscaledDeltaTime;
                        if (m_KeepAliveTimer >= m_AccessToken.FloatingSeatDuration.TotalSeconds)
                        {
                            KeepAliveFloatingSeat(m_AccessToken.CloudServicesAccessToken);
                            m_KeepAliveTimer = 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                accessTokenManagerExceptionEvent?.Invoke(ex);
                Dispose(); 
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!m_Disposed)
            {
                if (disposing)
                {
                    if (m_Cache.ContainsValue(this))
                    {
                        var key = m_Cache.FirstOrDefault(e => e.Value == this).Key;
                        m_Cache.Remove(key);
                    }

                    m_UpdateDelegate.update -= OnUpdate;
                    m_AccessToken = null;
                    m_CreateAccessTokenTask = null;
                    m_CreateAccessTokenWithLinkTask = null;
                    m_CreateAccessTokenCallback = null;
                    m_CreateAccessTokenWithLinkCallback = null;
                    m_RefreshAccessTokenTask = null;
                    m_KeepAliveFloatingSeatTask = null;
                    m_ReleaseFloatingSeatTask = null;
                    m_ReleaseFloatingSeatCallback = null;
                }

                m_Disposed = true;
            }
        }
    }
}