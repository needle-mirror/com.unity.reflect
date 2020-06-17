#if UNITY_STANDALONE_WIN && !UNITY_EDITOR && PIPELINE_API
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Reflect;

namespace UnityEngine.Reflect
{
    internal class WindowsStandaloneAuthBackend : IAuthenticatable
    {
        [DllImport("user32.dll")]
        static extern int SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        static extern int ShowWindow(IntPtr hwnd, ShowWindowEnum winEnum);

        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll")]
        static extern IntPtr GetActiveWindow();

        [DllImport("User32.dll")]
        static extern bool IsIconic(IntPtr handle);

        bool IsMainViewer = false;
        bool IsTopMost = true;
        string ViewProjectId = string.Empty;
        private LoginManager m_Manager;
        
        private readonly string k_LoginUrl = "https://api.unity.com/v1/oauth2/authorize?client_id=industrial_reflect&response_type=rsa_jwt&state=hello&redirect_uri=reflect://implicit/callback/login/";
        private readonly string k_LogoutUrl = "https://api.unity.com/v1/oauth2/end-session";
        private readonly string k_RegistryUrlProtocol = "URL Protocol";
        private readonly string k_RegistryShellKey = @"shell\open\command";
        private readonly string k_UriScheme = "reflect";
        private readonly string k_jwtParamName = "?jwt=";
        private string m_ReflectLoginUrl;
        const string k_JwtTokenFileName = "jwttoken.data";

        internal WindowsStandaloneAuthBackend(LoginManager loginManager)
        {
            m_Manager = loginManager;
            Application.focusChanged += OnApplicationFocus;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (IsMainViewer)
            {

                var viewerTokenFilePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}/Unity/Reflect/" +
                    $"{Application.productName}/{k_JwtTokenFileName}";
                if (File.Exists(viewerTokenFilePath))
                {
                    if (hasFocus)
                    {
                        var nextToken = File.ReadAllText(viewerTokenFilePath);
                        if (!nextToken.Equals(m_Manager.credentials.jwtToken))
                        {
                            m_Manager.ProcessToken(nextToken);
                        }
                    }
                    // In all cases, if file exists, delete it.
                    File.Delete(viewerTokenFilePath);
                }
                // When logging out, we make this app topMost, to avoid loosing focus,
                // But as soon as user interact with OS, we release this state
                if (IsTopMost)
                {
                    var wHandle = GetWindowHandle();
                    SetWindowPos(wHandle, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                    IsTopMost = false;
                }
            }
        }

        public void Start()
        {
            m_Manager.ReadPersistentToken();

            AddRegistryKeys();
            string[] args = Environment.GetCommandLineArgs();
            var myHandle = GetWindowHandle();

            m_ReflectLoginUrl = $"{k_LoginUrl}{myHandle}";
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

            if (string.IsNullOrEmpty(trailingArg))
            {
                 IsMainViewer = true;
            }
            else
            {
                 // Request from dashboard to open specific project in app
                 // OPEN_PROJECT_REQUEST ?jwt=token projectId
                 if (trailingArg.StartsWith("OPEN_PROJECT_REQUEST")) 
                 {
                     var splitRequest = trailingArg.Split(' ');
                     // If at least idToken and projectId is provided 
                     if (splitRequest.Length > 2) {
                         ViewProjectId = splitRequest[2];
                         if (UrlHelper.TryCreateUriAndValidate(splitRequest[1], UriKind.Absolute, out var uri))
                         {
                             ReadUrlCallback(uri);
                         }
                     }
                 }
                 else 
                 {
                     if (UrlHelper.TryCreateUriAndValidate(trailingArg, UriKind.Absolute, out var uri))
                     {
                         ReadUrlCallback(uri);
                     }
                 }
            }
        }

        public void Update()
        {
            // nothing to do
        }

        public void Login()
        {
            Application.OpenURL(m_ReflectLoginUrl);
        }

