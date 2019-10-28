using System.Collections.Generic;

namespace Unity.Labs.ModuleLoader
{
    /// <inheritdoc />
    /// <summary>
    /// Provides access to functionality injection
    /// </summary>
    public interface IUsesFunctionalityInjection : IFunctionalitySubscriber<IProvidesFunctionalityInjection>
    {
    }

    public static class IUsesFunctionalityInjectionMethods
    {
        /// <summary>
        /// Inject functionality into a list of objects
        /// The list is scanned for unique types, and for each unique type, a new provider is created if it does not
        /// already exist.
        /// </summary>
        /// <param name="user">The object using using functionality injection</param>
        /// <param name="objects">The list of objects into which functionality will be injected</param>
        public static void InjectFunctionality(this IUsesFunctionalityInjection user, List<object> objects)
        {
#if !FI_AUTOFILL
            user.provider.InjectFunctionality(objects);
#endif
        }

        /// <summary>
        /// Inject functionality into a single object
        /// This method does not check the object's type and assumes that providers have been set up. If no providers
        /// exist that match subscriber interfaces on the object, no action is taken.
        /// </summary>
        /// <param name="user">The object using using functionality injection</param>
        /// <param name="obj">The object into which functionality will be injected</param>
        public static void InjectFunctionalitySingle(this IUsesFunctionalityInjection user, object obj)
        {
#if !FI_AUTOFILL
            user.provider.InjectFunctionalitySingle(obj);
#endif
        }

        /// <summary>
        /// Inject functionality into a list of objects
        /// This method assumes that all necessary providers have been prepared.  If no providers
        /// exist that match subscriber interfaces on the object, no action is taken.
        /// Useful in cases where functionality is already setup, as it is faster than InjectFunctionality.
        /// </summary>
        /// <param name="user">The object using using functionality injection</param>
        /// <param name="objects">The list of objects into which functionality will be injected</param>
        public static void InjectPreparedFunctionality(this IUsesFunctionalityInjection user, List<object> objects)
        {
#if !FI_AUTOFILL
            user.provider.InjectPreparedFunctionality(objects);
#endif
        }
    }
}
