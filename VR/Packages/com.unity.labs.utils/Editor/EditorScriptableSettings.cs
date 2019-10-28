using UnityEditor;
using UnityEngine;

namespace Unity.Labs.Utils
{
    /// <inheritdoc />
    /// <summary>
    /// Based off of Unity's Internal ScriptableSingleton with UnityEditorInternal bits removed
    /// </summary>
    /// <typeparam name="T">The class being created</typeparam>
    public abstract class EditorScriptableSettings<T> : Internal.ScriptableSettingsBase<T> where T : ScriptableObject
    {
        const string k_SavePathFormat = "Assets/{0}ScriptableSettings/{1}.asset";

        /// <summary>
        /// Retrieves a reference to the given settings class. Will load and initialize once, and cache for all future access.
        /// </summary>
        public static T instance
        {
            get
            {
                if (s_Instance == null)
                    CreateAndLoad();

                return s_Instance;
            }
        }

        static void CreateAndLoad()
        {
            System.Diagnostics.Debug.Assert(s_Instance == null);

            // Try to load the singleton
            const string filter = "t:{0}";
            foreach (var guid in AssetDatabase.FindAssets(string.Format(filter, typeof(T).Name)))
            {
                s_Instance = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (s_Instance != null)
                    break;
            }

            // Create it if it doesn't exist
            if (s_Instance == null)
            {
                s_Instance = CreateInstance<T>();

                // And save it back out if appropriate
                Save(k_SavePathFormat);
            }

            System.Diagnostics.Debug.Assert(s_Instance != null);
        }
    }
}
