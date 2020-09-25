using System;
using System.IO;
using Unity.Reflect;
using UnityEditor;

namespace UnityEngine.Reflect
{
    public static class ProjectServer
    {
        public static string ProjectDataPath { get; set; }

        public static ProjectServerClient Client { get; set; }

        static UnityUser s_UnityUser;

        static bool s_Initialized;

        static ProjectServer()
        {
            Init();
        }

        public static void Init()
        {
            if (s_Initialized)
                return;

            s_Initialized = true;
            
            ProjectDataPath = Path.Combine(Application.persistentDataPath, "ProjectData");
            Client = new ProjectServerClient(ProjectDataPath);
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

