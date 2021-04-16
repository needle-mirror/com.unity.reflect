#if UNITY_IOS && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Reflect.Utils;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace UnityEngine.Reflect
{
    public class AuthBackend : IAuthenticatable, IDeepLinkable
    {
        private DeepLinkManager m_DeepLinkManager;

        private LoginManager m_Manager;

        [DllImport("__Internal")]
        static extern void LaunchSafariWebViewUrl(string url);
        [DllImport("__Internal")]
        static extern void DismissSafariWebView();

        internal AuthBackend(LoginManager manager)
        {
            m_Manager = manager;
        }

        public void Start()
        {
            m_DeepLinkManager = new DeepLinkManager(m_Manager);
            m_Manager.ReadPersistentToken();
        }

        public void Update()
        {
        }

        public void Login()
        {
            var loginUrl = AuthConfiguration.LoginUrl;
            var clientId = "industrial_reflect";
            var iosClientId = "industrial_reflect_ios";
            loginUrl = loginUrl.Replace(clientId, iosClientId);
            LaunchSafariWebViewUrl(loginUrl);
        }

        public void Logout()
        {
            Debug.Log($"Sign out using: {AuthConfiguration.LogoutUrl}");
            m_Manager.InvalidateToken();
            LaunchSafariWebViewUrl(AuthConfiguration.LogoutUrl);
        }

        public void DeepLinkComplete()
        {
            DismissSafariWebView();
        }
    }
}

#endif
