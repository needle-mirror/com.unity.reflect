using System.Linq;
using Unity.Labs.Utils.Internal;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

namespace Unity.Labs.ModuleLoader
{
    class ModuleLoaderEditorCallbacks : IPreprocessBuildWithReport, IProcessSceneWithReport, IPostprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            ModuleLoaderCore.OnPreprocessBuild();
        }

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            ModuleLoaderCore.OnProcessScene(scene);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            ModuleLoaderCore.OnPostprocessBuild();
        }
    }

    class ModuleLoaderAssetModificationProcessor : UnityEditor.AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] paths)
        {
            return ModuleLoaderCore.OnWillSaveAssets(paths);
        }

        public static AssetDeleteResult OnWillDeleteAsset(string path, RemoveAssetOptions options)
        {
            return ModuleLoaderCore.OnWillDeleteAsset(path, options);
        }
    }

    class ModuleLoaderAssetPostprocessor : AssetPostprocessor
    {
        public static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            // To ensure that ModuleLoaderCore references the correct ScriptableSettings module instances,
            // we reload modules whenever an IModule ScriptableSettings is imported.
            if (importedAssets
                .Select(AssetDatabase.LoadAssetAtPath<ScriptableSettingsBase>)
                .Any(scriptableSettings => scriptableSettings != null && scriptableSettings is IModule))
            {
                // Since OnPostprocessAllAssets could get called while a module is in the middle of executing some method
                // we delay the call to ModuleLoaderCore.ReloadModules so that we don't interrupt that execution.
                EditorApplication.delayCall += ModuleLoaderCore.instance.ReloadModules;
            }
        }
    }
}
