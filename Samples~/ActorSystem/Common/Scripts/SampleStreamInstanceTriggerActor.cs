using System.Linq;
using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors.Samples
{
    [Actor]
    public class SampleStreamInstanceTriggerActor
    {
#pragma warning disable 649
        NetOutput<UpdateStreaming> m_UpdateStreamingOutput;
#pragma warning restore 649

        [NetInput]
        void OnDynamicEntryChanged(NetContext<DynamicEntryChanged> ctx)
        {
            var addedInstances = ctx.Data.Delta.Added.Select(entry => entry.Id).ToList();
            var removedInstances = ctx.Data.Delta.Removed.Select(entry => entry.Id).ToList();
            m_UpdateStreamingOutput.Send(new UpdateStreaming(addedInstances, removedInstances));
        }
    }
}
