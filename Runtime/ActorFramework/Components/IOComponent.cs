using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Unity.Reflect.ActorFramework
{
    public class IOComponent : IAsyncComponent
    {
        CancellationToken m_GlobalToken;

        object m_JobLock = new object();
        readonly Queue<HiddenContext> m_WaitingJobs = new Queue<HiddenContext>();
        readonly HashSet<HiddenContext> m_ActiveJobs = new HashSet<HiddenContext>();
        readonly ConcurrentQueue<HiddenContext> m_CompletedJobs = new ConcurrentQueue<HiddenContext>();
        
        TaskFactory m_TaskFactory = new TaskFactory();
        MpscSynchronizer m_Synchronizer = new MpscSynchronizer();

        public IOComponent(CancellationToken globalToken)
        {
            NbConcurrentTasks = SystemInfo.processorCount;
            m_GlobalToken = globalToken;
        }

        public int NbConcurrentTasks { get; set; }

        public async Task<WaitResult> WaitAsync(CancellationToken token)
        {
            try
            {
                await m_Synchronizer.WaitAsync(token).ConfigureAwait(false);
            }
            catch
            {
                lock (m_JobLock)
                {
                    ClearWaitingJobs();
                    if (m_ActiveJobs.Count == 0)
                        return WaitResult.Completed;
                }

                await m_Synchronizer.WaitAsync(default).ConfigureAwait(false);
            }

            return WaitResult.Continuing;
        }

        public TickResult Tick(TimeSpan endTime)
        {
            bool enoughTime;
            while ((enoughTime = EnoughTimeRemaining(endTime)) && m_CompletedJobs.TryDequeue(out var ctx))
            {
                lock (m_JobLock)
                {
                    if (m_WaitingJobs.Count > 0 && m_ActiveJobs.Count < NbConcurrentTasks)
                    {
                        var jobCtx = m_WaitingJobs.Dequeue();
                        m_ActiveJobs.Add(jobCtx);
                        StartTask(jobCtx);
                    }
                }

                try
                {
                    if (ctx.Exception == null)
                    {
                        try
                        {
                            ctx.SuccessAction(ctx.State, ctx.Context, ctx.UserContext, ctx.Result);
                        }
                        catch (Exception ex)
                        {
                            ctx.Exception = new Exception("Success callback failed", ex);
                            ctx.FailureAction(ctx.State, ctx.Context, ctx.UserContext, ctx.Exception);
                        }
                    }
                    else
                        ctx.FailureAction(ctx.State, ctx.Context, ctx.UserContext, ctx.Exception);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }

            return enoughTime ? TickResult.Wait : TickResult.Yield;
        }

        public void StartJob<TState, TContext, TUserContext, TResult>(
            TState state,
            TContext context,
            TUserContext userContext,
            Func<TState, TContext, TUserContext, CancellationToken, Task<TResult>> func,
            Action<TState, TContext, TUserContext, TResult> success,
            Action<TState, TContext, TUserContext, Exception> failure)
            where TState : class
            where TContext : class
            where TUserContext : class
            where TResult : class
        {
            var jobCtx = new HiddenContext
            {
                Component = this,
                State = state,
                Context = context,
                UserContext = userContext,
                SuccessAction = Unsafe.As<Action<object, object, object, object>>(success),
                FailureAction = Unsafe.As<Action<object, object, object, Exception>>(failure)
            };

            jobCtx.JobWrapper = async ctx =>
            {
                try
                {
                    var f = Unsafe.As<Func<object, object, object, CancellationToken, Task<object>>>(func);
                    ctx.Result = await f(ctx.State, ctx.Context, ctx.UserContext, m_GlobalToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    ctx.Exception = ex;
                }

                lock (ctx.Component.m_JobLock)
                    ctx.Component.m_ActiveJobs.Remove(jobCtx);

                ctx.Component.m_CompletedJobs.Enqueue(jobCtx);
                ctx.Component.m_Synchronizer.Set();
            };

            lock (m_JobLock)
            {
                if (m_GlobalToken.IsCancellationRequested)
                {
                    jobCtx.FailureAction(state, context, userContext, new OperationCanceledException());
                    return;
                }

                if (m_ActiveJobs.Count >= NbConcurrentTasks)
                    m_WaitingJobs.Enqueue(jobCtx);
                else
                {
                    m_ActiveJobs.Add(jobCtx);
                    StartTask(jobCtx);
                }
            }
        }

        void ClearWaitingJobs()
        {
            foreach (var waiting in m_WaitingJobs)
            {
                waiting.Exception = new OperationCanceledException();
                m_CompletedJobs.Enqueue(waiting);
            }

            m_WaitingJobs.Clear();
        }

        void StartTask(HiddenContext jobCtx)
        {
            Action<object> action = async ctx =>
            {
                var jobCtx = (HiddenContext)ctx;
                await jobCtx.JobWrapper(jobCtx).ConfigureAwait(false);
            };

            m_TaskFactory.StartNew(action, jobCtx, TaskCreationOptions.RunContinuationsAsynchronously);
        }

        static bool EnoughTimeRemaining(TimeSpan endTime)
        {
            var remaining = endTime - TimeSpan.FromTicks(Stopwatch.GetTimestamp());
            return remaining > TimeSpan.FromMilliseconds(1);
        }
        
        class HiddenContext
        {
            public IOComponent Component;
            public object State;
            public object Context;
            public object UserContext;
            public Func<object, object, object, Task<object>> Func;
            public Exception Exception;
            public object Result;
            
            public Func<HiddenContext, Task> JobWrapper;
            public Action<object, object, object, object> SuccessAction = (self, ctx, userCtx, result) => { };
            public Action<object, object, object, Exception> FailureAction = (self, ctx, userCtx, ex) => { };
        }
    }
}
