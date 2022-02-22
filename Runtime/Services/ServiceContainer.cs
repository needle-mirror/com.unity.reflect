namespace UnityEngine.Reflect
{
    /// <summary>
    ///     References all services that must be exposed to the unity scripts and systems.
    /// </summary>
    public class ServiceContainer
    {
        public EventHub EventHub { get; }
        public MemoryTracker MemoryTracker { get; }

        public ServiceContainer(HelperContainer.Proxy helpers)
        {
            EventHub = new EventHub();
            MemoryTracker = new MemoryTracker(helpers.Clock);
        }
    }
}
