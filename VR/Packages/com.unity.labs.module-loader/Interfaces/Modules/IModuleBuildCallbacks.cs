using UnityEngine.SceneManagement;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Define this module as one that needs build callbacks
    /// </summary>
    public interface IModuleBuildCallbacks : IModule
    {
#if UNITY_EDITOR
        /// <summary>
        /// Called before the build is started
        /// </summary>
        void OnPreprocessBuild();

        /// <summary>
        /// Called when a scene is processed during the build
        /// </summary>
        /// <param name="scene">The current scene being processed</param>
        void OnProcessScene(Scene scene);

        /// <summary>
        /// Called after the build is complete
        /// </summary>
        void OnPostprocessBuild();
#endif
    }
}
