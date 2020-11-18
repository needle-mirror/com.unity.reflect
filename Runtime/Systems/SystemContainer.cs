using System;
using System.Diagnostics;

namespace UnityEngine.Reflect
{
    public class SystemContainer
    {
        MemoryCleaner m_MemoryCleaner;
        MemoryCleaner.Proxy m_MemoryCleanerProxy;

        TimeSpan m_LastTime;

        public SystemContainer(HelperContainer.Proxy helpers, ServiceContainer services)
        {
            m_MemoryCleaner = new MemoryCleaner();
            m_MemoryCleanerProxy = new MemoryCleaner.Proxy(m_MemoryCleaner);

            m_LastTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            m_MemoryCleaner.Initialize(helpers.unityStatic, helpers.clock, helpers.memoryStats, services.eventHub);
        }

        public void Start()
        {
            m_MemoryCleaner.Start();
        }

        public void Tick()
        {
            m_MemoryCleaner.Tick();
        }

        public void Shutdown()
        {
            m_MemoryCleaner.Shutdown();
        }

        public struct Proxy
        {
            SystemContainer m_Container;

            public Proxy(SystemContainer container)
            {
                m_Container = container;
            }

            public MemoryCleaner.Proxy memoryCleaner => m_Container.m_MemoryCleanerProxy;
        }
    }
}