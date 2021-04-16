using System;
using System.IO;
using Unity.Reflect;
using Unity.Reflect.Utils;
using UnityEditor;

namespace UnityEngine.Reflect
{
    public static class ProjectServer
    {
        public static string ProjectDataPath { get; private set; }

        public static ProjectServerClient Client { get; private set; }
        
        static UnityUser s_UnityUser;

        static bool s_Initialized;

        public static RegionUtils.Provider Provider { get; private set; }

        static ProjectServer()
        {
            Init();
        }
        
        public static void Init(string appId = "REFLECT_VIEWER")
        {
            if (s_Initialized)
                return;
            
            s_Initialized = true;
            
            var environmentInfo = LocaleUtils.GetEnvironmentInfo();
            Provider = environmentInfo.provider;

            var projectDataSuffix = string.Empty;
            string projectServerAddress;
            if (PlayerPrefs.HasKey(LocaleUtils.SettingsKeys.CloudEnvironment) && environmentInfo.cloudEnvironment == CloudEnvironment.Other)
            {
                projectServerAddress = environmentInfo.customUrl;
                projectDataSuffix = $"-{projectServerAddress.MD5Hash()}";
            }
            else
            {
                projectServerAddress = ProjectServerClient.ProjectServerAddress(environmentInfo.provider, environmentInfo.cloudEnvironment);
                                
                if (environmentInfo.cloudEnvironment != CloudEnvironment.Production)
                {
                    projectDataSuffix = $"-{environmentInfo.provider}-{environmentInfo.cloudEnvironment}";
                }
                // else: No suffix for prod since real users already have data stored in their ProjectData folder
            }

            ProjectDataPath = Path.Combine(Application.persistentDataPath, $"ProjectData{projectDataSuffix}");
            Directory.CreateDirectory(ProjectDataPath);
            
            Client = new ProjectServerClient(projectServerAddress, appId, ProjectDataPath);
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
    }
}

