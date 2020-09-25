#if PIPELINE_API
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Reflect;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace UnityEngine.Reflect
{
    [Serializable]
    public struct LoginCredential
    {
        public string jwtToken;
        public bool isSecure;
    }

    [System.Serializable]
    public class TokenEvent : UnityEvent<string> {}

    [System.Serializable]
    public class FailureEvent : UnityEvent<string> {}

    [System.Serializable]
    public class DeepLinkingEvent : UnityEvent<string> {}

    internal static class UrlHelper
    {
        private static readonly string k_UriScheme = "reflect";
        private static readonly string k_jwtParamName = "?jwt=";
        internal static bool TryCreateUriAndValidate(string uriString, UriKind uriKind, out Uri uriResult)
        {
            return Uri.TryCreate(uriString, uriKind, out uriResult) &&
                   string.Equals(uriResult.Scheme, k_UriScheme, StringComparison.OrdinalIgnoreCase) &&
                   uriResult.Query.StartsWith(k_jwtParamName);
        }
    }
    
    public class LoginManager : MonoBehaviour
    {
        public LoginCredential credentials;
        public UserSession session;

        public TokenEvent tokenUpdated;
        public FailureEvent authenticationFailed;
        public UnityUserUnityEvent userLoggedIn;
        public UnityEvent userLoggedOut;
        public DeepLinkingEvent deepLinkingRequested;

        private IAuthenticatable authBackend;
        private string m_TokenPersistentPath;

        private readonly string k_JwtTokenFileName = "jwttoken.data";
        private Coroutine m_SilentLogoutCoroutine;
        private Coroutine m_StartGetUserInfo;
        private Coroutine m_StartSendLogoutEvent;

        void Awake()
        {
            credentials = new LoginCredential() { jwtToken = string.Empty, isSecure = false };
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

            ProjectServer.Init();
#if UNITY_EDITOR
            authBackend = new UnityEditorAuthBackend(this);
#elif UNITY_STANDALONE_OSX
            authBackend = new OSXStandaloneAuthBackend(this);
#elif UNITY_STANDALONE_WIN
            authBackend = new WindowsStandaloneAuthBackend(this);
#elif UNITY_IOS
            authBackend = new IOSAuthBackend(this);
#elif UNITY_ANDROID
            authBackend = new AndroidAuthBackend(this);
#endif
            m_TokenPersistentPath = Path.Combine(Application.persistentDataPath, k_JwtTokenFileName);
        }

        void Start()
        {
            session = new UserSession(this);
            authBackend.Start();
        }

        void FixedUpdate()
        {
            authBackend?.Update();
        }

        void OnDisable()
        {
            authBackend = null;
            session = null;
            ProjectServer.Cleanup();
        }

        internal void ProcessToken(string token, bool update = true)
        {
            if (!string.IsNullOrEmpty(token))
            {
                credentials.jwtToken = token;
                if (update)
                {
                    RemovePersistentToken();
                    File.WriteAllText(m_TokenPersistentPath, token);
                }
                tokenUpdated?.Invoke(token);
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
                tokenUpdated?.Invoke(string.Empty);
            }
        }

        void RemovePersistentToken()
        {
            if (File.Exists(m_TokenPersistentPath))
            {
                File.Delete(m_TokenPersistentPath);
            }
        }
        
        internal void InvalidateToken()
        {
            RemovePersistentToken();
            credentials.jwtToken = string.Empty;
            tokenUpdated?.Invoke(string.Empty);
        }

        public void Login()
        {
            if (session == null)
            {
                session = new UserSession(this);
            }
            authBackend.Login();
        }

        public void Logout()
        {
            if (session != null)
            {
                authBackend.Logout();
                session = null;
            }
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

            session.OnCompletedTask(task);
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

        public void onDeeplink(string deeplink)
        {
            deepLinkingRequested?.Invoke(deeplink);
        }
    }
}
#endif
