using System.Linq;
using Unity.Reflect.Streaming;

namespace Unity.Reflect.Actor.Samples
{
    [Actor]
    public class SampleSpatialActor
    {
#pragma warning disable 649
        NetOutput<UpdateStreaming> m_UpdateStreamingOutput;
#pragma warning restore 649

        [NetInput]
        void OnSpatialDataChanged(NetContext<SpatialDataChanged> ctx)
        {
            var added = ctx.Data.Added.Select(x => x.Id).ToList();
            var removed = ctx.Data.Removed.Select(x => x.Id).ToList();
            m_UpdateStreamingOutput.Send(new UpdateStreaming(added, removed));
        }
    }
}
