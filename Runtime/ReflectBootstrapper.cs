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
        
        public HelperContainer.Proxy Helpers { get; private set; }
        public ServiceContainer Services { get; private set; }
        public SystemContainer.Proxy Systems { get; private set; }

        [Obsolete("Use PostTick event instead")]
        public event Action<float> update;

        public event Action PostTick;

        public virtual void Initialize()
        {
            m_Helpers = new HelperContainer();
            Helpers = new HelperContainer.Proxy(m_Helpers);

            Services = new ServiceContainer(Helpers);

            m_Systems = new SystemContainer(Helpers, Services);
            Systems = new SystemContainer.Proxy(m_Systems);
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

#pragma warning disable 618
            update?.Invoke(0.0f);
#pragma warning restore 618
            PostTick?.Invoke();
        }
    }
}
