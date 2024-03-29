#if UNITY_STANDALONE_LINUX && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Unity.Reflect.Utils;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEngine.Reflect
{
    public class AuthBackend : IAuthenticatable
    {
        private LoginManager m_Manager;

        internal AuthBackend(LoginManager manager)
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