using Grpc.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.Reflect;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace UnityEngine.Reflect
{
    [Serializable]
    public class UnityUserUnityEvent : UnityEvent<UnityUser> { }

    [Serializable]
    public class TokenEvent : UnityEvent<string> {}

    [Serializable]
    public class FailureEvent : UnityEvent<string> {}
    
    [Serializable]
    public class LinkSharingEvent : UnityEvent<string> { }

    [Serializable]
    public class OpenInViewerEvent : UnityEvent<OpenInViewerInfo> { }


    public enum DeepLinkRoute
    {
        none,
        implicitcallbacklogin,
        openprojectrequest,
        opensharelink
    };

    internal static class UrlHelper
    {
        static Regex s_AllowedDomainsRegex = new Regex(@"reflect\.unity(3d)?\.(com|cn)$");

        internal static bool TryCreateUriAndValidate(string uriString, out Uri uriResult)
        {
            return Uri.TryCreate(uriString, UriKind.Absolute, out uriResult) &&
                   string.Equals(uriResult.Scheme, AuthConfiguration.UriScheme, StringComparison.OrdinalIgnoreCase) &&
                   uriResult.Query.StartsWith(AuthConfiguration.JwtParamName);
        }

        internal static bool TryParseDeepLink(string deepLinkUrlString, out string token, out DeepLinkRoute route, out List<string> args)
        {
            token = string.Empty;
            route = DeepLinkRoute.none;
            args = new List<string>();
            if (Uri.TryCreate(deepLinkUrlString, UriKind.Absolute, out var uriResult))
            {
                if (uriResult.Query.Length > 0) 
                {
                    token = uriResult.Query.Substring(AuthConfiguration.JwtParamName.Length);
                }

                var knownRoute = Enum.TryParse(uriResult.Host, out DeepLinkRoute deepLink);
                // Make special case of implicit/callback/login as implicit is a keyword in C#
                if (!knownRoute) 
                {
                    if (uriResult.Host.Equals("implicit") && uriResult.AbsolutePath.StartsWith("/callback/login"))
                    {
                        knownRoute = true;
                        deepLink = DeepLinkRoute.implicitcallbacklogin;
                    }
                    else if (s_AllowedDomainsRegex.IsMatch(uriResult.Host))
                    {
                        knownRoute = true;
                        deepLink = DeepLinkRoute.opensharelink;
                    }
                }

                if (knownRoute) 
                {
                    route = deepLink;
                    args = uriResult.AbsolutePath.Split('/').ToList();
                    args.RemoveAt(0);
                }
                return knownRoute && uriResult.Scheme.Equals(AuthConfiguration.UriScheme, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                return false;
            }
        }
    }

    public class LoginManager : MonoBehaviour
    {
        public UnityUser m_User;
        private Task<UnityUser> m_UnityUserTask;

        public FailureEvent authenticationFailed;
        public TokenEvent tokenUpdated;
        public UnityUserUnityEvent userLoggedIn;
        public UnityEvent userLoggedOut;
        public LinkSharingEvent linkSharingDetected;
        public OpenInViewerEvent openInViewerDetected;

        private IAuthenticatable authBackend;
        private IInteropable m_Interop = null;
        private string m_TokenPersistentPath;
        
        private Coroutine m_SilentLogoutCoroutine;
        private Coroutine m_StartGetUserInfo;
        private Coroutine m_StartSendLogoutEvent;

        static readonly TimeSpan s_SessionExpiryThreshold = TimeSpan.FromDays(5);

        void Awake()
        {
            // Called as soon as we can
            SetupProxy();

            if (tokenUpdated == null)
            {
                tokenUpdated = new TokenEvent();
            }
            if (authenticationFailed == null)
            {
                authenticationFailed = new FailureEvent();
            }
            if (userLoggedIn == null)
            {
                userLoggedIn = new UnityUserUnityEvent();
            }
            if (userLoggedOut == null)
            {
                userLoggedOut = new UnityEvent();
            }
            if (linkSharingDetected == null)
            {
                linkSharingDetected = new LinkSharingEvent();
            }
            if (openInViewerDetected == null)
            {
                openInViewerDetected = new OpenInViewerEvent();
            }

            ProjectServer.Init();

            authBackend = new AuthBackend(this);
#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
            m_Interop = new Interop(this);
            m_Interop.Start();
#endif

            m_TokenPersistentPath = Path.Combine(Application.persistentDataPath, AuthConfiguration.JwtTokenFileName);
        }

        void Start()
        {
            authBackend.Start();
        }

        void FixedUpdate()
        {
            authBackend?.Update();
        }

        void OnDisable()
        {
            authBackend = null;
            if (m_Interop != null) 
            {
                m_Interop.OnDisable();
                m_Interop = null;
            }
            if (m_StartGetUserInfo != null)
            {
                StopCoroutine(m_StartGetUserInfo);
            }
            m_UnityUserTask = null;

            ProjectServer.Cleanup();
        }

        // Code (now highly modified) originally from 3.2 mono implementation
        // https://github.com/mono/mono/blob/mono-3-2/mcs/class/System/System.Net/WebRequest.cs
        // This is to workaround the broken implementation of WebRequest.GetSystemWebProxy() of the current Mono forked version of Unity
        // If any http proxy is found, we refresh the grpc_proxy env variable value to enable grpc calls through Proxy
        void SetupProxy()
        {
#if UNITY_STANDALONE_WIN
            var envVarProxyAddress = Environment.GetEnvironmentVariable("HTTP_PROXY");
            if (string.IsNullOrEmpty(envVarProxyAddress))
            {
                envVarProxyAddress = Environment.GetEnvironmentVariable("http_proxy");
            }
            if (string.IsNullOrEmpty(envVarProxyAddress))
            {
                envVarProxyAddress = Environment.GetEnvironmentVariable("HTTPS_PROXY");
            }
            if (string.IsNullOrEmpty(envVarProxyAddress))
            {
                envVarProxyAddress = Environment.GetEnvironmentVariable("https_proxy");
            }
            if (string.IsNullOrEmpty(envVarProxyAddress))
            {
                envVarProxyAddress = Environment.GetEnvironmentVariable("GRPC_PROXY");
            }
            if (string.IsNullOrEmpty(envVarProxyAddress))
            {
                envVarProxyAddress = Environment.GetEnvironmentVariable("grpc_proxy");
            }
            if (!string.IsNullOrEmpty(envVarProxyAddress))
            {
                return;
            }

            var registryHttpProxy = "";
            try 
            {
                int isProxyEnable = (int)Microsoft.Win32.Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", "ProxyEnable", 0);
                if (isProxyEnable > 0)
                {
                    var strProxyServer = (string)Microsoft.Win32.Registry.GetValue("HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings", "ProxyServer", null);
                    if (!string.IsNullOrEmpty(strProxyServer))
                    {
                        if (strProxyServer.Contains("="))
                        {
                            foreach (var strEntry in strProxyServer.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
                            {
                                if (strEntry.StartsWith("http="))
                                {
                                    registryHttpProxy = strEntry.Substring(5);
                                    break;
                                }
                            }
                        }
                        else
                        {
                            registryHttpProxy = strProxyServer;
                        }
                    }
                }
            }
            catch (System.Security.SecurityException se)
            {
                Debug.LogWarning($"Could not access HKCU proxy values. {se.Message}");
            }
            catch (IOException ioe)
            {
                Debug.LogWarning($"Could not access HKCU proxy values. {ioe.Message}");
            }
            
            // If found Proxy in Windows registry
            if (!string.IsNullOrEmpty(registryHttpProxy))
            {
                if (!registryHttpProxy.StartsWith("http://")) 
                {
                    registryHttpProxy = $"http://{registryHttpProxy}";
                }
                Debug.Log($"Found Proxy in Registry: {registryHttpProxy}");
                Environment.SetEnvironmentVariable("GRPC_PROXY", registryHttpProxy);
            }
#endif
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus || ProjectServer.Provider == LocaleUtils.GetProvider())
            {
                return;
            }

            if (m_User != null)
            {
                Logout();
            }
            ProjectServer.Cleanup();
            ProjectServer.Init();
        }

        private void OnTokenUpdated(string newToken)
        {
            if (!string.IsNullOrEmpty(newToken))
            {
                m_UnityUserTask = Task.Run(() => ProjectServer.Client.GetUserInfo(newToken));
                StartGetUserInfo(m_UnityUserTask);
            }
            else
            {
                StartSendLogoutEvent();
            }
        }

        internal void ProcessDeepLink(string token, DeepLinkRoute route, List<string> args) 
        {
            switch (route) 
            {
                case DeepLinkRoute.openprojectrequest:
                    // /ServerId is first arg, ProjectId is the third
                    if (args.Count >= 4)
                    {
                        Debug.Log($"ProcessDeepLink, detected openprojectrequest Route request to open '{args[3]}' referenced project.");
                        // TODO : use sourceId param to filter visible sources
                        var sourceId = args.Count >= 6 ? args[5] : string.Empty;
                        // last args following projectId or sourceId is the intended viewerId target, it can also be omitted.
                        var viewerId = string.Empty;
                        switch (args.Count) 
                        {
                            case 5:
                                viewerId = args[4];
                                break;
                            case 7:
                                viewerId = args[6];
                                break;
                        }
                        // Automation request can have token attached
                        if (!string.IsNullOrEmpty(token))
                        {
                            ResetToken(token);
                        }
                        openInViewerDetected?.Invoke(new OpenInViewerInfo(args[1], args[3], sourceId, viewerId));
                    }
                    break;
                case DeepLinkRoute.implicitcallbacklogin:
                    ProcessToken(token);
                    break;
                case DeepLinkRoute.opensharelink:
                    // Second argument is the UID reference to be used to query cloud for ProjectId/ServerId 
                    if (args.Count >= 2) 
                    {
                        // Automation request can have token attached
                        if (!string.IsNullOrEmpty(token))
                        {
                            ResetToken(token);
                        }
                        Debug.Log($"ProcessDeepLink, detected opensharelink Route request to open '{args[1]}' referenced project.");
                        linkSharingDetected?.Invoke(args[1]);
                    }
                    break;
            }
        }

        internal void ProcessToken(string token, bool update = true)
        {
            if (!string.IsNullOrEmpty(token))
            {
                if (update)
                {
                    RemovePersistentToken();
                    File.WriteAllText(m_TokenPersistentPath, token);
                }
                OnTokenUpdated(token);
            }
        }

        internal void ReadPersistentToken()
        {
            if (!string.IsNullOrEmpty(m_TokenPersistentPath) && File.Exists(m_TokenPersistentPath))
            {
                ProcessToken(File.ReadAllText(m_TokenPersistentPath));
            }
            else
            {
                OnTokenUpdated(string.Empty);
            }
        }

        void RemovePersistentToken()
        {
            if (File.Exists(m_TokenPersistentPath))
            {
                File.Delete(m_TokenPersistentPath);
            }
        }

        internal void ResetToken(string token) 
        {
            if (m_StartGetUserInfo != null)
            {
                StopCoroutine(m_StartGetUserInfo);
            }
            InvalidateToken();
            ProcessToken(token);
        }

        internal void InvalidateToken()
        {
            RemovePersistentToken();
            OnTokenUpdated(string.Empty);
        }

        public void Login()
        {
            authBackend.Login();
        }

        public void Logout()
        {
            authBackend.Logout();
        }

        internal void StartSendLogoutEvent()
        {
            if (m_StartSendLogoutEvent != null)
            {
                StopCoroutine(m_StartSendLogoutEvent);
            }
            m_StartSendLogoutEvent = StartCoroutine("SendLogoutEvent");
        }

        internal void StartGetUserInfo(System.Object data)
        {
            if (m_StartGetUserInfo != null)
            {
                StopCoroutine(m_StartGetUserInfo);
            }
            m_StartGetUserInfo = StartCoroutine("WaitForSessionCompletion", data);
        }
        
        internal IEnumerator WaitForSessionCompletion(Task<UnityUser> task)
        {
            while (!task.IsCompleted)
            {
                yield return null;
            }
            OnCompletedUnityUserTask(task);
        }

        internal void OnCompletedUnityUserTask(Task<UnityUser> task)
        {
            if (task.IsFaulted)
            {
                Debug.LogError($"Get User Info failed: {task.Exception}");
                authenticationFailed?.Invoke(task.Exception.ToString());
            }
            else
            {
                var user = task.Result;

                if (DateTime.UtcNow > user.SessionExpiry - s_SessionExpiryThreshold &&
                    Application.internetReachability != NetworkReachability.NotReachable)
                {
                    Debug.LogWarning($"User token about to expire, force login");
                    authenticationFailed?.Invoke("Token expired");
                }
                else
                {
                    m_User = user;
                    userLoggedIn?.Invoke(m_User);
                }
            }
            m_UnityUserTask = null;
        }

        internal void SilentInvalidateTokenLogout(string logoutUrl)
        {
            if (m_SilentLogoutCoroutine != null)
            {
                StopCoroutine(m_SilentLogoutCoroutine);
            }
            m_SilentLogoutCoroutine = StartCoroutine(SilentLogoutCoroutine(logoutUrl));
        }

        IEnumerator SilentLogoutCoroutine(string logoutUrl)
        {
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
                    InvalidateToken();
                }
            }
        }

        IEnumerator SendLogoutEvent()
        {
            yield return 0;
            userLoggedOut?.Invoke();
        }

        // Android still calls directly this gameobject function from java context
        // TODO make Android support deeplink from Unity API
        public void onDeeplink(string deepLink)
        {
            onDeepLink(deepLink);
        }

        public void onDeepLink(string deepLink, bool isStartUpLink = false)
        {
            if (UrlHelper.TryParseDeepLink(deepLink, out var token, out var deepLinkRoute, out var deepLinkArgs))
            {   
                ProcessDeepLink(token, deepLinkRoute, deepLinkArgs);
            }
            if (!isStartUpLink && authBackend is IDeepLinkable authBackendDeepLinkable)
            {
                authBackendDeepLinkable.DeepLinkComplete();
            }
        }
    }
}
