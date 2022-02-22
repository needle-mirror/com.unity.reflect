using Unity.Reflect;

namespace UnityEngine.Reflect
{
    public class SystemContainer
    {
        MemoryCleaner m_MemoryCleaner;
        MemoryCleaner.Proxy m_MemoryCleanerProxy;
        ActorRunner m_ActorRunner;
        ActorRunner.Proxy m_ActorRunnerProxy;

        public SystemContainer(HelperContainer.Proxy helpers, ServiceContainer services)
        {
            m_MemoryCleaner = new MemoryCleaner();
            m_MemoryCleanerProxy = new MemoryCleaner.Proxy(m_MemoryCleaner);
            m_ActorRunner = new ActorRunner();
            m_ActorRunnerProxy = new ActorRunner.Proxy(m_ActorRunner);
            
            m_MemoryCleaner.Initialize(helpers.UnityStatic, helpers.Clock, helpers.MemoryStats, services.EventHub);
        }

        public void Start()
        {
            m_MemoryCleaner.Start();
        }

        public void Tick()
        {
            m_MemoryCleaner.Tick();
            m_ActorRunner.Tick();
        }

        public void Shutdown()
        {
            m_ActorRunner.Shutdown();
            m_MemoryCleaner.Shutdown();
        }

        public struct Proxy
        {
            SystemContainer m_Self;

            public Proxy(SystemContainer self)
            {
                m_Self = self;
            }

            public MemoryCleaner.Proxy MemoryCleaner => m_Self.m_MemoryCleanerProxy;
            public ActorRunner.Proxy ActorRunner => m_Self.m_ActorRunnerProxy;
        }
    }
}
