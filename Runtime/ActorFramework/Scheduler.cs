using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Unity.Reflect.Collections;

namespace Unity.Reflect.ActorFramework
{
    public class Scheduler
    {
        object m_ExternalCallLock = new object();
        const bool k_IsLoggingEnabled = false;

        int m_LastAddedIndex;
        ExecutionGroup m_FakeExecutionGroup;
        ExecutionGroup[] m_ExecutionGroups;
        Dictionary<ActorHandle, ActorState> m_Actors = new Dictionary<ActorHandle, ActorState>();

        public Scheduler(int nbLogicalCores)
        {
            // Add +1 for fake ExecutionGroup to send the Add/Remove one at a time, but from any thread.
            m_ExecutionGroups = new ExecutionGroup[nbLogicalCores + 1];
            for (var i = 0; i < nbLogicalCores + 1; ++i)
                m_ExecutionGroups[i] = new ExecutionGroup(i, nbLogicalCores + 1, 0.1f, m_ExecutionGroups, k_IsLoggingEnabled);

            // Set it as cooperative, it will never execute.
            m_FakeExecutionGroup = m_ExecutionGroups[nbLogicalCores];
            m_FakeExecutionGroup.IsCooperative = true;
        }

        public void Shutdown()
        {
            if (m_ExecutionGroups != null)
            {
                foreach (var group in m_ExecutionGroups)
                    group?.Dispose();
            }
            m_FakeExecutionGroup?.Dispose();
        }

        /// <summary>
        ///     Set a thread (e.g. unity thread) to a specific <see cref="ExecutionGroup"/>.
        ///     When set, the caller must periodically call <see cref="Tick"/> function with the same
        ///     <see cref="groupIndex"/>, else the <see cref="ExecutionGroup"/> will never execute.
        /// </summary>
        /// <param name="groupIndex"></param>
        public void SetPeriodicTickingThread(int groupIndex)
        {
            m_ExecutionGroups[groupIndex].IsCooperative = true;
        }

        public void Start()
        {
            foreach(var group in m_ExecutionGroups)
                group.Start();
        }

        public void Stop()
        {
            foreach(var group in m_ExecutionGroups)
                group.Stop();
        }

        public void Tick(TimeSpan startTime, TimeSpan endTime, int groupIndex)
        {
            m_ExecutionGroups[groupIndex].CooperativeTick(startTime, endTime);
        }

        public void WakeUpActor(ActorHandle handle)
        {
            // Remove possible race condition with Add/Remove
            var actors = m_Actors;

            if (!actors.TryGetValue(handle, out var actorState))
                return;

            actorState.IsReady = true;
            actorState.ExecutionGroup.Signal();
        }

        /// <summary>
        ///     Add an actor in the scheduler. This will not set the actor to the specified
        ///     group if the actor is already in the scheduler or has not been signaled as
        ///     removed by the <see cref="ExecutionGroup"/> that was executing it.
        /// </summary>
        /// <param name="actor"></param>
        /// <param name="groupIndex"></param>
        public void Add(Actor<object> actor, int groupIndex = -1)
        {
            lock (m_ExternalCallLock)
            {
                var actors = new Dictionary<ActorHandle, ActorState>(m_Actors);
                m_FakeExecutionGroup.FakeExecute(new FakeCallback(this));

                if (actors.ContainsKey(actor.Handle))
                    return;
                
                if (groupIndex == -1)
                    groupIndex = GetNextNonCooperativeGroupIndex();
                if (groupIndex == -1)
                    groupIndex = 0;

                // Assign all to same thread and looks to the stealing result
                //if (groupIndex != 0)
                //    groupIndex = 1;

                var actorState = new ActorState(m_ExecutionGroups[groupIndex], actor, true);
                actors.Add(actor.Handle, actorState);
                actorState.ExecutionGroup.SendAddActor(m_FakeExecutionGroup, actorState);

                m_Actors = actors;
            }
        }

        public void Remove(Actor<object> actor)
        {
            lock (m_ExternalCallLock)
            {
                if (!m_Actors.ContainsKey(actor.Handle))
                    return;
                
                var actorState = m_Actors[actor.Handle];
                actorState.ExecutionGroup.SendRemoveActor(m_FakeExecutionGroup, actorState);
            }
        }

