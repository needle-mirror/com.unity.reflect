using System;
using System.Collections;
using System.IO;
using Unity.Reflect;
using Unity.Reflect.Utils;
using UnityEditor;
using Unity.Reflect.Runtime;
using UnityEngine.Networking;

namespace UnityEngine.Reflect
{
    public static class ProjectServer
    {
        public static string ProjectDataPath { get; private set; }

        public static ProjectServerClient Client { get; private set; }

        static UnityUser s_UnityUser;

        static bool s_Initialized;

        const string k_DefaultAppId = "REFLECT_REFERENCE_VIEWER";
        static string s_AppId = k_DefaultAppId;

        static string s_ProjectServerAddress;

        public static RegionUtils.Provider Provider { get; private set; }

        static ProjectServer()
        {
            Init();
        }

        public static void SetAppId(string appId)
        {
            if (string.IsNullOrEmpty(appId))
                s_AppId = k_DefaultAppId;
            else
                s_AppId = appId;

            Cleanup();
            Init();
        }

        public static void Init()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;

            var environmentInfo = LocaleUtils.GetEnvironmentInfo();
            Provider = environmentInfo.provider;

            var projectDataSuffix = string.Empty;
            if (PlayerPrefs.HasKey(LocaleUtils.SettingsKeys.CloudEnvironment) && environmentInfo.cloudEnvironment == CloudEnvironment.Other)
            {
                s_ProjectServerAddress = environmentInfo.customUrl;
                projectDataSuffix = $"-{s_ProjectServerAddress.MD5Hash()}";
            }
            else
            {
                s_ProjectServerAddress = ProjectServerClient.ProjectServerAddress(
                    environmentInfo.provider,
                    environmentInfo.cloudEnvironment,
                    protocol: Protocol.Http);

                if (environmentInfo.cloudEnvironment != CloudEnvironment.Production)
                {
                    projectDataSuffix = $"-{environmentInfo.provider}-{environmentInfo.cloudEnvironment}";
                }
                // else: No suffix for prod since real users already have data stored in their ProjectData folder
            }

            ProjectDataPath = Path.Combine(Application.persistentDataPath, $"ProjectData{projectDataSuffix}");
            Directory.CreateDirectory(ProjectDataPath);

            var reflectRequestHandler = new ReflectRequestHandler();
            Client = ProjectServerClient.Create(s_ProjectServerAddress, s_AppId, reflectRequestHandler, ProjectDataPath);
        }

        public static void Cleanup()
        {
            if (!s_Initialized)
                return;

            s_Initialized = false;

            ProjectDataPath = String.Empty;

            Client.Dispose();
            Client = null;
        }

        public static UnityUser UnityUser
        {
            get
            {
#if UNITY_EDITOR
                if (!EditorApplication.isPlaying)
                {
                    if (string.IsNullOrEmpty(CloudProjectSettings.accessToken))
                    {
                        return null;
                    }

                    return new UnityUser(
                        CloudProjectSettings.accessToken,
                        CloudProjectSettings.userName,
                        CloudProjectSettings.userId);
                }
#endif
                return s_UnityUser;
            }
            set
            {
                s_UnityUser = value;
            }
        }

        public static IEnumerator CheckProjectServerConnection(Action<bool> action)
        {
            bool result;
            using (var request = UnityWebRequest.Head(s_ProjectServerAddress))
            {
                request.timeout = 4;
                yield return request.SendWebRequest();
                result = request.result != UnityWebRequest.Result.ConnectionError;
            }
            action (result);
        }
    }
}

