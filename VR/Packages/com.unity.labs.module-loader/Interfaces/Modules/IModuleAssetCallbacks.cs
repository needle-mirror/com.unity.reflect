#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Define this module as one that needs asset modification callbacks
    /// </summary>
    public interface IModuleAssetCallbacks : IModule
    {
#if UNITY_EDITOR
        /// <summary>
        /// Called when Unity is about to write serialized assets or scene files to disk
        /// </summary>
        /// <param name="paths">Path names of assets about to be saved</param>
        /// <returns>Path names of assets to save - this allows the module to override which files are written to disk</returns>
        string[] OnWillSaveAssets(string[] paths);

        /// <summary>
        /// Called when Unity is about to delete an asset from disk
        /// </summary>
        /// <param name="path">Pathname of the asset about to be deleted</param>
        /// <param name="options">How the asset should be deleted</param>
        /// <returns>Whether this callback deleted the asset</returns>
        AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options);
#endif
    }
}
