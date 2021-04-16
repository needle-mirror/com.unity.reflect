#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEngine.Reflect
{
    public class AuthBackend : IAuthenticatable
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string windowName);

        private LoginManager m_Manager;
        
        IntPtr m_HWnd;

        internal AuthBackend(LoginManager loginManager)
        {
            m_Manager = loginManager;
            Application.focusChanged += OnApplicationFocus;
        }

        void SetLoginUrlWithWindowPtr()
        {
            m_HWnd = GetWindowHandle();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
            {
                SetLoginUrlWithWindowPtr();
            }
        }

        public void Start()
        {
            m_Manager.ReadPersistentToken();

            string[] args = Environment.GetCommandLineArgs();
            if (args.Length > 0 && File.Exists(args[0])) 
            {
                // First argument is always the absolute path to the viewer .exe
                RegisterViewerPath(args[0]);
            }

            SetLoginUrlWithWindowPtr();
        }

        public void Update()
        {
            // nothing to do
        }

        public void Login()
        {
            AddRegistryKeys();
            var loginUrl = $"{AuthConfiguration.LoginUrl}{m_HWnd}"; 
            Debug.Log($"Sign in using: {loginUrl}");
            Application.OpenURL(loginUrl);
        }

        public void Logout()
        {   
            Debug.Log($"Sign out using: {AuthConfiguration.LogoutUrl}");
            m_Manager.InvalidateToken();
            // This will deliberately unfocus the viewer application
            Application.OpenURL(AuthConfiguration.LogoutUrl);
        }

        // TODO move to other class to remove WindowsStandaloneInterop reference and reset this class as internal
        public static IntPtr GetWindowHandle()
        {
            // "UnityWndClass" is the string value returned when invoking user32.dll GetClassName function
            IntPtr hWnd = FindWindow("UnityWndClass", Application.productName);
            if (hWnd != IntPtr.Zero)
            {
                return hWnd;
            }
            hWnd = GetActiveWindow();
            return hWnd;
        }

        string GetResolverPath() 
        {
            var appDomainLocation = typeof(AppDomain).Assembly.Location;
            var subPath = appDomainLocation.Substring(0, appDomainLocation.LastIndexOf("_Data\\Managed\\"));
            var lastFolderIndex = subPath.LastIndexOf("\\");
            var exePathRoot = subPath.Substring(0, lastFolderIndex + 1);
            var exeAppName = subPath.Substring(lastFolderIndex + 1);
            return $"{exePathRoot}Unity_Reflect_Interop\\{exeAppName}.exe";
        }

        void AddRegistryKeys()
        {
            var resolverLocation = GetResolverPath();
            // Use CurrentUser, since LocalMachine will fail on some browser
            using (var reflectKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + AuthConfiguration.UriScheme))
            {
                reflectKey.SetValue("", "URL:" + AuthConfiguration.UriScheme);
                reflectKey.SetValue(AuthConfiguration.RegistryUrlProtocol, "");
                using (var commandKey = reflectKey.CreateSubKey(AuthConfiguration.RegistryShellKey))
                {
                    commandKey.SetValue("", "\"" + resolverLocation + "\" \"%1\"");
                }
            }
        }

        // Register current viewer .exe path for reflect deeplink resolver
        void RegisterViewerPath(string exePath)
        {
            using (var reflectKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("SOFTWARE\\Unity Reflect\\Apps\\" + Application.productName))
            {
                reflectKey.SetValue("StartPath", exePath);
            }
        }
    }
}
#endif
