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
            Provider = LocaleUtils.GetProvider();
            
            ProjectDataPath = Path.Combine(Application.persistentDataPath, "ProjectData");
            Client = new ProjectServerClient(Provider, appId, ProjectDataPath);
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

