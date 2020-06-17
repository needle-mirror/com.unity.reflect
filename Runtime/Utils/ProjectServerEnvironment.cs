using System;
using System.Collections.Generic;
using System.IO;
using Unity.Reflect;
using UnityEditor;

namespace UnityEngine.Reflect
{
    public static class ProjectServerEnvironment
    {
        // Editor or Viewer user can set this env var to "staging" or "test" to use Project Server on GCP stg or test
        const string k_ProjectServerAddressEnvVar = "REFLECT_CLOUD";

        public static string ProjectDataPath { get; set; }

        public static ProjectServerClient Client { get; set; }

        static UnityUser s_UnityUser;

        static readonly Dictionary<string, ProjectServerClient.CloudEnvironment> k_ProjectServerEnvironmentMap = new Dictionary<string, ProjectServerClient.CloudEnvironment>()
        {
            { "local", ProjectServerClient.CloudEnvironment.Local  },
            { "test", ProjectServerClient.CloudEnvironment.Test },
            { "staging", ProjectServerClient.CloudEnvironment.Staging },
            { "production", ProjectServerClient.CloudEnvironment.Production },
        };

        static bool s_Initialized;

        static ProjectServerEnvironment()
        {
            Init();
        }

        public static void Init()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;
            
            var env = Environment.GetEnvironmentVariable(k_ProjectServerAddressEnvVar) ?? "production";

            var cloudConfiguration = k_ProjectServerEnvironmentMap[env];
            Debug.Log($"Using cloud configuration: {cloudConfiguration}");

            ProjectDataPath = Path.Combine(Application.persistentDataPath, "ProjectData");
            Client = new ProjectServerClient(cloudConfiguration, ProjectDataPath);
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