        int GetNextNonCooperativeGroupIndex()
        {
            // -1 to ignore the fake execution group
            for (var i = 0; i < m_ExecutionGroups.Length - 1; ++i)
            {
                var index = m_LastAddedIndex++ % (m_ExecutionGroups.Length - 1);
                if (!m_ExecutionGroups[index].IsCooperative)
                    return index;
            }

            return -1;
        }

        struct FakeCallback : ExecutionGroup.IFakeCallback
        {
            public Scheduler Scheduler;

            public FakeCallback(Scheduler scheduler)
            {
                Scheduler = scheduler;
            }

            public void Callback(ExecutionGroup.ThreadMessage msg)
            {
                if (msg.Type == ExecutionGroup.MessageType.RemoveActorResponse)
                {
                    var actors = new Dictionary<ActorHandle, ActorState>(Scheduler.m_Actors);
                    actors.Remove(msg.ActorState.Instance.Handle);
                    Scheduler.m_Actors = actors;
                }
            }
        }

        class ActorState
        {
            public volatile ExecutionGroup ExecutionGroup;
            public volatile Actor<object> Instance;
            public volatile bool IsReady;
            public long NbExecutions;
            public long NbCycles;

            public ActorState(ExecutionGroup executionGroup, Actor<object> instance, bool isReady)
            {
                ExecutionGroup = executionGroup;
                Instance = instance;
                IsReady = isReady;
            }
        }

        class ExecutionGroup : IDisposable
        {
            const int k_SampleSize = 16;
            const float k_MinRemoteLoadToSteal = 0.90f;
            const float k_MaxLocalWorstLoadToSteal = 0.80f;
            const int k_PercentileSampleSize = k_SampleSize / 4;
            static readonly TimeSpan k_CycleTime = TimeSpan.FromMilliseconds(10);

            static readonly ConcurrentDictionary<Type, TimeSpan> k_ActorTimes = new ConcurrentDictionary<Type, TimeSpan>();

            Thread m_Thread;
            int m_Index;
            float m_CycleSleepRatio;
            int m_NextActorIndex;
            long m_NbCyclesSinceLastSendStolenActor;
            long m_NbCycleSinceLastStealingRequest;

            volatile bool m_IsStopping;
            volatile float m_LoadFactor;
            int m_LastLoadFactorIndex;
            float[] m_LastLoadFactors = new float[k_SampleSize];
            float[] m_LastPercentileLoadFactors = new float[k_PercentileSampleSize];
            Random m_Rand = new Random();
            AutoResetEvent m_Synchronizer = new AutoResetEvent(true);
            AutoResetEvent m_IsStoppedSynchronizer = new AutoResetEvent(false);

            List<ActorState> m_ActorStates = new List<ActorState>();

            SwsrQueue<ThreadMessage>[] m_IncomingMessages;
            volatile bool m_HasPendingIncomingMessages;

            bool m_HasPendingStealingRequest;
            bool m_StatsLoggingEnabled;

            ExecutionGroup[] m_ExecutionGroups;

            public bool IsCooperative { get; set; }
            public float LoadFactor => m_LoadFactor;

            public ExecutionGroup(int index, int nbExecutionGroups, float cycleSleepRatio, ExecutionGroup[] executionGroups, bool statsLoggingEnabled)
            {
                m_Index = index;
                m_CycleSleepRatio = cycleSleepRatio;
                m_IncomingMessages = new SwsrQueue<ThreadMessage>[nbExecutionGroups];
                for (var i = 0; i < nbExecutionGroups; ++i)
                    m_IncomingMessages[i] = new SwsrQueue<ThreadMessage>();

                m_ExecutionGroups = executionGroups;
                m_StatsLoggingEnabled = statsLoggingEnabled;
            }

            public void Start()
            {
                if (IsCooperative)
                    return;

                while (m_IsStopping) { }

                m_Thread = new Thread(() => Run());
                m_Thread.Start();
            }

            public void Stop()
            {
                if (IsCooperative)
                    return;

                m_IsStopping = true;
                m_Synchronizer.Set();
                m_IsStoppedSynchronizer.WaitOne();
            }

