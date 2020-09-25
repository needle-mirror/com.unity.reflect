using System;
using System.Collections;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;
using UnityEngine.Reflect;
using Debug = UnityEngine.Debug;

[Serializable]
public class StringUnityEvent : UnityEvent<string> { }

public static class LoginCredential
{
    public static string jwtToken = string.Empty;
    public static bool isSecure = false;
}

public class LoginResolver : MonoBehaviour
{
    const string k_RegistryUrlProtocol = "URL Protocol";
    const string k_RegistryShellKey = @"shell\open\command";
    const string k_LoginUrl = "https://api.unity.com/v1/oauth2/authorize?client_id=industrial_reflect&response_type=rsa_jwt&state=hello&redirect_uri=reflect://implicit/callback/login/";
    const string k_LogoutUrl = "https://api.unity.com/v1/oauth2/end-session";

    const string k_UriScheme = "reflect";
    const string k_JwtTokenFileName = "jwttoken.data";
    const string k_jwtParamName = "?jwt=";
    bool IsMainViewer = false;
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    bool IsTopMost = false;
#endif
    public static string ViewProjectId = string.Empty;
    string m_ReflectLoginUrl = string.Empty;

    public static readonly bool k_IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static readonly bool k_IsOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static readonly bool k_IsIOS = Application.platform == RuntimePlatform.IPhonePlayer;
    public static readonly bool k_IsAndroid = Application.platform == RuntimePlatform.Android;

    private IInteropable m_Interop;

    public StringUnityEvent OnGetToken;
    Coroutine m_SilentLogoutCoroutine;

    string k_JwtTokenPersistentPath = string.Empty;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void UnityDeeplinks_init(string gameObject = null, string deeplinkMethod = null);
    [DllImport("__Internal")]
    extern static void LaunchSafariWebViewUrl(string url);
    [DllImport("__Internal")]
    extern static void DismissSafariWebView();
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR

    [DllImport("OSXReflectViewerPlugin")]
    static extern void DeepLink_Reset();

    [DllImport("OSXReflectViewerPlugin")]
    static extern string DeepLink_GetURL();

    [DllImport("OSXReflectViewerPlugin")]
    static extern string DeepLink_GetProcessId();
#endif

    void Start()
    {
        k_JwtTokenPersistentPath = Path.Combine(Application.persistentDataPath, k_JwtTokenFileName);
        m_ReflectLoginUrl = k_LoginUrl;

#if NET_STANDARD_2_0 && UNITY_EDITOR
        Debug.LogWarning(
            "The Unity Reflect package requires .NET 4.x API Compatibility Level on Windows."
            + "Please select this option in your Project Settings to avoid any build errors "
            + "related to framework compatibility (Edit -> Project Settings... -> Player -> "
            + "Other Settings -> Api Compatibility Level)."
        );
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        m_ReflectLoginUrl = $"{k_LoginUrl}{DeepLink_GetProcessId()}";
#endif

#if UNITY_IOS && !UNITY_EDITOR
        UnityDeeplinks_init(gameObject.name);
#endif
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        if (m_Interop == null) 
        {
            m_Interop = new WindowsStandaloneInterop();
            m_Interop.Start();
        }

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

        if (string.IsNullOrEmpty(trailingArg))
        {
            IsMainViewer = true;
        }
        else
        {
            var splitRequest = trailingArg.Split(' ');
            // Request from dashboard to open specific project in app
            // OPEN_PROJECT_REQUEST ?jwt=token projectId sourceId
            if (trailingArg.StartsWith("OPEN_PROJECT_REQUEST") && (splitRequest.Length > 2)) 
            {
                ViewProjectId = splitRequest[2];
                if (TryCreateUriAndValidate(splitRequest[1], UriKind.Absolute, out var uri))
                {
                    ReadUrlCallback(uri);
                }   
            }
            else 
            {
                if (TryCreateUriAndValidate(trailingArg, UriKind.Absolute, out var uri))
                {
                    ReadUrlCallback(uri);
                }
            }
        }
#endif
    }

    void FixedUpdate()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        if (k_IsOSX)
        {
            if (DeepLink_GetURL() == "") return;
            onDeeplink(DeepLink_GetURL());
            DeepLink_Reset();
        }
#endif
    }

    bool TryCreateUriAndValidate(string uriString, UriKind uriKind, out Uri uriResult)
    {
        return Uri.TryCreate(uriString, uriKind, out uriResult) && 
            string.Equals(uriResult.Scheme, k_UriScheme, StringComparison.OrdinalIgnoreCase) && 
            uriResult.Query.StartsWith(k_jwtParamName);
    }

    void SetLoginUrlWithWindowPtr() 
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        IntPtr hWnd = GetWindowHandle();
        m_ReflectLoginUrl = $"{k_LoginUrl}{hWnd}";
