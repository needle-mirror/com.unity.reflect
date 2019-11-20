using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Unity.Reflect;
using UnityEditor;

namespace UnityEngine.Reflect
{
    static class ProjectServerEnvironment
    {        
        public static string ProjectDataPath { get; }

        public static ProjectServerClient Client { get; }

        static UnityUser s_UnityUser;

        static readonly Dictionary<string, ProjectServerClient.CloudEnvironment> k_ProjectServerEnvironmentMap = new Dictionary<string, ProjectServerClient.CloudEnvironment>()
        {
            { "dev", ProjectServerClient.CloudEnvironment.Test },
            { "staging", ProjectServerClient.CloudEnvironment.Staging },
            { "production", ProjectServerClient.CloudEnvironment.Production },
        };

        static ProjectServerEnvironment()
        {
            var cloudConfiguration = ProjectServerClient.CloudEnvironment.Production;

#if UNITY_EDITOR
            var asm = Assembly.GetAssembly(typeof(CloudProjectSettings));
            var unityConnect = asm.GetType("UnityEditor.Connect.UnityConnect");
            var instanceProperty = unityConnect.GetProperty("instance");
            var configurationProperty = unityConnect.GetProperty("configuration");

            var instance = instanceProperty.GetValue(null, null);
            var envValue = (string)configurationProperty.GetValue(instance, null);

            if (!k_ProjectServerEnvironmentMap.TryGetValue(envValue?.ToLower() ?? string.Empty, out cloudConfiguration))
            {
                Debug.LogWarning($"Could not find cloud config environment, using production environment.");
            }
#endif

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

