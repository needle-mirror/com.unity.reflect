using System;
using System.Diagnostics;
using Reflect.Unity.Actor;
using Unity.Reflect.Actor;
using Unity.Reflect.Streaming;
using UnityEngine;

namespace Unity.Reflect
{
    /// <summary>
    ///     Wrap the native <see cref="ActorSystem"/>. This is used as an endpoint to normalize
    ///     the access to the actors for external code (non-actor code). It is also the entry-point
    ///     to get execution time on main thread for actors that has this requirement.
    /// </summary>
    public class ActorRunner
    {
        Scheduler m_Scheduler;
        ActorSystem m_System;
        BridgeActor m_Bridge;

        bool m_IsRunning;

        public ActorRunner()
        {
            m_System = new ActorSystem(new Scheduler(SystemInfo.processorCount));
        }

        public void Shutdown()
        {
            m_System.Stop();
            m_System.Shutdown();
        }

        public void Tick()
        {
            if (!m_IsRunning)
                return;

            var startTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            m_Scheduler.Tick(startTime + TimeSpan.FromMilliseconds(8), default, 0);
        }

        void Instantiate(ActorSystemSetup asset, UnityProject project, IExposedPropertyTable resolver, UnityUser user)
        {
            StopActorSystem();
            m_System.Shutdown();

            // Do a copy so the asset in editor is not affected
            asset = UnityEngine.Object.Instantiate(asset);

            m_Scheduler = new Scheduler(SystemInfo.processorCount);
            m_Scheduler.SetPeriodicTickingThread(0);
            m_System = new ActorSystem(m_Scheduler);

            ActorSystemSetupAnalyzer.MigrateInPlace(asset);
            ActorSystemSetupAnalyzer.Instantiate(m_System, asset, resolver, project, user, UnityEngine.Reflect.ProjectServer.Client, m_Scheduler);

            m_Bridge = m_System.FindActorState<BridgeActor>();
            m_Bridge?.Initialize(asset);
        }

        void StartActorSystem()
        {
            m_System.Start();
            m_IsRunning = true;
        }

        void StopActorSystem()
        {
            m_System.Stop();
            m_IsRunning = false;
        }

        public struct Proxy
        {
            ActorRunner m_Self;

            public Proxy(ActorRunner self)
            {
                m_Self = self;
            }

            public BridgeActor.Proxy Bridge => new BridgeActor.Proxy(m_Self.m_Bridge);
            public void Instantiate(ActorSystemSetup asset, UnityProject project, IExposedPropertyTable resolver, UnityUser user) => m_Self.Instantiate(asset, project, resolver, user);
            public void StartActorSystem() => m_Self.StartActorSystem();
            public void StopActorSystem() => m_Self.StopActorSystem();
            public T FindActorState<T>() where T : class => m_Self.m_System.FindActorState<T>();
        }
    }
}
