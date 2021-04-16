using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Reflect.Unity.Actor;
using UnityEngine;

namespace Unity.Reflect.Actor
{
    public static class ActorUtils
    {
        public static Task StartTaskForAsyncComponents(Scheduler scheduler, ActorRef actorRef, IAsyncComponent[] asyncComponents, CancellationToken token)
        {
            if (asyncComponents.Length == 0)
                return Task.CompletedTask;

            var task = Task.Run(async () =>
            {
                var tasks = new List<Task>(Enumerable.Repeat(Task.CompletedTask, asyncComponents.Length));

                while (!token.IsCancellationRequested)
                {
                    var isWaitingForCallback = false;
                    for (var i = 0; i < tasks.Count; ++i)
                    {
                        var task = tasks[i];
                        if (task.IsCompleted)
                        {
                            isWaitingForCallback = true;
                            var component = asyncComponents[i];
                            tasks[i] = component.WaitAsync(token);
                        }
                    }

                    if (isWaitingForCallback)
                        scheduler.AwakeActor(actorRef);
                    
                    await Task.WhenAny(tasks);
                }
            }, token);

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
