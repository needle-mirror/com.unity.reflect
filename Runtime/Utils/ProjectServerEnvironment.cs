using System;
using System.Reflection;
using UnityEditor;
using Unity.Reflect.Services.Client.ProjectServer;
using System.Collections.Generic;
using Unity.Reflect;

namespace UnityEngine.Reflect
{
    static class ProjectServerEnvironment
    {
        static readonly ProjectServerClient.CloudEnvironment s_CloudConfiguration = ProjectServerClient.CloudEnvironment.Production;

        public static ProjectServerClient Client { get; } = new ProjectServerClient(s_CloudConfiguration);

#if !UNITY_EDITOR
        static UnityUser s_UnityUser;
#endif

        static readonly Dictionary<string, ProjectServerClient.CloudEnvironment> k_ProjectServerEnvironmentMap = new Dictionary<string, ProjectServerClient.CloudEnvironment>()
        {
            { "dev", ProjectServerClient.CloudEnvironment.Test },
            { "staging", ProjectServerClient.CloudEnvironment.Staging },
            { "production", ProjectServerClient.CloudEnvironment.Production },
        };

        static ProjectServerEnvironment()
        {
#if UNITY_EDITOR
            var asm = Assembly.GetAssembly(typeof(CloudProjectSettings));
            var unityConnect = asm.GetType("UnityEditor.Connect.UnityConnect");
            var instanceProperty = unityConnect.GetProperty("instance");
            var configurationProperty = unityConnect.GetProperty("configuration");

            var instance = instanceProperty.GetValue(null, null);
            var envValue = (string)configurationProperty.GetValue(instance, null);

            if (!k_ProjectServerEnvironmentMap.TryGetValue(envValue?.ToLower() ?? string.Empty, out s_CloudConfiguration))
            {
                Debug.LogWarning($"Could not find cloud config environment, using production environment.");
            }
#endif
        }

        public static UnityUser UnityUser
        {
            get
            {
#if UNITY_EDITOR
                if (string.IsNullOrEmpty(CloudProjectSettings.accessToken))
                {
                    return null;
                }

                return new UnityUser(
                    CloudProjectSettings.accessToken,
                    CloudProjectSettings.userName,
                    CloudProjectSettings.userId);
#else
                return s_UnityUser;
#endif
            }
            set
            {
#if !UNITY_EDITOR
                s_UnityUser = value;
#endif
            }
        }
    }
}

