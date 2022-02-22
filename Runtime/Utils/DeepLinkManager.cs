using System;
using UnityEngine;

namespace UnityEngine.Reflect
{
    internal class DeepLinkManager
    {
        public static DeepLinkManager Instance { get; private set; }
        private LoginManager m_Manager;
        public DeepLinkManager(LoginManager loginManager)
        {
            if (Instance == null)
            {
                Instance = this;
                m_Manager = loginManager;
                Application.deepLinkActivated += OnDeepLinkingRequested;
                if (!string.IsNullOrEmpty(Application.absoluteURL))
                {
                    Debug.Log($"Reflect started from deeplink: '{Application.absoluteURL}'");
                    m_Manager.onDeepLink(Application.absoluteURL, true);
                }
            }
        }
        private void OnDeepLinkingRequested(string deepLink)
        {
            Debug.Log($"OS Level deep link requested: '{Application.absoluteURL}'");
            m_Manager.onDeepLink(deepLink);
        }
    }
}