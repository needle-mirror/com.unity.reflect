using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.Reflect.Actor
{
    public class IOComponent : IAsyncComponent
    {
        readonly HashSet<HiddenContext> m_ActiveJobs = new HashSet<HiddenContext>();
        readonly ConcurrentQueue<HiddenContext> m_CompletedJobs = new ConcurrentQueue<HiddenContext>();
        
        TaskFactory m_TaskFactory = new TaskFactory();
        MpscSynchronizer m_Synchronizer = new MpscSynchronizer();

        public async Task WaitAsync(CancellationToken token)
        {
            await m_Synchronizer.WaitAsync(token);
        }

        public bool Tick(TimeSpan endTime, CancellationToken token)
        {
            while (m_CompletedJobs.TryDequeue(out var ctx))
            {
                try
                {
                    if (ctx.Exception == null)
                        ctx.SuccessAction(ctx.State, ctx.Context, ctx.UserContext, ctx.Result);
                    else
                        ctx.FailureAction(ctx.State, ctx.Context, ctx.UserContext, ctx.Exception);
                }
                catch (Exception ex)
                {
                    Debug.LogError(ex);
                }
            }

            return true;
        }

        public IOJob<TState, TContext, TUserContext, TResult> StartJob<TState, TContext, TUserContext, TResult>(TState state, TContext context, TUserContext userContext, Func<TState, TContext, TUserContext, Task<TResult>> func)
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
                UserContext = userContext
            };

            jobCtx.JobWrapper = async ctx =>
            {
                try
                {
                    var f = Unsafe.As<Func<object, object, object, Task<object>>>(func);
                    ctx.Result = await f(ctx.State, ctx.Context, ctx.UserContext);
                }
                catch (Exception ex)
                {
                    ctx.Exception = ex;
                }

                lock (ctx.Component.m_ActiveJobs)
                    ctx.Component.m_ActiveJobs.Remove(jobCtx);

                ctx.Component.m_CompletedJobs.Enqueue(jobCtx);
                ctx.Component.m_Synchronizer.Set();
            };
            
            lock (m_ActiveJobs)
                m_ActiveJobs.Add(Unsafe.As<HiddenContext>(jobCtx));
            
            Action<object> action = async ctx =>
            {
                var jobCtx = (HiddenContext)ctx;
                await jobCtx.JobWrapper(jobCtx);
            };
            m_TaskFactory.StartNew(action, jobCtx);

            return new IOJob<TState, TContext, TUserContext, TResult>(jobCtx);
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

        public struct IOJob<TState, TContext, TUserContext, TResult>
            where TState : class
            where TContext : class
            where TUserContext : class
            where TResult : class
        {
            HiddenContext m_Ctx;

            internal IOJob(object ctx)
            {
                m_Ctx = (HiddenContext)ctx;
            }

            public void Success(Action<TState, TContext, TUserContext, TResult> action)
            {
                m_Ctx.SuccessAction = Unsafe.As<Action<object, object, object, object>>(action);
            }

            public void Failure(Action<TState, TContext, TUserContext, Exception> action)
            {
                m_Ctx.FailureAction = Unsafe.As<Action<object, object, object, Exception>>(action);
            }
        }
    }
}