#endif
    }

    // Focus will be given (or taken by user) after browser login redirection
    // We use this event to try and read the local file containing the jwt token.
    void OnApplicationFocus(bool hasFocus)
    {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            if (hasFocus)
            {
                SetLoginUrlWithWindowPtr();
            }
#endif

        if (IsMainViewer)
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            var viewerTokenFilePath = $"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}/Unity/Reflect/{Application.productName}/{k_JwtTokenFileName}";
            if (File.Exists(viewerTokenFilePath))
            {
                if (hasFocus)
                {
                    var nextToken = File.ReadAllText(viewerTokenFilePath);
                    if (!nextToken.Equals(LoginCredential.jwtToken))
                    {
                        LoginCredential.jwtToken = nextToken;
                        ProcessJwtToken();
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
#endif
        }
    }

    // Android/iOS entry point
    public void onDeeplink(string deeplink)
    {
        // Debug.Log($"onDeeplink {deeplink}");
        if (TryCreateUriAndValidate(deeplink, UriKind.Absolute, out var uri))
        {
            LoginCredential.jwtToken = uri.Query.Substring(k_jwtParamName.Length);
            ProcessJwtToken();
#if UNITY_IOS && !UNITY_EDITOR
            DismissSafariWebView();
#endif
        }
    }

    void ReadUrlCallback(Uri uri)
    {
        LoginCredential.jwtToken = uri.Query.Substring(k_jwtParamName.Length);
        if (!string.IsNullOrEmpty(ViewProjectId))
        {
            ProcessJwtToken();
        }
        else
        {
            var saveToDirectory = $"{Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)}/Unity/Reflect/{Application.productName}";
            if (!Directory.Exists(saveToDirectory))
            {
                Directory.CreateDirectory(saveToDirectory);
            }
            var viewerTokenFilePath = Path.Combine(saveToDirectory, k_JwtTokenFileName);
            File.WriteAllText(viewerTokenFilePath, LoginCredential.jwtToken);

            var absolutePath = uri.AbsolutePath.ToString();
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
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                // If processId not found, keep current instance
                SetWindowPos(processIdIntPtr, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                canExit = SetForegroundWindow(processIdIntPtr) != 0;
#endif
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
                else
                {
                    ProcessJwtToken();
                    if (File.Exists(viewerTokenFilePath))
                    {
                        File.Delete(viewerTokenFilePath);
                    }
                }
            }
        }
    }

    public void OnSplashScreenComplete()
    {
#if UNITY_EDITOR
        LoginCredential.jwtToken = UnityEditor.CloudProjectSettings.accessToken;
        ProcessJwtToken(false);
#else
        ReadPersistentToken();
#endif
    }

    public void OnAuthenticationFailure()
    {
#if UNITY_EDITOR
        Debug.Log("Authentication Failure. A valid Unity User session is required to access Reflect services. Please Signin with the Unity Hub.");
#else
        Debug.Log("Authentication Failure. A valid Unity User session is required to access Reflect services.");
        RemovePersistentToken();
        LoginCredential.jwtToken = string.Empty;
        OnGetToken.Invoke(string.Empty);
#endif
    }

    void ReadPersistentToken()
    {
#if !UNITY_EDITOR
        if (!string.IsNullOrEmpty(k_JwtTokenPersistentPath) && File.Exists(k_JwtTokenPersistentPath))
        {
            LoginCredential.jwtToken = File.ReadAllText(k_JwtTokenPersistentPath);
            ProcessJwtToken();
        } 
        else
        {
            OnGetToken.Invoke(string.Empty);
        }
#endif
    }

    void Awake()
    {
        OnSplashScreenComplete();
    }

    void OnDisable() 
    {
        if (m_Interop != null) 
        {
            m_Interop.OnDisable();
            m_Interop = null;
        }
        ProjectServer.Cleanup();
    }

    void RemovePersistentToken()
    {
        if (File.Exists(k_JwtTokenPersistentPath))
        {
            File.Delete(k_JwtTokenPersistentPath);
        }
    }
    public void DisplayLoginBrowserWindow()
    {
        Debug.Log($"Sign in using: {m_ReflectLoginUrl}");
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        AddRegistryKeys();
#endif
#if UNITY_IOS && !UNITY_EDITOR
        LaunchSafariWebViewUrl(m_ReflectLoginUrl);
#else
        Application.OpenURL(m_ReflectLoginUrl);
#endif
    }

    public void LogoutRequest()
    {
        var unityUserLogoutUrl = ProjectServer.UnityUser?.LogoutUrl?.AbsoluteUri;
        if (!string.IsNullOrEmpty(unityUserLogoutUrl))
        {
            Debug.Log($"Sign out using: {unityUserLogoutUrl}");
#if UNITY_IOS && !UNITY_EDITOR
            InvalidateToken();
            LaunchSafariWebViewUrl(unityUserLogoutUrl);
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
            InvalidateToken();
            var wHandle = GetWindowHandle();
            SetWindowPos(wHandle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
            IsTopMost = true;
            Application.OpenURL(unityUserLogoutUrl);
#else
            SilentInvalidateTokenLogout(unityUserLogoutUrl);
#endif
        }
        else
        {
            Debug.Log($"Sign out using: {k_LogoutUrl}");
            InvalidateToken();
#if UNITY_IOS && !UNITY_EDITOR
            LaunchSafariWebViewUrl(k_LogoutUrl);
#else
            Application.OpenURL(k_LogoutUrl);
#endif
        }
    }

    void InvalidateToken()
    {
        RemovePersistentToken();
        LoginCredential.jwtToken = string.Empty;
        OnGetToken.Invoke(string.Empty);
    }

    public void SilentInvalidateTokenLogout(string logoutUrl)
    {
        if (m_SilentLogoutCoroutine != null)
        {
            StopCoroutine(m_SilentLogoutCoroutine);
        }
        m_SilentLogoutCoroutine = StartCoroutine(SilentLogoutCoroutine(logoutUrl));
    }

    IEnumerator SilentLogoutCoroutine(string logoutUrl)
    {
        InvalidateToken();
        using (UnityWebRequest webRequest = UnityWebRequest.Get(logoutUrl))
        {
            yield return webRequest.SendWebRequest();
            if (webRequest.isNetworkError)
            {
                Debug.Log($"LogoutUrl Error: " + webRequest.error);
            }
            else
            {
                Debug.Log("LogoutUrl success");
            }
        }
    }

    void ProcessJwtToken(bool saveIfNewer = true)
    {
        if (!string.IsNullOrEmpty(LoginCredential.jwtToken))
        {
            Debug.Log($"Processing jwt token.");
            if (saveIfNewer)
            {
                RemovePersistentToken();
                File.WriteAllText(k_JwtTokenPersistentPath, LoginCredential.jwtToken);
            }
            OnGetToken.Invoke(LoginCredential.jwtToken);
        }
    }

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
    [DllImport("user32.dll")]
    static extern int SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    static extern int ShowWindow(IntPtr hwnd, ShowWindowEnum winEnum);

    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

    [DllImport("user32.dll")]
    public static extern IntPtr FindWindow(string className, string windowName);

    [DllImport("User32.dll")]
    private static extern bool IsIconic(IntPtr handle);

    [DllImport("user32.dll")]
    static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    
    public static IntPtr GetWindowHandle()
    {
        // "UnityWndClass" is the string value returned when invoking user32.dll GetClassName function
        IntPtr hWnd = FindWindow("UnityWndClass", Application.productName);
        if (hWnd != IntPtr.Zero) 
        {
            return hWnd;
        }
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
        var appDomainLocation = typeof(AppDomain).Assembly.Location;
        var subPath = appDomainLocation.Substring(0, appDomainLocation.LastIndexOf("_Data\\Managed\\"));
        var lastFolderIndex = subPath.LastIndexOf("\\");
        var exePathRoot =  subPath.Substring(0, lastFolderIndex + 1);
        var exeAppName = subPath.Substring(lastFolderIndex + 1);
        var applicationLocation =  $"{exePathRoot}{exeAppName}.exe";

        // Early exit if entry already exists and has the current application path
        using (Microsoft.Win32.RegistryKey reflectKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("SOFTWARE\\Classes\\" + k_UriScheme, false))
        {
            if (reflectKey != null)
            {
                var shellKey = reflectKey.OpenSubKey(k_RegistryShellKey);
                if (shellKey != null)
                {
                    var shellKeyApplicationPath = shellKey.GetValue("") as string;
                    if (shellKeyApplicationPath.Contains(applicationLocation))
                    {
                        return;
                    }
                }
            }
        }
        RegisterUriScheme(applicationLocation);
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

#endif

}
