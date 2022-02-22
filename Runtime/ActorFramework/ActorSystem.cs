using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.Reflect.ActorFramework
{
    public class ActorSystem
    {
        bool m_IsRunning;
        CancellationTokenSource m_Cts;
        CancellationToken m_Token;
        Scheduler m_Scheduler;
        Dictionary<ActorHandle, ActorWrapper> m_Actors = new Dictionary<ActorHandle, ActorWrapper>();

        public IPlayerClient PlayerClient { get; set; }
        

        /// <summary>
        ///     Small hack to track components not referenced directly by actors so the GC does not collect them.
        ///     This will be removed in the future when a common base class can be inherited (ReflectActor) instead of IActor and IAsyncActor
        /// </summary>
        public Dictionary<ActorHandle, Dictionary<Type, object>> RefToComponents = new Dictionary<ActorHandle, Dictionary<Type, object>>();

        Dictionary<Type, object> m_Dependencies = new Dictionary<Type, object>();
        public Dictionary<Type, object> Dependencies
        {
            set
            {
                DisposeDependencies();
                m_Dependencies = value;
            }
        }

        public CancellationToken Token
        {
            get
            {
                PrepareToken();
                return m_Token;
            }
        }

        public ActorSystem(Scheduler scheduler)
        {
            m_Scheduler = scheduler;
        }

        /// <summary>
        ///     Gets the first matching actor for type <see cref="TState"/> and returns its internal state.
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <returns>The state of the actor.</returns>
        /// <exception cref="KeyNotFoundException">No actor of type <see cref="TState"/> is currently instantiated in the actor system.</exception>
        public TState GetActorState<TState>()
            where TState : class
        {
            if (TryGetActorState<TState>(out var state))
                return state;

            throw new KeyNotFoundException($"{typeof(TState).Name}");
        }

        /// <summary>
        ///     Gets the first matching actor for type <see cref="TState"/> and returns its internal state.
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="state"></param>
        /// <returns></returns>
        public bool TryGetActorState<TState>(out TState state)
            where TState : class
        {
            var wrapper = m_Actors.FirstOrDefault(x => x.Value.Actor.State.GetType() == typeof(TState)).Value;
            state = (TState)wrapper?.Actor.State;
            return state != null;
        }

        /// <summary>
        ///     Gets the first matching actor handle for the actor state <see cref="TState"/>.
        /// </summary>
        /// <typeparam name="TState">The type of the actor state.</typeparam>
        /// <returns></returns>
        /// <exception cref="KeyNotFoundException">No actor of type <see cref="TState"/> is currently instantiated in the actor system.</exception>
        public ActorHandle GetActorHandle<TState>()
            where TState : class
        {
            if (TryGetActorHandle<TState>(out var handle))
                return handle;

            throw new KeyNotFoundException($"{typeof(TState).Name}");
        }

        /// <summary>
        ///     Gets the first matching actor handle for the actor state <see cref="TState"/>.
        /// </summary>
        /// <typeparam name="TState"></typeparam>
        /// <param name="handle"></param>
        /// <returns>True if there is a match; otherwise, false.</returns>
        public bool TryGetActorHandle<TState>(out ActorHandle handle)
            where TState : class
        {
            handle = m_Actors.FirstOrDefault(x => x.Value.Actor.State.GetType() == typeof(TState)).Key;
            return handle != null;
        }

        public void Shutdown()
        {
            Stop();

            foreach(var handle in m_Actors.Keys.ToList())
                Remove(handle);

            m_Scheduler.Shutdown();

            DisposeDependencies();
        }

        public void Start()
        {
            if (m_IsRunning)
                return;

            PrepareToken();

            m_Scheduler.Start();

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

        public void PreStop()
        {
            m_Cts.Cancel();
        }

        public void Stop()
        {
            if (!m_IsRunning)
                return;

            if (!m_Cts.IsCancellationRequested)
                m_Cts.Cancel();

            m_Scheduler.Stop();

            foreach (var kv in m_Actors)
                kv.Value.Actor.Lifecycle.Stop(kv.Value.Actor.State);

            try
            {
                if (!Task.WaitAll(m_Actors.Select(x => x.Value.Task).ToArray(), TimeSpan.FromSeconds(5)))
                {
                    var actorNames = string.Join(",", m_Actors.Where(x => !x.Value.Task.IsCompleted).Select(x => x.Key.Type.Name));
                    throw new TimeoutException($"Actors ({actorNames}) {nameof(IAsyncComponent)} components timed out");
                }
            }
            catch (AggregateException ex)
            {
                if (ex.Flatten().InnerExceptions.Any(inner => !(inner is OperationCanceledException)))
                    Debug.LogException(ex);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }

            DisposeToken();
            m_IsRunning = false;
        }

        public void DisposeDependencies()
        {
            foreach (var kv in m_Dependencies)
            {
                if (kv.Value is IDisposable dependency)
                    dependency.Dispose();
            }
            m_Dependencies.Clear();

            DisposeToken();
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

            m_Actors.Add(actor.Handle, new ActorWrapper{ Actor = a });

            RefToComponents[actor.Handle] = components;
        }
        
        public void Remove(ActorHandle handle)
        {
            if (m_IsRunning)
                throw new NotSupportedException("Cannot remove actor while the system is running.");

            var actor = m_Actors[handle].Actor;

            m_Scheduler.Remove(actor);
            try
            {
                actor.Lifecycle.Shutdown(actor.State);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
            m_Actors.Remove(handle);
            RefToComponents.Remove(handle);
        }

        void PrepareToken()
        {
            if (m_Cts == null)
            {
                m_Cts = new CancellationTokenSource();
                m_Token = m_Cts.Token;
            }
        }

        void DisposeToken()
        {
            m_Cts?.Dispose();
            m_Cts = null;
        }

        class ActorWrapper
        {
            public Actor<object> Actor;
            public Task Task;
            public IAsyncComponent[] AsyncComponents;
        }
    }
}
