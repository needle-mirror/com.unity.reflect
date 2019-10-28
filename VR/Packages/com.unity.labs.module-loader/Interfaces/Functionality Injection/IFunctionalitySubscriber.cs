namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Provides a non-generic base for IFunctionalitySubscriber for collecting subscribers
    /// </summary>
    public interface IFunctionalitySubscriber
    {
    }

    /// <inheritdoc />
    /// <summary>
    /// Grants implementors the ability to access functionality provided by a TProvider. Methods on the provider object
    /// are exposed via extension method, so that they can be treated like instance methods. For example, a
    /// provider with a method Foo can be called within an implementing class as this.Foo().
    /// Code generation will fill in a `TProvider provider` property which is used within these extension methods to
    /// call the corresponding method on the provider.
    /// </summary>
    /// <typeparam name="TProvider">The type which will provide functionality</typeparam>
    public interface IFunctionalitySubscriber<TProvider> : IFunctionalitySubscriber where TProvider : IFunctionalityProvider
    {
#if !FI_AUTOFILL
        TProvider provider { get; set; }
#endif
    }

    public static class FunctionalitySubscriberMethods
    {
        public static bool HasProvider<TProvider>(this IFunctionalitySubscriber<TProvider> subscriber)
            where TProvider : class, IFunctionalityProvider
        {
#if FI_AUTOFILL
            return false;
#else
            return subscriber.provider != null;
#endif
        }
    }
}
