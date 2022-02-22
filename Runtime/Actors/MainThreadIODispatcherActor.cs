using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Actor("ee4c4feb-a535-4f2c-84a8-b6b21a180850", true, true)]
    public class MainThreadIODispatcherActor
    {
        ActorHandle m_Self;
        Scheduler m_Scheduler;
        CancellationToken m_Token;
        CancellationTokenRegistration m_Registration;

        object m_Lock = new object();
        List<TaskCompletionSource<object>> m_RunningTasks = new List<TaskCompletionSource<object>>();
        HashSet<Action> m_WaitingActions = new HashSet<Action>();
        List<Action> m_Temp = new List<Action>();

        public MainThreadIODispatcherActor(ActorHandle self, Scheduler scheduler, CancellationToken token)
        {
            m_Self = self;
            m_Scheduler = scheduler;
            m_Token = token;
        }

        public void Initialize()
        {
            m_Registration = m_Token.Register(() =>
            {
                lock (m_Lock)
                {
                    foreach (var waiting in m_WaitingActions)
                        waiting();

                    m_WaitingActions.Clear();

                    foreach (var running in m_RunningTasks)
                        running.TrySetCanceled();

                    m_RunningTasks.Clear();
                }
            });
        }

        public void Shutdown()
        {
            m_Registration.Dispose();
        }

        public TickResult Tick(TimeSpan _)
        {
            lock (m_Lock)
            {
                m_Temp.AddRange(m_WaitingActions);
                m_WaitingActions.Clear();
            }

            foreach (var action in m_Temp)
                action();

            m_Temp.Clear();

            return TickResult.Wait;
        }

        public Task<T> Run<T>(Action<Action<T>> func)
            where T : class
        {
            m_Token.ThrowIfCancellationRequested();

            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

            lock (m_Lock)
            {
                m_WaitingActions.Add(() =>
                {
                    try
                    {
                        if (m_Token.IsCancellationRequested)
                        {
                            tcs.TrySetCanceled();
                            return;
                        }
                        
                        var objTcs = Unsafe.As<TaskCompletionSource<object>>(tcs);
                        lock (m_Lock)
                            m_RunningTasks.Add(objTcs);

                        func(res =>
                        {
                            lock (m_Lock)
                                m_RunningTasks.Remove(objTcs);
                            if (m_Token.IsCancellationRequested)
                                objTcs.TrySetCanceled();
                            else
                                objTcs.TrySetResult(res);
                        });
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                });
            }

            m_Scheduler.WakeUpActor(m_Self);

            return tcs.Task;
        }
    }
}
