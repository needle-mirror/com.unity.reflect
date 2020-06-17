using System;
using System.Linq;
using UnityEngine.UI;
using UnityEngine.Events;
using Unity.Reflect;
using Unity.Reflect.Services.Client.ProjectServer;
using System.Threading.Tasks;
using Grpc.Core;
using System.Collections;

namespace UnityEngine.Reflect
{
    [Serializable]
    public class UnityUserUnityEvent : UnityEvent<UnityUser> { }

    public class Session : MonoBehaviour
    {
        public Button LoginButton;
        public Button LogoutButton;
        public Text UserDisplayName;

        UnityUser unityUser;
        const string k_AnomymousUser = "Please sign in";
        bool m_IsLoggedIn = false;
        string m_SessionToken = string.Empty;
        bool m_SplashScreenComplete = true; // TODO: temp hack to skip splash screen

        Coroutine m_RefreshUserInfoCoroutine;

        public UnityEvent onAuthenticationFailure;
        public UnityUserUnityEvent onUnityUserChanged;

        void Awake()
        {
            ProjectServerEnvironment.Init();
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            if (UserDisplayName != null)
            {
                UserDisplayName.gameObject.SetActive(m_SplashScreenComplete);
            }
#if UNITY_EDITOR
            if (LoginButton != null)
            {
                LoginButton.gameObject.SetActive(false);
                LogoutButton.gameObject.SetActive(false);
            }

            if (m_SplashScreenComplete)
            {
                if (UserDisplayName != null)
                {
                    UserDisplayName.text = m_IsLoggedIn && unityUser != null ? unityUser.DisplayName : k_AnomymousUser;
                }
            }
#else
            if (m_SplashScreenComplete)
            {
                if (LoginButton != null)
                {
                    LoginButton.gameObject.SetActive(!m_IsLoggedIn);
                }
                if (LogoutButton != null)
                {
                    LogoutButton.gameObject.SetActive(m_IsLoggedIn);
                }
                if (UserDisplayName != null)
                {
                    UserDisplayName.text = m_IsLoggedIn && unityUser != null ? unityUser.DisplayName : k_AnomymousUser;
                }
            }
            else
            {
                if (LoginButton != null)
                {
                    LoginButton.gameObject.SetActive(false);
                }
                if (LogoutButton != null)
                {
                    LogoutButton.gameObject.SetActive(false);
                }
            }
#endif
        }


        public void OnSplashScreenComplete()
        {
            m_SplashScreenComplete = true;
            UpdateDisplay();
        }

        public void SetAccessToken(string token)
        {
            // This seems to be called before awake. Make sure ProjectServerEnvironment
            // is initialized on the main thread.
            ProjectServerEnvironment.Init();
            
            m_SessionToken = token;
            m_IsLoggedIn = !string.IsNullOrEmpty(m_SessionToken);
            if (m_IsLoggedIn)
            {
                GetUserInfo(token);
            }
            else
            {
                unityUser = null;
                onUnityUserChanged?.Invoke(unityUser);
            }
            UpdateDisplay();
        }

        void GetUserInfo(string token)
        {
            if (m_RefreshUserInfoCoroutine != null)
            {
                StopCoroutine(m_RefreshUserInfoCoroutine);
            }
            m_RefreshUserInfoCoroutine = StartCoroutine(GetUserInfoCoroutine(token));
        }

        IEnumerator GetUserInfoCoroutine(string token)
        {
            // Use ContinueWith to make sure the task doesn't throw
            var task = Task.Run(() => ProjectServerEnvironment.Client.GetUserInfo(token));//.ContinueWith(t => t);
            while (!task.IsCompleted)
            {
                yield return null;
            }
            
            if (task.IsFaulted)
            {
                Debug.LogError($"Get User Info failed: {task.Exception}");
                if (task.Exception.InnerExceptions.OfType<RpcException>().Where(x => x.StatusCode.Equals(StatusCode.Unauthenticated) || x.StatusCode.Equals(StatusCode.PermissionDenied)).FirstOrDefault() != null)
                {
                    onAuthenticationFailure?.Invoke();
                }
                yield break;
            }
            unityUser = task.Result;
            onUnityUserChanged?.Invoke(unityUser);
            UpdateDisplay();
        }
    }
}

