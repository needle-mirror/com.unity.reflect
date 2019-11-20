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
        bool m_SplashScreenComplete = false;

        Coroutine m_RefreshUserInfoCoroutine;

        public UnityEvent onAuthenticationFailure;
        public UnityUserUnityEvent onUnityUserChanged;

        void Awake()
        {
            UpdateDisplay();
        }

        void UpdateDisplay()
        {
            UserDisplayName.gameObject.SetActive(m_SplashScreenComplete);
#if UNITY_EDITOR
            LoginButton.gameObject.SetActive(false);
            LogoutButton.gameObject.SetActive(false);
            if (m_SplashScreenComplete)
            {
                UserDisplayName.text = m_IsLoggedIn && unityUser != null ? unityUser.DisplayName : k_AnomymousUser;
            }
#else
            if (m_SplashScreenComplete)
            {
                LoginButton.gameObject.SetActive(!m_IsLoggedIn);
                LogoutButton.gameObject.SetActive(m_IsLoggedIn);
                UserDisplayName.text = m_IsLoggedIn && unityUser != null ? unityUser.DisplayName : k_AnomymousUser;
            }
            else
            {
                LoginButton.gameObject.SetActive(false);
                LogoutButton.gameObject.SetActive(false);
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

        public void GetUserInfo(string token)
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
            var task = Task.Run(() => ProjectServerEnvironment.Client.GetUserInfo(token)).ContinueWith(t => t);
            while (!task.IsCompleted)
            {
                yield return null;
            }

            var listTask = task.Result;
            if (listTask.IsFaulted)
            {
                Debug.LogError($"Get User Info failed: {listTask.Exception}");
                if (listTask.Exception.InnerExceptions.OfType<RpcException>().Where(x => x.StatusCode.Equals(StatusCode.Unauthenticated) || x.StatusCode.Equals(StatusCode.PermissionDenied)).FirstOrDefault() != null)
                {
                    onAuthenticationFailure?.Invoke();
                }
                yield break;
            }
            unityUser = listTask.Result;
            onUnityUserChanged?.Invoke(unityUser);
            UpdateDisplay();
        }
    }
}