            public void Dispose()
            {
                m_Synchronizer?.Dispose();
                m_IsStoppedSynchronizer?.Dispose();

                if (m_StatsLoggingEnabled && m_Index == 0)
                {
                    using (var log = File.CreateText("actor_times.txt"))
                    {
                        var hasJumped = false;
                        if (k_ActorTimes.Count > 0)
                            log.Write("Main thread actors:\n");

                        foreach (var kv in k_ActorTimes.OrderBy(x => x.Key.GetCustomAttribute<ActorAttribute>().IsBoundToMainThread ? 0 : 1).ThenByDescending(x => x.Value))
                        {
                            if (!hasJumped && !kv.Key.GetCustomAttribute<ActorAttribute>().IsBoundToMainThread)
                            {
                                hasJumped = true;
                                log.Write("\nBackground thread actors:\n");
                            }
                            log.Write($"{kv.Key.Name}:\t{kv.Value.TotalMilliseconds}\tms\n");
                        }
                    }
                }
            }

            public void CooperativeTick(TimeSpan startTime, TimeSpan endTime)
            {
                ExecuteWorkStealing();
                ProcessInterThreadIncomingMessages();

                if (m_StatsLoggingEnabled)
                    ExecuteActors<EnabledActorTimingPolicy>(startTime, endTime);
                else
                    ExecuteActors<NoPolicy>(startTime, endTime);
            }

            void ExecuteActors<TPolicy>(TimeSpan startTime, TimeSpan endTime)
                where TPolicy : struct, IStatsPolicy
            {
                var policy = new TPolicy();
                var isEverythingProcessed = true;
                var count = m_ActorStates.Count;
                for (var i = 0; i < count; ++i)
                {
                    m_NextActorIndex = ++m_NextActorIndex % m_ActorStates.Count;
                    var actorState = m_ActorStates[m_NextActorIndex];

                    if (!actorState.IsReady)
                        continue;

                    actorState.IsReady = false;
                    actorState.NbExecutions++;

                    long t1 = 0;
                    if (policy.IsActorTimingEnabled())
                        t1 = Stopwatch.GetTimestamp();

                    var res = actorState.Instance.Lifecycle.Tick(actorState.Instance.State, endTime);

                    if (policy.IsActorTimingEnabled())
                    {
                        var t2 = Stopwatch.GetTimestamp();
                        k_ActorTimes.TryGetValue(actorState.Instance.Handle.Type, out var time);
                        k_ActorTimes[actorState.Instance.Handle.Type] = time + TimeSpan.FromTicks(t2 - t1);
                    }
                    
                    if (res == TickResult.Yield)
                    {
                        actorState.IsReady = true;
                        isEverythingProcessed = false;
                        // Give more time to this actor on the next cycle as it was not the first one to execute during this cycle.
                        // This guarantee that each actor has at least an entire cycle every N cycles, where N is the number of actors
                        // running in this ExecutionGroup
                        if (i != 0)
                        {
                            --m_NextActorIndex;
                            if (m_NextActorIndex == -1)
                                m_NextActorIndex = m_ActorStates.Count - 1;
                        }
                        break;
                    }
                }

                ++m_NbCyclesSinceLastSendStolenActor;
                ++m_NbCycleSinceLastStealingRequest;
                
                // Make sure to increment the cycle count for all actors
                for (var i = 0; i < count; ++i)
                {
                    var index = m_NextActorIndex++ % m_ActorStates.Count;
                    var actorState = m_ActorStates[index];
                    ++actorState.NbCycles;
                }

                // If some actors didn't have the time to complete their processing (endTime reached),
                // make sure the thread continue to execute later
                var loadFactorIndex = m_LastLoadFactorIndex++ % k_SampleSize;
                if (!isEverythingProcessed)
                {
                    m_LastLoadFactors[loadFactorIndex] = 1.0f;
                    m_Synchronizer.Set();
                }
                else
                {
                    var realEndTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
                    m_LastLoadFactors[loadFactorIndex] = (realEndTime - startTime).Ticks / (float)(endTime - startTime).Ticks;
                }

                m_LoadFactor = GetPercentileLoadFactor();
            }

            float GetPercentileLoadFactor()
            {
                Array.Clear(m_LastPercentileLoadFactors, 0, k_PercentileSampleSize);

                for (var i = 0; i < k_SampleSize; ++i)
                {
                    var val = m_LastLoadFactors[i];
                    for (var j = 0; j < k_PercentileSampleSize; ++j)
                    {
                        var curVal = m_LastPercentileLoadFactors[j];
                        if (val > curVal)
                        {
                            m_LastPercentileLoadFactors[j] = val;
                            break;
                        }
                    }
                }
                
                var sum = 0.0f;
                for(var i = 0; i < k_PercentileSampleSize; ++i)
                    sum += m_LastPercentileLoadFactors[i];

                return sum / k_PercentileSampleSize;
            }

