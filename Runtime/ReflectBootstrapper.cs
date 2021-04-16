using System;

namespace UnityEngine.Reflect
{
    /// <summary>
    ///     The base class to access h.s.s. modules.
    /// </summary>
    public class ReflectBootstrapper : IUpdateDelegate
    {
        HelperContainer m_Helpers;
        SystemContainer m_Systems;
        
        public HelperContainer.Proxy helpers { get; private set; }
        public ServiceContainer services { get; private set; }
        public SystemContainer.Proxy systems { get; private set; }

        public event Action<float> update;

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
        }

        public virtual void Stop()
        {
        }

        public virtual void Tick()
        {
            m_Helpers.Tick();
            m_Systems.Tick();

            update?.Invoke(0.0f);
        }
    }
}
