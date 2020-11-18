#if UNITY_ANDROID && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Reflect.Utils;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public class AndroidAuthBackend : IAuthenticatable
    {
        private LoginManager m_Manager;
        private readonly string k_jwtParamName = "?jwt=";

        internal AndroidAuthBackend(LoginManager manager)
        {
            m_Manager = manager;
        }

        public void Start()
        {
            m_Manager.ReadPersistentToken();
        }

        public void Update()
        {
        }

        public void Login()
        {
            Application.OpenURL(AuthConfiguration.LoginUrl);
        }

        public void Logout()
        {
            Debug.Log($"Sign out using: {AuthConfiguration.LogoutUrl}");
            m_Manager.InvalidateToken();
            Application.OpenURL(AuthConfiguration.LogoutUrl);
        }
        private void OnDeepLinkingRequested(string deepLink)
        {
            m_Manager.onDeepLink(deepLink);
        }
    }
}

#endif
