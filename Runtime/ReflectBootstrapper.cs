namespace UnityEngine.Reflect
{
    /// <summary>
    ///     The base class to access h.s.s. modules.
    ///     Inherits from <see cref="ReflectBootstrapperBehavior"/> and from <see cref="ReflectBootstrapper"/> to add new h.s.s modules.
    /// </summary>
    public class ReflectBootstrapper
    {
        HelperContainer m_Helpers;
        SystemContainer m_Systems;
        IUpdateDelegate m_Update;

        public HelperContainer.Proxy helpers { get; private set; }
        public ServiceContainer services { get; private set; }
        public SystemContainer.Proxy systems { get; private set; }

        public ReflectBootstrapper(IUpdateDelegate update)
        {
            m_Update = update;
        }

        public virtual void Initialize()
        {
            m_Helpers = new HelperContainer();
            helpers = new HelperContainer.Proxy(m_Helpers);

            services = new ServiceContainer(helpers);

            m_Systems = new SystemContainer(helpers, services);
            systems = new SystemContainer.Proxy(m_Systems);
        }

        public virtual void Shutdown()
        {
            m_Systems.Shutdown();
        }

        public virtual void Start()
        {
            m_Systems.Start();
            m_Update.update += Tick;
        }

        public virtual void Stop()
        {
            m_Update.update -= Tick;
        }

        protected virtual void Tick(float unscaledDeltaTime)
        {
            m_Helpers.Tick();
            m_Systems.Tick();
        }
    }
}
