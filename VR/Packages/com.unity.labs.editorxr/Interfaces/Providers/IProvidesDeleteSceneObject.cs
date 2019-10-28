using Unity.Labs.ModuleLoader;
using UnityEngine;

namespace Unity.Labs.EditorXR.Interfaces
{
    /// <summary>
    /// Provide access to delete functionality
    /// </summary>
    public interface IProvidesDeleteSceneObject : IFunctionalityProvider
    {
        /// <summary>
        /// Remove the game object from the scene
        /// </summary>
        /// <param name="go">The game object to delete from the scene</param>
        void DeleteSceneObject(GameObject go);
    }
}
