using System;

namespace Unity.Reflect.Actor
{
    public class ActorRef
    {
        public Type Type;
    }

    public class Actor<TState> where TState : class
    {
        public ActorRef ActorRef;
        public TState State;
        public Lifecycle<TState> Lifecycle;

        public Actor(ActorRef actorRef, TState state, Lifecycle<TState> lifecycle)
        {
            ActorRef = actorRef;
            State = state;
            Lifecycle = lifecycle;
        }
    }
}
