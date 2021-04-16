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
            m_Manager.ProcessToken(UnityEditor.CloudProjectSettings.accessToken, false);
        }

        public void Logout()
        {
            // nothing to do
        }
    }
}
#endif