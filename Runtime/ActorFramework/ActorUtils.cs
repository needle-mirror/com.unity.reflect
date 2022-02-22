using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace Unity.Reflect.ActorFramework
{
    public static class ActorUtils
    {
        public static Task StartTaskForAsyncComponents(Scheduler scheduler, ActorHandle handle, IAsyncComponent[] asyncComponents, CancellationToken token)
        {
            if (asyncComponents.Length == 0)
                return Task.CompletedTask;

            var task = Task.Run(async () =>
            {
                var tasks = new List<Task<WaitResult>>(Enumerable.Repeat(Task.FromResult(WaitResult.Continuing), asyncComponents.Length));

                while (true)
                {
                    var isWaitingForCallback = false;
                    for (var i = 0; i < tasks.Count; ++i)
                    {
                        var componentTask = tasks[i];
                        if (componentTask.IsCompleted)
                        {
                            WaitResult res;
                            try
                            {
                                res = componentTask.Result;
                            }
                            catch (AggregateException ex)
                            {
                                res = WaitResult.Completed;
                                if (ex.Flatten().InnerExceptions.Any(inner => !(inner is OperationCanceledException)))
                                    Debug.LogException(ex);
                            }
                            catch (Exception ex)
                            {
                                res = WaitResult.Completed;
                                Debug.LogException(ex);
                            }
                            
                            isWaitingForCallback = true;

                            if (res == WaitResult.Completed)
                            {
                                tasks.RemoveAt(i);
                                i--;
                                continue;
                            }

                            var component = asyncComponents[i];
                            tasks[i] = component.WaitAsync(token);
                        }
                    }

                    if (isWaitingForCallback)
                        scheduler.WakeUpActor(handle);

                    if (tasks.Count == 0)
                        break;

                    await Task.WhenAny(tasks).ConfigureAwait(false);
                }
            }, default);

            return task;
        }

        public static ActorSystemSetup CreateActorSystemSetup()
        {
            var actorSystemSetup = ScriptableObject.CreateInstance<ActorSystemSetup>();
            ActorSystemSetupAnalyzer.InitializeActorSystemSetup(actorSystemSetup);
            return actorSystemSetup;
        }
    }
}
