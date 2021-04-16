using System;
using System.Threading;

namespace Unity.Reflect.Actor
{
    public class Lifecycle<TState>
    {
        public Action<TState> Initialize;
        public Action<TState> Shutdown;
        public Action<TState> Start;
        public Action<TState> Stop;
        public Func<TState, TimeSpan, CancellationToken, bool> Tick;

        public Lifecycle(Action<TState> initialize, Action<TState> shutdown, Action<TState> start, Action<TState> stop, Func<TState, TimeSpan, CancellationToken, bool> tick)
        {
            Initialize = initialize;
            Shutdown = shutdown;
            Start = start;
            Stop = stop;
            Tick = tick;
        }
    }
}
