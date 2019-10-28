namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Define this module as one that needs behavior callbacks
    /// These methods provide entry points for scene load and unload operations during MonoBehaviour callback phases,
    /// i.e. using capabilities to create required providers
    /// </summary>
    public interface IModuleBehaviorCallbacks : IModule
    {
        /// <summary>
        /// Called by ModuleManager Awake as early as possible (using a very low Script Execution Order)
        /// </summary>
        void OnBehaviorAwake();

        /// <summary>
        /// Called by ModuleManager OnEnable as early as possible (using a very low Script Execution Order)
        /// </summary>
        void OnBehaviorEnable();

        /// <summary>
        /// Called by ModuleManager Start as early as possible (using a very low Script Execution Order)
        /// </summary>
        void OnBehaviorStart();

        /// <summary>
        /// Called by ModuleManager Update as early as possible (using a very low Script Execution Order)
        /// </summary>
        void OnBehaviorUpdate();

        /// <summary>
        /// Called by ModuleManager OnDisable as early as possible (using a very low Script Execution Order)
        /// </summary>
        void OnBehaviorDisable();

        /// <summary>
        /// Called by ModuleManager OnDestroy as early as possible (using a very low Script Execution Order)
        /// </summary>
        void OnBehaviorDestroy();
    }
}
