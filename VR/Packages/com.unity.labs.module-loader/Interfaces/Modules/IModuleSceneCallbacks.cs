using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor.SceneManagement;
#endif

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Define this module as one that needs scene management callbacks
    /// </summary>
    public interface IModuleSceneCallbacks : IModule
    {
        /// <summary>
        /// Called after the scene is loaded, after MonoBehaviour Awake
        /// </summary>
        /// <param name="scene">The scene that was loaded</param>
        /// <param name="mode">The mode used to load the scene</param>
        void OnSceneLoaded(Scene scene, LoadSceneMode mode);

        /// <summary>
        /// Called after the scene is unloaded
        /// </summary>
        /// <param name="scene">The scene that was unloaded</param>
        void OnSceneUnloaded(Scene scene);

        /// <summary>
        /// Called when the active scene changes
        /// </summary>
        /// <param name="oldScene">The previously active scene</param>
        /// <param name="newScene">The scene that will become active</param>
        void OnActiveSceneChanged(Scene oldScene, Scene newScene);

#if UNITY_EDITOR
        /// <summary>
        /// Called after a new scene is created in the Editor
        /// </summary>
        /// <param name="scene">The scene that was created</param>
        /// <param name="setup">The NewSceneSetup of the created scene</param>
        /// <param name="mode">The mode that was used to create the new scene</param>
        void OnNewSceneCreated(Scene scene, NewSceneSetup setup, NewSceneMode mode);

        /// <summary>
        /// Called before a scene is opened in the Editor
        /// </summary>
        /// <param name="path">The path of the scene that will be opened</param>
        /// <param name="mode">The mode that will be used to open the scene</param>
        void OnSceneOpening(string path, OpenSceneMode mode);

        /// <summary>
        /// Called after the scene is opened in the Editor
        /// </summary>
        /// <param name="scene">The scene that was opened</param>
        /// <param name="mode">The mode that was used to open the scene</param>
        void OnSceneOpened(Scene scene, OpenSceneMode mode);
#endif
    }
}
