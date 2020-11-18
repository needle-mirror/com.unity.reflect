#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEngine.Reflect
{
    internal class OSXStandaloneAuthBackend : IAuthenticatable
    {
        [DllImport("OSXReflectViewerPlugin")]
        static extern void DeepLink_Reset();

        [DllImport("OSXReflectViewerPlugin")]
        static extern string DeepLink_GetURL();

        [DllImport("OSXReflectViewerPlugin")]
        static extern string DeepLink_GetProcessId();

        private LoginManager m_Manager;

        internal OSXStandaloneAuthBackend(LoginManager manager)
        {
            m_Manager = manager;
        }

        public void Start()
        {
            m_Manager.ReadPersistentToken();
        }

        public void Login()
        {
            var loginUrl = $"{AuthConfiguration.LoginUrl}{DeepLink_GetProcessId()}"; 
            Debug.Log($"Sign in using: {loginUrl}");
            Application.OpenURL(loginUrl);
        }

        public void Logout()
        {
            Debug.Log($"Sign out using: {AuthConfiguration.LogoutUrl}");
            m_Manager.InvalidateToken();
            Application.OpenURL(AuthConfiguration.LogoutUrl);
        }
        public void Update()
        {
            if (DeepLink_GetURL() == "") return;
            onDeepLink(DeepLink_GetURL());
            DeepLink_Reset();
        }

        void onDeepLink(string deepLink)
        {
            if (UrlHelper.TryCreateUriAndValidate(deepLink, out var uri))
            {
                m_Manager.ProcessToken(uri.Query.Substring(AuthConfiguration.JwtParamName.Length));
            }
        }
    }
}
#endif

