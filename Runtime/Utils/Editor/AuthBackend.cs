#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.Reflect;
using Debug = UnityEngine.Debug;

namespace UnityEngine.Reflect
{
    internal class AuthBackend : IAuthenticatable
    {
        private LoginManager m_Manager;

        internal AuthBackend(LoginManager manager)
        {
            m_Manager = manager;
        }

        public void Start()
        {
            m_Manager.Login();
        }

        public void Update()
        {
            // nothing to do
        }

        public void Login()
        {
            if (string.IsNullOrEmpty(UnityEditor.CloudProjectSettings.accessToken))
            {
                Debug.LogWarning("Missing Unity User Access Token. Please restart the Unity Hub and sign in. "
                    + "If you are already signed in, relaunch the Unity Hub to enable Authentication Service.");
            }
            else
            {
                m_Manager.ProcessToken(UnityEditor.CloudProjectSettings.accessToken, false);
            }
        }

        public void Logout()
        {
            // nothing to do
        }
    }
}
#endif