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
    public class WindowsStandaloneAuthBackend : IAuthenticatable
    {
        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(string className, string windowName);

        private LoginManager m_Manager;
        
        private string m_ReflectLoginUrl;

        internal WindowsStandaloneAuthBackend(LoginManager loginManager)
        {
            m_Manager = loginManager;
            Application.focusChanged += OnApplicationFocus;
        }

        void SetLoginUrlWithWindowPtr()
        {
            IntPtr hWnd = GetWindowHandle();
            m_ReflectLoginUrl = $"{AuthConfiguration.LoginUrl}{hWnd}";
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

            SetLoginUrlWithWindowPtr();

            // Unity usual start command have the path to application as single argument
            // Some Build will mishandle spaces and artificially creates more than 1 argument
            var appPathFound = false;
            var appPath = string.Empty;
            var trailingArg = string.Empty;
            for (var i=0;i<args.Length;i++)
            {
                if (!appPathFound) {
                     if (i > 0)
                     {
                         appPath += " ";
                     }
                     appPath += args[i];
                     appPathFound = File.Exists(appPath);
                     Debug.Log($"appPathFound {appPathFound} : {appPath}");
                 }
                 else
                 {
                     if (!string.IsNullOrEmpty(trailingArg))
                     {
                         trailingArg += " ";
                     }
                     trailingArg += args[i];
                     Debug.Log($"trailingArg {trailingArg}");
                 }
            }

            if (!string.IsNullOrEmpty(trailingArg))
            {
                // DeepLink processing
                if (UrlHelper.TryParseDeepLink(trailingArg, out var token, out var deepLinkRoute, out var deepLinkArgs))
                {   
                    m_Manager.ProcessDeepLink(token, deepLinkRoute, deepLinkArgs);
                }
            }
        }

        public void Update()
        {
            // nothing to do
        }

        public void Login()
        {
            AddRegistryKeys();
            Debug.Log($"Sign in using: {m_ReflectLoginUrl}");
            Application.OpenURL(m_ReflectLoginUrl);
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

        void AddRegistryKeys()
        {
            var appDomainLocation = typeof(AppDomain).Assembly.Location;
            var subPath = appDomainLocation.Substring(0, appDomainLocation.LastIndexOf("_Data\\Managed\\"));
            var lastFolderIndex = subPath.LastIndexOf("\\");
            var exePathRoot = subPath.Substring(0, lastFolderIndex + 1);
            var exeAppName = subPath.Substring(lastFolderIndex + 1);
            var applicationLocation =  $"{exePathRoot}Unity_Reflect_Interop\\{exeAppName}";

            // Use CurrentUser, since LocalMachine will fail on some browser
            using (var reflectKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + AuthConfiguration.UriScheme))
            {
                reflectKey.SetValue("", "URL:" + AuthConfiguration.UriScheme);
                reflectKey.SetValue(AuthConfiguration.RegistryUrlProtocol, "");
                using (var commandKey = reflectKey.CreateSubKey(AuthConfiguration.RegistryShellKey))
                {
                    commandKey.SetValue("", "\"" + applicationLocation + "\" \"%1\"");
                }
            }
        }
    }
}
#endif
