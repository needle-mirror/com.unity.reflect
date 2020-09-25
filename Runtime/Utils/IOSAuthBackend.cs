#if UNITY_IOS //&& !UNITY_EDITOR && PIPELINE_API
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class IOSAuthBackend : IAuthenticatable
    {
        [DllImport("__Internal")]
        private static extern void UnityDeeplinks_init(string gameObject = null, string deeplinkMethod = null);
        [DllImport("__Internal")]
        static extern void LaunchSafariWebViewUrl(string url);
        [DllImport("__Internal")]
        static extern void DismissSafariWebView();

        private LoginManager m_Manager;
        private readonly string k_LoginUrl = "https://api.unity.com/v1/oauth2/authorize?client_id=industrial_reflect&response_type=rsa_jwt&state=hello&redirect_uri=reflect://implicit/callback/login/";
        private readonly string k_LogoutUrl = "https://api.unity.com/v1/oauth2/end-session";
        private readonly string k_jwtParamName = "?jwt=";

        internal IOSAuthBackend(LoginManager manager)
        {
            m_Manager = manager;
        }

        public void Start()
        {
            UnityDeeplinks_init(m_Manager.gameObject.name);
            m_Manager.deepLinkingRequested.AddListener(OnDeepLinkingRequested);
            m_Manager.ReadPersistentToken();
        }

        public void Update()
        {
        }

        public void Login()
        {
            LaunchSafariWebViewUrl(k_LoginUrl);
        }

        public void Logout()
        {
            var url = ProjectServer.UnityUser?.LogoutUrl?.AbsoluteUri;
            if (!string.IsNullOrEmpty(url))
            {
                Debug.Log($"Silent Sign out using: {url}");
                m_Manager.InvalidateToken();
                LaunchSafariWebViewUrl(url);
            }
            else
            {
                Debug.Log($"Sign out using: {k_LogoutUrl}");
                m_Manager.InvalidateToken();
                LaunchSafariWebViewUrl(k_LogoutUrl);
            }
        }

        private void OnDeepLinkingRequested(string deepLinkingInfo)
        {
            if (UrlHelper.TryCreateUriAndValidate(deepLinkingInfo, UriKind.Absolute, out var uri))
            {
                m_Manager.ProcessToken(uri.Query.Substring(k_jwtParamName.Length));
            }
            DismissSafariWebView();
        }
    }
}

#endif