            void ProcessInterThreadIncomingMessages()
            {
                if (!m_HasPendingIncomingMessages)
                    return;

                m_HasPendingIncomingMessages = false;
                for (var i = 0; i < m_IncomingMessages.Length; ++i)
                {
                    var messages = m_IncomingMessages[i];
                    while (messages.TryDequeue(out var msg))
                        ProcessInterThreadMessage(msg);
                }
            }

            void ProcessInterThreadMessage(ThreadMessage msg)
            {
                switch (msg.Type)
                {
                    case MessageType.AddActor:
                        // Only comes from Scheduler, nothing to answer
                        m_ActorStates.Add(msg.ActorState);
                        msg.ActorState.NbExecutions = 0;
                        msg.ActorState.NbCycles = 0;
                        break;
                    case MessageType.RemoveActor:
                        // Only comes from Scheduler, no need to signal the source
                        // as it will be fetched when Add is called again in Scheduler
                        m_ActorStates.Remove(msg.ActorState);
                        msg.Source.SendRemoveActorResponse(this, msg.ActorState);
                        break;
                    case MessageType.StealActor:
                        FindAndSendStolenActor(msg);
                        break;
                    case MessageType.StealActorResponse:
                        m_HasPendingStealingRequest = false;
                        if (msg.ActorState != null)
                        {
                            m_ActorStates.Add(msg.ActorState);
                            msg.ActorState.ExecutionGroup = this;
                            msg.ActorState.IsReady = true;
                            msg.ActorState.NbExecutions = 0;
                            msg.ActorState.NbCycles = 0;
                        }
                        break;
                    case MessageType.RemoveActorResponse:
                        // Should never happen, only FakeExecutionGroup will receive it and it's running in
                        // cooperative mode without ticking being called
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            void ExecuteWorkStealing()
            {
                if (IsCooperative ||
                    m_HasPendingStealingRequest ||
                    m_NbCycleSinceLastStealingRequest < 3 ||
                    m_LastLoadFactors.Max() > k_MaxLocalWorstLoadToSteal)
                    return;

                var executionGroup = FindStealingTarget();
                if (executionGroup == null)
                    return;

                m_HasPendingStealingRequest = true;
                m_NbCycleSinceLastStealingRequest = 0;
                executionGroup.SendStealActor(this);
            }

            ExecutionGroup FindStealingTarget()
            {
                var startIndex = m_Rand.Next(0, m_ExecutionGroups.Length - 1);
                
                ExecutionGroup executionGroup = null;
                for (var i = 0; i < m_ExecutionGroups.Length; ++i)
                {
                    var index = startIndex++ % m_ExecutionGroups.Length;
                    var group = m_ExecutionGroups[index];

                    // Cannot steal from cooperative execution group for now.
                    // This will guarantee that main thread actors stay on the main thread.
                    if (!group.IsCooperative &&
                        group.LoadFactor > k_MinRemoteLoadToSteal &&
                        group.m_ActorStates.Count > 1)
                    {
                        executionGroup = group;
                        break;
                    }
                }

                // May return null
                return executionGroup;
            }

            void FindAndSendStolenActor(ThreadMessage msg)
            {
                if (m_NbCyclesSinceLastSendStolenActor < k_SampleSize || m_ActorStates.Count < 2)
                {
                    msg.Source.SendStealActorResponse(this, null);
                    return;
                }

                ActorState bestMatch = null;
                var smallestRatio = float.MaxValue;
                var bestMatchIndex = 0;

                for (var i = 0; i < m_ActorStates.Count; ++i)
                {
                    var actor = m_ActorStates[i];

                    // Not enough data to know if this actor is heavy or lightweight
                    if (actor.NbCycles < k_SampleSize * 2)
                        continue;

                    var ratio = actor.NbExecutions / (float)actor.NbCycles;
                    if (ratio < smallestRatio)
                    {
                        bestMatch = actor;
                        bestMatchIndex = i;
                        smallestRatio = ratio;
                    }
                }

                if (bestMatch == null)
                {
                    msg.Source.SendStealActorResponse(this, null);
                    return;
                }
                
                m_NbCyclesSinceLastSendStolenActor = 0;
                m_ActorStates.RemoveAt(bestMatchIndex);
                msg.Source.SendStealActorResponse(this, bestMatch);
            }

            public void Signal()
            {
                m_Synchronizer.Set();
            }

            public void FakeExecute<T>(T callback)
                where T : struct, IFakeCallback
            {
                if (m_HasPendingIncomingMessages)
                {
                    m_HasPendingIncomingMessages = false;
                    for (var i = 0; i < m_IncomingMessages.Length; ++i)
                    {
                        var messages = m_IncomingMessages[i];
                        while (messages.TryDequeue(out var msg))
                            callback.Callback(msg);
                    }
                }
            }

            public void SendAddActor(ExecutionGroup source, ActorState actorState)
            {
                m_IncomingMessages[source.m_Index].TryEnqueue(CreateAddActorMessage(source, actorState));
                m_HasPendingIncomingMessages = true;
                Signal();
            }

            public void SendRemoveActor(ExecutionGroup source, ActorState actorState)
            {
                m_IncomingMessages[source.m_Index].TryEnqueue(CreateRemoveActorMessage(source, actorState));
                m_HasPendingIncomingMessages = true;
                Signal();
            }

            void SendRemoveActorResponse(ExecutionGroup source, ActorState actorState)
            {
                m_IncomingMessages[source.m_Index].TryEnqueue(CreateRemoveActorResponseMessage(source, actorState));
                m_HasPendingIncomingMessages = true;
                // No need to Signal(), it's processed when the client Add() a new actor to the scheduler
            }

            void SendStealActor(ExecutionGroup source)
            {
                m_IncomingMessages[source.m_Index].TryEnqueue(CreateStealActorMessage(source));
                m_HasPendingIncomingMessages = true;
                Signal();
            }

            void SendStealActorResponse(ExecutionGroup source, ActorState actorState)
            {
                m_IncomingMessages[source.m_Index].TryEnqueue(CreateStealActorResponseMessage(source, actorState));
                m_HasPendingIncomingMessages = true;
                Signal();
            }

            void Tick()
            {
                var cycleAwakeTime = TimeSpan.FromTicks((long)(k_CycleTime.Ticks * (1.0f - m_CycleSleepRatio)));
                var startTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());
                var endTime = startTime + cycleAwakeTime;
                
                CooperativeTick(startTime, endTime);
                var realEndTime = TimeSpan.FromTicks(Stopwatch.GetTimestamp());

                if (realEndTime > endTime)
                    Thread.Sleep(k_CycleTime - cycleAwakeTime);
                else Thread.Sleep(k_CycleTime - (realEndTime - startTime));

                // 100ms to make sure a thread tries to steal actor periodically if there is no actor running on this group
                m_Synchronizer.WaitOne(100);
            }

            void Run()
            {
                while (!m_IsStopping)
                    Tick();

                m_IsStopping = false;
                m_IsStoppedSynchronizer.Set();
            }

            static ThreadMessage CreateAddActorMessage(ExecutionGroup source, ActorState actorState)
            {
                return new ThreadMessage(MessageType.AddActor, source, actorState);
            }

            static ThreadMessage CreateRemoveActorMessage(ExecutionGroup source, ActorState actorState)
            {
                return new ThreadMessage(MessageType.RemoveActor, source, actorState);
            }

            static ThreadMessage CreateStealActorMessage(ExecutionGroup source)
            {
                return new ThreadMessage(MessageType.StealActor, source, null);
            }

            static ThreadMessage CreateStealActorResponseMessage(ExecutionGroup source, ActorState actorState)
            {
                return new ThreadMessage(MessageType.StealActorResponse, source, actorState);
            }

            static ThreadMessage CreateRemoveActorResponseMessage(ExecutionGroup source, ActorState actorState)
            {
                return new ThreadMessage(MessageType.RemoveActorResponse, source, actorState);
            }

            public interface IFakeCallback
            {
                void Callback(ThreadMessage msg);
            }

            public enum MessageType
            {
                AddActor,
                RemoveActor,
                RemoveActorResponse,
                StealActor,
                StealActorResponse
            }

            public struct ThreadMessage
            {
                public MessageType Type;
                public ExecutionGroup Source;
                public ActorState ActorState;

                public ThreadMessage(MessageType type, ExecutionGroup source, ActorState actorState)
                {
                    Type = type;
                    Source = source;
                    ActorState = actorState;
                }
            }

            interface IStatsPolicy
            {
                public bool IsActorTimingEnabled();
            }

            struct EnabledActorTimingPolicy : IStatsPolicy
            {
                public bool IsActorTimingEnabled() => true;
            }

            struct NoPolicy : IStatsPolicy
            {
                public bool IsActorTimingEnabled() => false;
            }
        }
    }
}
