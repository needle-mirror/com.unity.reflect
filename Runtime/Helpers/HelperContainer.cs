namespace UnityEngine.Reflect
{
    /// <summary>
    ///     Exposes all helpers that are exposed to services, systems and unity scripts
    /// </summary>
    public class HelperContainer
    {
        UnityStatic m_UnityStatic;
        Clock m_Clock;
        MemoryStats m_MemoryStats;

        Clock.Proxy m_ClockProxy;
        MemoryStats.Proxy m_MemoryStatsProxy;

        public HelperContainer()
        {
            m_UnityStatic = new UnityStatic();

            m_Clock = new Clock();
            m_ClockProxy = new Clock.Proxy(m_Clock);
            m_Clock.Start();

            m_MemoryStats = new MemoryStats();
            m_MemoryStatsProxy = new MemoryStats.Proxy(m_MemoryStats);
        }

        public void Tick()
        {
            m_Clock.Tick();
            m_MemoryStats.Tick();
        }

        public struct Proxy
        {
            HelperContainer m_Container;

            public Proxy(HelperContainer container)
            {
                m_Container = container;
            }

            public UnityStatic UnityStatic => m_Container.m_UnityStatic;
            public Clock.Proxy Clock => m_Container.m_ClockProxy;
            public MemoryStats.Proxy MemoryStats => m_Container.m_MemoryStatsProxy;
        }
    }
}
