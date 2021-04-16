using Unity.Reflect.Streaming;

namespace Unity.Reflect.Actor.Samples
{
    [Actor]
    public class SampleDummyActor
    {
        [NetInput]
        public void OnEntryDataChanged(NetContext<EntryDataChanged> ctx)
        {
        }
        
        [NetInput]
        public void OnGameObjectCreated(NetContext<GameObjectCreated> ctx)
        {
        }
    }
}
