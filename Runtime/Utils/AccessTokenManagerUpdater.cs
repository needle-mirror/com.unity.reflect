using System;
using Unity.Reflect;
using UnityEngine.Events;

namespace UnityEngine.Reflect
{
    [Serializable]
    public class AccessTokenEvent : UnityEvent<AccessToken> { }

    public class AccessTokenManagerUpdater : MonoBehaviour, IUpdateDelegate
    {
        // CreateAccessToken
        public AccessTokenEvent createAccessTokenEvent;
        public ExceptionEvent createAccessTokenExceptionEvent;

        public AccessTokenEvent createAccessTokenWithLinkTokenEvent;
        public ExceptionEvent createAccessTokenWithLinkTokenExceptionEvent;

        // RefreshAccessToken
        public AccessTokenEvent refreshAccessTokenEvent;
        public ExceptionEvent refreshAccessTokenExceptionEvent;

        // KeepAliveFloatingSeat
        public UnityEvent keepAliveFloatingSeatEvent;
        public ExceptionEvent keepAliveFloatingSeatExceptionEvent;

        // ReleaseFloatingSeat
        public UnityEvent releaseFloatingSeatEvent;
        public ExceptionEvent releaseFloatingSeatExceptionEvent;
        
        public ExceptionEvent accessTokenExceptionEvent;

        public event Action<float> update;

        void Update()
        {
            update?.Invoke(Time.unscaledDeltaTime);
        }


        public void CreateAccessToken(Project project, string userLoginToken, Action<AccessToken> callback = null)
        {
            AccessTokenManager accessTokenManager = AccessTokenManager.Create(project, this);

            AddEventListeners(accessTokenManager);

            accessTokenManager.CreateAccessToken(project, userLoginToken, callback);
        }

        public void CreateAccessTokenWithLinkToken(string linkToken, string userLoginToken,
            Action<AccessToken> callback = null)
        {
            AccessTokenManager accessTokenManager = new AccessTokenManager(this);
            AddEventListeners(accessTokenManager);
            accessTokenManager.CreateAccessTokenWithLinkToken(linkToken, userLoginToken, accessToken =>
            {
                callback?.Invoke(accessToken);
                AccessTokenManager.Add(accessTokenManager, new Project(accessToken.UnityProject));
            });
        }

        public void ReleaseAccessTokenManager(Project project, Action callback = null)
        {
            var accessTokenManager = AccessTokenManager.Get(project);
            accessTokenManager?.ReleaseFloatingSeat(() =>
            {
                AccessTokenManager.Remove(project);
                callback?.Invoke();
                RemoveListeners(accessTokenManager);
            });
        }

        void AddEventListeners(AccessTokenManager accessTokenManager)
        {

            accessTokenManager.createAccessTokenEvent += OnCreateAccessToken;
            accessTokenManager.createAccessTokenExceptionEvent += OnCreateAccessTokenException;

            accessTokenManager.createAccessTokenWithLinkTokenEvent += OnCreateAccessTokenWithLinkToken;
            accessTokenManager.createAccessTokenWithLinkTokenExceptionEvent += OnCreateAccessTokenWithLinkTokenException;

            accessTokenManager.refreshAccessTokenEvent += OnRefreshAccessToken;
            accessTokenManager.refreshAccessTokenExceptionEvent += OnRefreshAccessTokenException;

            accessTokenManager.keepAliveFloatingSeatEvent += OnKeepAliveFloatingSeat;
            accessTokenManager.keepAliveFloatingSeatExceptionEvent += OnKeepAliveFloatingSeatException;

            accessTokenManager.releaseFloatingSeatEvent += OnReleaseFloatingSeat;
            accessTokenManager.releaseFloatingSeatExceptionEvent += OnReleaseFloatingSeatException;
            
            accessTokenManager.accessTokenManagerExceptionEvent += OnAccessTokenException;

        }

        void RemoveListeners(AccessTokenManager accessTokenManager)
        {
            accessTokenManager.createAccessTokenEvent -= OnCreateAccessToken;
            accessTokenManager.createAccessTokenExceptionEvent -= OnCreateAccessTokenException;

            accessTokenManager.createAccessTokenWithLinkTokenEvent -= OnCreateAccessTokenWithLinkToken;
            accessTokenManager.createAccessTokenWithLinkTokenExceptionEvent -= OnCreateAccessTokenWithLinkTokenException;

            accessTokenManager.refreshAccessTokenEvent -= OnRefreshAccessToken;
            accessTokenManager.refreshAccessTokenExceptionEvent -= OnRefreshAccessTokenException;

            accessTokenManager.keepAliveFloatingSeatEvent -= OnKeepAliveFloatingSeat;
            accessTokenManager.keepAliveFloatingSeatExceptionEvent -= OnKeepAliveFloatingSeatException;

            accessTokenManager.releaseFloatingSeatEvent -= OnReleaseFloatingSeat;
            accessTokenManager.releaseFloatingSeatExceptionEvent -= OnReleaseFloatingSeatException;
            
            accessTokenManager.accessTokenManagerExceptionEvent -= OnAccessTokenException;
        }

        void OnCreateAccessToken(AccessToken accessToken)
        {
            createAccessTokenEvent?.Invoke(accessToken);
        }

        void OnCreateAccessTokenException(Exception exception)
        {
            createAccessTokenExceptionEvent?.Invoke(exception);
        }

        void OnCreateAccessTokenWithLinkToken(AccessToken accessToken)
        {
            createAccessTokenWithLinkTokenEvent?.Invoke(accessToken);
        }

        void OnCreateAccessTokenWithLinkTokenException(Exception exception)
        {
            createAccessTokenWithLinkTokenExceptionEvent?.Invoke(exception);
        }

        void OnRefreshAccessToken(AccessToken accessToken)
        {
            refreshAccessTokenEvent?.Invoke(accessToken);
        }

        void OnRefreshAccessTokenException(Exception exception)
        {
            refreshAccessTokenExceptionEvent?.Invoke(exception);
        }

        void OnKeepAliveFloatingSeat()
        {
            keepAliveFloatingSeatEvent?.Invoke();
        }

        void OnKeepAliveFloatingSeatException(Exception exception)
        {
            keepAliveFloatingSeatExceptionEvent?.Invoke(exception);
        }

        void OnReleaseFloatingSeat()
        {
            releaseFloatingSeatEvent?.Invoke();
        }

        void OnReleaseFloatingSeatException(Exception exception)
        {
            releaseFloatingSeatExceptionEvent?.Invoke(exception);
        }
        
        void OnAccessTokenException(Exception exception)
        {
            accessTokenExceptionEvent?.Invoke(exception);
        }
    }
}