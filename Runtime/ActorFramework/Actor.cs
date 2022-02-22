using System;

namespace Unity.Reflect.ActorFramework
{
    public class ActorHandle
    {
        public Type Type;
    }

    public class Actor<TState> where TState : class
    {
        public ActorHandle Handle;
        public TState State;
        public Lifecycle<TState> Lifecycle;

        public Actor(ActorHandle handle, TState state, Lifecycle<TState> lifecycle)
        {
            Handle = handle;
            State = state;
            Lifecycle = lifecycle;
        }
    }
}
