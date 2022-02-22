using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.Reflect.ActorFramework
{
    /// <summary>
    ///     The result of a Tick method.
    /// </summary>
    public enum TickResult
    {
        /// <summary>
        ///     The component still has work to do, but is cooperatively yielding the CPU.
        ///     This implicitly asks to be re-executed as soon as possible.
        /// </summary>
        Yield,

        /// <summary>
        ///     The component has completed all its work.
        ///     Indicates to the scheduler that this component doesn't need to re-execute until something wakes the actor up.
        /// </summary>
        Wait
    }

    /// <summary>
    ///     Interface to implement by a component to be able to run when its actor ticks.
    /// </summary>
    public interface IRunnableComponent
    {
        TickResult Tick(TimeSpan endTime);
    }

    /// <summary>
    ///     Interface to implement by a component to be able to run a C# async task.
    /// </summary>
    public interface IAsyncComponent : IRunnableComponent
    {
        /// <summary>
        ///     Method called by the actor system in loop. When the task completes, this method
        ///     is immediately re-executed, and the component's <see cref="IRunnableComponent.Tick"/>
        ///     method is scheduled to be executed. If data is shared between the component and the actor,
        ///     this is the responsibility of the user to implement a synchronization mechanism, as
        ///     <see cref="WaitAsync"/> and <see cref="IRunnableComponent.Tick"/> may be executed simultaneously.
        /// </summary>
        /// <param name="token">A token to be able to cancel the task.</param>
        /// <returns>A task with a result to be re-executed again or to stop, both possible results signal the scheduler to execute the <see cref="IRunnableComponent.Tick"/> method.</returns>
        Task<WaitResult> WaitAsync(CancellationToken token);
    }

    public enum WaitResult
    {
        Continuing,
        Completed
    }
}
