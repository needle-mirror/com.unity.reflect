namespace UnityEngine.Reflect
{
    /// <summary>
    ///     References all services that must be exposed to the unity scripts and systems.
    /// </summary>
    public class ServiceContainer
    {
        public EventHub eventHub { get; }
        public MemoryTracker memoryTracker { get; }

        public ServiceContainer(HelperContainer.Proxy helpers)
        {
            eventHub = new EventHub();
            memoryTracker = new MemoryTracker(helpers.clock);
        }
    }
}
