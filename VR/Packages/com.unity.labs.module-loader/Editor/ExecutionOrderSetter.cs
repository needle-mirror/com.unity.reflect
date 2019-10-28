using Unity.Labs.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Runtime hooks for ModuleCallbacksBehaviour.  One of these must be in any scene which depends on modules for it to function properly
    /// </summary>
    [InitializeOnLoad]
    public static class ExecutionOrderSetter
    {
        // For some reason, we can't set an execution order as low as int.MinValue
        public const int ModuleManagerExecutionOrder = short.MinValue / 2;

        static ExecutionOrderSetter()
        {
            var moduleManager = new GameObject().AddComponent<ModuleCallbacksBehaviour>();
            var managerMonoScript = MonoScript.FromMonoBehaviour(moduleManager);
            if (MonoImporter.GetExecutionOrder(managerMonoScript) != ModuleManagerExecutionOrder)
                MonoImporter.SetExecutionOrder(managerMonoScript, ModuleManagerExecutionOrder);

            UnityObjectUtils.Destroy(moduleManager.gameObject);
        }
    }
}