        public void Logout()
        {
            var url = ProjectServerEnvironment.UnityUser?.LogoutUrl?.AbsoluteUri;
            if (!string.IsNullOrEmpty(url))
            {
                Debug.Log($"Sign out using: {url}");
                m_Manager.InvalidateToken();
                var wHandle = GetWindowHandle();
                SetWindowPos(wHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                IsTopMost = true;
                Application.OpenURL(url);
            }
            else
            {
                Debug.Log($"Sign out using: {k_LogoutUrl}");
                m_Manager.InvalidateToken();
                Application.OpenURL(k_LogoutUrl);
            }
        }

        static IntPtr GetWindowHandle()
        {
            return GetActiveWindow();
        }

        enum ShowWindowEnum
        {
            Hide = 0,
            ShowNormal = 1, ShowMinimized = 2, ShowMaximized = 3,
            Maximize = 3, ShowNormalNoActivate = 4, Show = 5,
            Minimize = 6, ShowMinNoActivate = 7, ShowNoActivate = 8,
            Restore = 9, ShowDefault = 10, ForceMinimized = 11
        };

        const UInt32 SWP_NOSIZE = 0x0001;
        const UInt32 SWP_NOMOVE = 0x0002;
        const UInt32 SWP_SHOWWINDOW = 0x0040;
        static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
        static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);

        void AddRegistryKeys()
        {
            // Early exit if entry already exists and has the current application path
            using (Microsoft.Win32.RegistryKey reflectKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Classes\\" + k_UriScheme, false))
            {
                var appDomainLocation = typeof(AppDomain).Assembly.Location;
                var managedSubFolder = $"{Application.productName}_Data\\Managed\\";
                var applicationLocation = appDomainLocation.Substring(0, appDomainLocation.LastIndexOf(managedSubFolder)) + $"{Application.productName}.exe";

                if (reflectKey != null)
                {
                    var shellKey = reflectKey.OpenSubKey(k_RegistryShellKey);
                    var shellKeyApplicationPath = shellKey.GetValue("") as string;
                    if (shellKeyApplicationPath.Contains(applicationLocation))
                    {
                        return;
                    }
                }
                RegisterUriScheme(applicationLocation);
            }
        }
        
        public void RegisterUriScheme(string applicationLocation)
        {
            // Use CurrentUser, since LocalMachine will fail on some browser
            using (var reflectKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey("SOFTWARE\\Classes\\" + k_UriScheme))
            {
                reflectKey.SetValue("", "URL:" + k_UriScheme);
                reflectKey.SetValue(k_RegistryUrlProtocol, "");
                using (var commandKey = reflectKey.CreateSubKey(k_RegistryShellKey))
                {
                    commandKey.SetValue("", "\"" + applicationLocation + "\" \"%1\"");
                }
            }
        }
    
        void ReadUrlCallback(Uri uri)
        {
            var urlToken = uri.Query.Substring(k_jwtParamName.Length);
            m_Manager.ProcessToken(urlToken);
            if (string.IsNullOrEmpty(ViewProjectId))
            {
                var pathAndQuery = uri.PathAndQuery;
                var startIndex = pathAndQuery.LastIndexOf("/") + 1;
                var endIndex = pathAndQuery.LastIndexOf("?");
                var idString = pathAndQuery.Substring(startIndex, endIndex - startIndex);
                int processOrHandleId = 0;
                int.TryParse(idString, out processOrHandleId);
                IntPtr processIdIntPtr = new IntPtr(processOrHandleId);

                var canExit = true;
                try
                {
                    // If processId not found, keep current instance
                    if (IsIconic(processIdIntPtr))
                    {
                        ShowWindow(processIdIntPtr, ShowWindowEnum.Restore);
                    }
                    canExit = SetForegroundWindow(processIdIntPtr) != 0;
                }
                catch (Exception)
                {
                    canExit = false;
                }
                finally
                {
                    if (canExit)
                    {
                        Application.Quit();
                    }
                }
            }
        }
    }
}
#endif
