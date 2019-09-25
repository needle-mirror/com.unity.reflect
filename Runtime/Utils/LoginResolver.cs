using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Events;
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

    const string k_UriScheme = "reflect";
    const string k_JwtTokenFileName = "jwttoken.data";
    const string k_jwtParamName = "?jwt=";
    bool IsMainViewer = false;
    string reflectLoginUrl = string.Empty;

    public static readonly bool k_IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
    public static readonly bool k_IsOSX = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    public static readonly bool k_IsIOS = Application.platform == RuntimePlatform.IPhonePlayer;
    public static readonly bool k_IsAndroid = Application.platform == RuntimePlatform.Android;

    public StringUnityEvent OnGetToken;
    string k_JwtTokenPersistentPath = string.Empty;

#if UNITY_IOS && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void UnityDeeplinks_init(string gameObject = null, string deeplinkMethod = null);
#endif

#if UNITY_STANDALONE_OSX && !UNITY_EDITOR

    [DllImport("__Internal")]
    static extern void DeepLink_Reset();

    [DllImport("__Internal")]
    static extern string DeepLink_GetURL();

    [DllImport("__Internal")]
    static extern string DeepLink_GetSourceApplication();
#endif

    void Start()
    {
        k_JwtTokenPersistentPath = Path.Combine(Application.persistentDataPath, k_JwtTokenFileName);
        reflectLoginUrl = k_LoginUrl;

#if UNITY_IOS && !UNITY_EDITOR
        UnityDeeplinks_init(gameObject.name);
#endif
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        AddRegistryKeys();
#endif

#if UNITY_EDITOR
        if (!string.IsNullOrEmpty(UnityEditor.CloudProjectSettings.accessToken))
        {
            // WARNING accessToken is not a jwtToken, so it will not be decodable with the jwtDecoder
            LoginCredential.jwtToken = UnityEditor.CloudProjectSettings.accessToken;
        }
#else
        if (!string.IsNullOrEmpty(k_JwtTokenPersistentPath) && File.Exists(k_JwtTokenPersistentPath))
        {
            LoginCredential.jwtToken = File.ReadAllText(k_JwtTokenPersistentPath);
        }
#endif

#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
        string[] args = Environment.GetCommandLineArgs();
        var myHandle = GetWindowHandle();
        reflectLoginUrl = $"{k_LoginUrl}{myHandle}";
        // Unity usual start command have the path to application as single argument
        if (args.Length == 1)
        {
            IsMainViewer = true;
        }
        else
        {
            if (args.Length > 1 && TryCreateUriAndValidate(args[1], UriKind.Absolute, out var uri))
            {
                ReadUrlCallback(uri);
            }
        }
#endif

        // If token was not recovered at start, get it !
        if (string.IsNullOrEmpty(LoginCredential.jwtToken))
        {
#if !UNITY_EDITOR
            // TODO bring ui to ask for connection
            LoginRequest();
#endif
        }
        else
        {
            ProcessJwtToken();
        }
    }

    void FixedUpdate()
    {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
        if (k_IsOSX)
        {
            if (DeepLink_GetURL() == "") return;
            var deepLink = DeepLink_GetURL();
            if (TryCreateUriAndValidate(deepLink, UriKind.Absolute, out var uri))
            {
                LoginCredential.jwtToken = uri.Query.Substring(k_jwtParamName.Length);
                ProcessJwtToken();
            }
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

    // Focus will be given (or taken by user) after browser login redirection
    // We use this event to try and read the local file containing the jwt token.
    void OnApplicationFocus(bool hasFocus)
    {
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
#endif
        }
    }

    // Android/iOS entry point
    public void onDeeplink(string deeplink)
    {
        Debug.Log($"onDeeplink {deeplink}");
        if (TryCreateUriAndValidate(deeplink, UriKind.Absolute, out var uri))
        {
            LoginCredential.jwtToken = uri.Query.Substring(k_jwtParamName.Length);
            ProcessJwtToken();
        }
    }

    void ReadUrlCallback(Uri uri)
    {
        LoginCredential.jwtToken = uri.Query.Substring(k_jwtParamName.Length);
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
        
        try
        {
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
                // TODO if processId not found, keep current instance
                ShowWindow(processIdIntPtr, ShowWindowEnum.Restore);
                SetForegroundWindow(processIdIntPtr);
#endif
        }
        catch (Exception)
        {
            // Logging Exception here
        }
        Application.Quit();
    }

    public void OnAuthenticationFailure()
    {
#if UNITY_EDITOR
        Debug.Log("A valid AccessToken is required to list project. Please Signin.");
#else
        RemovePersistentToken();
        LoginRequest();
#endif
    }

    void RemovePersistentToken()
    {
        if (File.Exists(k_JwtTokenPersistentPath))
        {
            File.Delete(k_JwtTokenPersistentPath);
        }
    }

    public void LoginRequest()
    {
        Application.OpenURL(reflectLoginUrl);
    }

    void ProcessJwtToken(bool saveIfNewer = true)
    {
        if (!string.IsNullOrEmpty(LoginCredential.jwtToken))
        {
            Debug.Log($"Using jwt token credential: '{LoginCredential.jwtToken}'");
            if(saveIfNewer)
            {
                RemovePersistentToken();
                File.WriteAllText(k_JwtTokenPersistentPath, LoginCredential.jwtToken);
            }
            OnGetToken.Invoke(LoginCredential.jwtToken);
        }
    }

#if UNITY_STANDALONE_WIN  && !UNITY_EDITOR
    [DllImport("user32.dll")]
    static extern int SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    static extern int ShowWindow(IntPtr hwnd, ShowWindowEnum winEnum);

    [DllImport("user32.dll")]
    static extern IntPtr GetActiveWindow();

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

#endif

}
