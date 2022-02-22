using System;
using System.Diagnostics;
using System.Threading;
using Unity.Reflect.ActorFramework;
using Unity.Reflect.Actors;
using UnityEngine;
using UnityEngine.Reflect;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect
{
    /// <summary>
    ///     Wrap the native <see cref="ActorSystem"/>. This is used as an endpoint to normalize
    ///     the access to the actors for external code (non-actor code). It is also the entry-point
    ///     to get execution time on main thread for actors that have this requirement.
    /// </summary>
    public class ActorRunner
    {
        Scheduler m_Scheduler;
        ActorSystem m_System;
        BridgeActor m_Bridge;

        ActorSystemSetup m_Asset;
        Project m_Project;
        IExposedPropertyTable m_Resolver;
        UnityUser m_User;
        AccessToken m_AccessToken;
        Action<BridgeActor.Proxy> m_SettingsOverrideAction;
        Action<Proxy> m_PostInstantiationAction;

        bool m_IsRunning;

        public ActorRunner()
        {
            m_System = new ActorSystem(new Scheduler(SystemInfo.processorCount));
        }

        public void Shutdown()
        {
            StopActorSystem();
            m_System.Shutdown();
        }

        public void Tick()
        {
            if (!m_IsRunning)
                return;

            var startTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            m_Scheduler.Tick(startTime, startTime + TimeSpan.FromMilliseconds(12), 0);
        }

        void Instantiate(ActorSystemSetup asset, Project project, IExposedPropertyTable resolver, UnityUser user, AccessToken accessToken, Action<BridgeActor.Proxy> settingsOverrideAction, Action<Proxy> postInstantiationAction)
        {
            m_Asset = asset;
            m_Project = project;
            m_Resolver = resolver;
            m_User = user;
            m_AccessToken = accessToken;
            m_SettingsOverrideAction = settingsOverrideAction;
            m_PostInstantiationAction = postInstantiationAction;

            StopActorSystem();
            m_System.Shutdown();

            // Do a copy so the asset in editor is not affected
            asset = UnityEngine.Object.Instantiate(asset);

            m_Scheduler = new Scheduler(SystemInfo.processorCount);
            m_Scheduler.SetPeriodicTickingThread(0);
            m_System = new ActorSystem(m_Scheduler);

            ActorSystemSetupAnalyzer.InitializeAnalyzer(asset);
            ActorSystemSetupAnalyzer.PrepareInPlace(asset);
            ActorSystemSetupAnalyzer.MigrateInPlace(asset);
            ActorSystemSetupAnalyzer.Instantiate(m_System, asset, resolver, project, user, accessToken, UnityEngine.Reflect.ProjectServer.Client, m_Scheduler, settingsOverrideAction);

            m_System.TryGetActorState(out m_Bridge);
            m_Bridge?.SetActorRunner(new Proxy(this));

            postInstantiationAction(new Proxy(this));
        }

        void Restart()
        {
            Instantiate(m_Asset, m_Project, m_Resolver, m_User, m_AccessToken, m_SettingsOverrideAction, m_PostInstantiationAction);
        }

        void StartActorSystem()
        {
            m_System.Start();
            m_IsRunning = true;
        }

        void StopActorSystem()
        {
            if (!m_IsRunning)
                return;
            
            m_System.PreStop();

            // May be null in samples
            if (m_Bridge != null)
            {
                var isPreShutdownCompleted = false;
                new BridgeActor.Proxy(m_Bridge).UnsubscribeAll();
                new BridgeActor.Proxy(m_Bridge).PreShutdown(() => Volatile.Write(ref isPreShutdownCompleted, true));

                // Freezes main thread until PreShutdown is completed
                var watch = Stopwatch.StartNew();
                while (!Volatile.Read(ref isPreShutdownCompleted))
                {
                    // Temporary fix until we integrate natively the pre-shutdown phase
                    if (watch.ElapsedMilliseconds > 10000)
                    {
                        Debug.LogError($"{nameof(PreShutdown)} phase timeout.");
                        break;
                    }
                    
                    var startTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
                    m_Scheduler.Tick(startTime, startTime + TimeSpan.FromMilliseconds(100), 0);
                }
            }
            
            m_System.Stop();
            m_IsRunning = false;
        }

        void TickUntil<T>(ConditionCapture<T> cc, Func<ConditionCapture<T>, bool> conditionAction)
        {
            while (!conditionAction(cc))
            {
                Tick();
                Thread.Sleep(1);
            }
        }

        public struct Proxy
        {
            ActorRunner m_Self;

            public Proxy(ActorRunner self)
            {
                m_Self = self;
            }

            public BridgeActor.Proxy Bridge => new BridgeActor.Proxy(m_Self.m_Bridge);
            public IPlayerClient PlayerClient => m_Self.m_System.PlayerClient; 
            public void Instantiate(ActorSystemSetup asset, Project project, IExposedPropertyTable resolver, UnityUser user, AccessToken accessToken, Action<BridgeActor.Proxy> settingsOverrideAction, Action<Proxy> postInstantiationAction) 
                => m_Self.Instantiate(asset, project, resolver, user, accessToken, settingsOverrideAction, postInstantiationAction);
            public void Restart() => m_Self.Restart();
            public void StartActorSystem() => m_Self.StartActorSystem();
            public void StopActorSystem() => m_Self.StopActorSystem();
            public void ProcessUntil<T>(ConditionCapture<T> cc, Func<ConditionCapture<T>, bool> conditionAction) => m_Self.TickUntil(cc, conditionAction);
            public TActor GetActor<TActor>() where TActor : class => m_Self.m_System.GetActorState<TActor>();
            public bool TryGetActor<TActor>(out TActor actor) where TActor : class => m_Self.m_System.TryGetActorState(out actor);
            public ActorHandle GetActorHandle<TActor>() where TActor : class => m_Self.m_System.GetActorHandle<TActor>();
            public bool TryGetActorHandle<TActor>(out ActorHandle handle) where TActor : class => m_Self.m_System.TryGetActorHandle<TActor>(out handle);
        }
    }

    public class ConditionCapture<T>
    {
        public T Data;

        public ConditionCapture(T data)
        {
            Data = data;
        }
    }
}
