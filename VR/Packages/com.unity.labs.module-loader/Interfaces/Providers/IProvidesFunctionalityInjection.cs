using System.Collections.Generic;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Defines the API for a Functionality Injection Provider
    /// This functionality provider is responsible for allowing other classes to use Functionality Injection
    /// </summary>
    public interface IProvidesFunctionalityInjection : IFunctionalityProvider
    {
        /// <summary>
        /// Inject functionality into a list of objects
        /// The list is scanned for unique types, and for each unique type, a new provider is created if it does not
        /// already exist.
        /// </summary>
        /// <param name="objects">The list of objects into which functionality will be injected</param>
        /// <param name="newProviders">(Optional) List to which newly created providers will be added</param>
        void InjectFunctionality(List<object> objects, List<IFunctionalityProvider> newProviders = null);

        /// <summary>
        /// Inject functionality into a list of objects
        /// This method assumes that all necessary providers have been prepared.  If no providers
        /// exist that match subscriber interfaces on the object, no action is taken.
        /// Useful in cases where functionality is already setup, as it is faster than InjectFunctionality.
        /// </summary>
        /// <param name="objects">The list of objects into which functionality will be injected</param>
        void InjectPreparedFunctionality(List<object> objects);

        /// <summary>
        /// Inject functionality into a single object
        /// This method does not check the object's type and assumes that providers have been set up. If no providers
        /// exist that match subscriber interfaces on the object, no action is taken.
        /// </summary>
        /// <param name="obj">The object into which functionality will be injected</param>
        void InjectFunctionalitySingle(object obj);
    }
}
