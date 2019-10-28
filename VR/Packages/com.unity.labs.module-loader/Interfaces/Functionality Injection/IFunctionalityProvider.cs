namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Provides functionality for an IFunctionalitySubscriber
    /// By requiring that the provider template in IFunctionalitySubscriber inherit IFunctionalityProvider, we allow discovery
    /// and enumeration of providers. This is not required for Functionality injection to work, but allows us to
    /// distinguish providers from other types
    /// </summary>
    public interface IFunctionalityProvider
    {
        /// <summary>
        /// Called when the provider is loaded into the <c>FunctionalityInjectionModule</c>
        /// </summary>
        void LoadProvider();

        /// <summary>
        /// Called by the <c>FunctionalityInjectionModule</c> when injecting functionality on an object
        /// </summary>
        /// <param name="obj">The object onto which functionality is being injected. If this implements a subscriber
        /// interface that subscribes to functionality provided by this object, it will set itself as the provider</param>
        void ConnectSubscriber(object obj);

        /// <summary>
        /// Called when the provider is unloaded into the <c>FunctionalityInjectionModule</c>
        /// </summary>
        void UnloadProvider();
    }
}
