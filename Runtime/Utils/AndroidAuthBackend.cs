#if UNITY_ANDROID && !UNITY_EDITOR && PIPELINE_API
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class AndroidAuthBackend : IAuthenticatable
    {
        private LoginManager m_Manager;
        private readonly string k_LoginUrl = "https://api.unity.com/v1/oauth2/authorize?client_id=industrial_reflect&response_type=rsa_jwt&state=hello&redirect_uri=reflect://implicit/callback/login/";
        private readonly string k_LogoutUrl = "https://api.unity.com/v1/oauth2/end-session";
        private readonly string k_jwtParamName = "?jwt=";

        internal AndroidAuthBackend(LoginManager manager)
        {
            m_Manager = manager;
        }

        public void Start()
        {
            m_Manager.deepLinkingRequested.AddListener(OnDeepLinkingRequested);
            m_Manager.ReadPersistentToken();
        }

        public void Update()
        {
        }

        public void Login()
        {
            Application.OpenURL(k_LoginUrl);
        }

        public void Logout()
        {
            var url = ProjectServer.UnityUser?.LogoutUrl?.AbsoluteUri;
            if (!string.IsNullOrEmpty(url))
            {
                Debug.Log($"Silent Sign out using: {url}");
                m_Manager.InvalidateToken();
                m_Manager.SilentInvalidateTokenLogout(url);
            }
            else
            {
                Debug.Log($"Sign out using: {k_LogoutUrl}");
                m_Manager.InvalidateToken();
                Application.OpenURL(k_LogoutUrl);
            }
        }

        private void OnDeepLinkingRequested(string deepLinkingInfo)
        {
            if (UrlHelper.TryCreateUriAndValidate(deepLinkingInfo, UriKind.Absolute, out var uri))
            {
                m_Manager.ProcessToken(uri.Query.Substring(k_jwtParamName.Length));
            }
        }
    }
}

#endif
