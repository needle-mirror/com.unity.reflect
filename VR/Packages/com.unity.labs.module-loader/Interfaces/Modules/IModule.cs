namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Exposes this type to the system as a module that will be loaded when starting the app/editor
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Called after all modules have been instantiated and dependencies are connected
        /// </summary>
        void LoadModule();

        /// <summary>
        /// Called before system shuts down
        /// </summary>
        void UnloadModule();
    }
}
