using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Reflect.Unity.Actor;

namespace Unity.Reflect.Actor
{
    public class ActorSystem
    {
        bool m_IsRunning;
        CancellationTokenSource m_Cts;
        CancellationToken m_Token;
        Scheduler m_Scheduler;
        Dictionary<ActorRef, ActorState> m_Actors = new Dictionary<ActorRef, ActorState>();

        /// <summary>
        ///     Small hack to track components not referenced directly by actors so the GC does not collect them.
        ///     This will be removed in the future when a common base class can be inherited (ReflectActor) instead of IActor and IAsyncActor
        /// </summary>
        public Dictionary<ActorRef, Dictionary<Type, object>> RefToComponents = new Dictionary<ActorRef, Dictionary<Type, object>>();

        Dictionary<Type, object> m_Dependencies = new Dictionary<Type, object>();
        public Dictionary<Type, object> Dependencies
        {
            set
            {
                DisposeDependencies();
                m_Dependencies = value;
            }
        }

        public CancellationToken Token => m_Token;

        public ActorSystem(Scheduler scheduler)
        {
            m_Scheduler = scheduler;
        }

        /// <summary>
        ///     Find the first matching actor for type <see cref="TState"/> and return its internal state,
        ///     which is the class type of the actor or a custom type for deeply customized actor flow.
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <returns></returns>
        public TState FindActorState<TState>()
            where TState : class
        {
            return (TState)m_Actors.FirstOrDefault(x => x.Value.Actor.State.GetType() == typeof(TState)).Value?.Actor.State;
        }

        public void Shutdown()
        {
            Stop();

            foreach(var actorRef in m_Actors.Keys.ToList())
                Remove(actorRef);

            DisposeDependencies();
        }

        public void Start()
        {
            if (m_IsRunning)
                return;

            m_Cts = new CancellationTokenSource();
            m_Token = m_Cts.Token;

            m_Scheduler.Start(m_Token);

            foreach (var kv in m_Actors)
            {
                kv.Value.Actor.Lifecycle.Start(kv.Value.Actor.State);

                var components = RefToComponents[kv.Key].Values.ToList();

                var asyncComponents = components
                    .Where(x => x.GetType().GetInterfaces().Contains(typeof(IAsyncComponent)))
                    .Cast<IAsyncComponent>()
                    .ToArray();

                kv.Value.AsyncComponents = asyncComponents;

                kv.Value.Task = ActorUtils.StartTaskForAsyncComponents(m_Scheduler, kv.Key, asyncComponents, m_Token);
            }

            m_IsRunning = true;
        }

        public void Stop()
        {
            if (!m_IsRunning)
                return;

            m_Scheduler.Stop();

            foreach (var kv in m_Actors)
                kv.Value.Actor.Lifecycle.Stop(kv.Value.Actor.State);

            m_Cts.Cancel();
            try
            {
                Task.WaitAll(m_Actors.Select(x => x.Value.Task).ToArray());
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
            m_Cts.Dispose();
            m_IsRunning = false;
        }

        public void DisposeDependencies()
        {
            foreach (var kv in m_Dependencies)
            {
                if (kv.Value is IDisposable dependency)
                    dependency.Dispose();
            }
        }

        public void Add<TState>(Actor<TState> actor, Dictionary<Type, object> components)
            where TState : class
        {
            if (m_IsRunning)
                throw new NotSupportedException("Cannot add actor while the system is running.");

            var a = Unsafe.As<Actor<object>>(actor);

            a.Lifecycle.Initialize(a.State);
            
            if (actor.State.GetType().GetCustomAttribute<ActorAttribute>().IsBoundToMainThread)
                m_Scheduler.Add(a, 0);
            else
                m_Scheduler.Add(a);

            m_Actors.Add(actor.ActorRef, new ActorState{ Actor = a });

            RefToComponents[actor.ActorRef] = components;
        }
        
        public void Remove(ActorRef actorRef)
        {
            if (m_IsRunning)
                throw new NotSupportedException("Cannot remove actor while the system is running.");

            var actor = m_Actors[actorRef].Actor;

            m_Scheduler.Remove(actor);
            actor.Lifecycle.Shutdown(actor.State);
            m_Actors.Remove(actorRef);
            RefToComponents.Remove(actorRef);
        }

        class ActorState
        {
            public Actor<object> Actor;
            public Task Task;
            public IAsyncComponent[] AsyncComponents;
        }
    }
}
