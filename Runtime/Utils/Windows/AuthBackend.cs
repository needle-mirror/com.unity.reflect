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

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        static readonly IntPtr HWND_TOP = new IntPtr(0);
        const UInt32 SWP_NOSIZE = 0x0001;
        const UInt32 SWP_NOMOVE = 0x0002;
        const UInt32 SWP_SHOWWINDOW = 0x0040;
        
        bool TopMostSate = false;
        
        private LoginManager m_Manager;
        
        IntPtr m_HWnd;

        internal AuthBackend(LoginManager loginManager)
        {
            m_Manager = loginManager;
            Application.focusChanged += OnApplicationFocus;
            m_HWnd = GetWindowHandle();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (TopMostSate)
            {
                // Remove temporary topmost state
                SetWindowPos(m_HWnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                TopMostSate = false;
            }
        }

        public void Start()
        {
            m_Manager.ReadPersistentToken();
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
            
            // Make temporary topmost window so logout navigatio occurs behind viewer
            SetWindowPos(m_HWnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            TopMostSate = true;
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
            var appDomainLocation = Application.dataPath.Replace("/", "\\");
            var subPath = appDomainLocation.Substring(0, appDomainLocation.LastIndexOf("_Data"));
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
    }
}
#endif
