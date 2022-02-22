using Unity.Reflect.ActorFramework;
using Unity.Reflect.Model;

namespace Unity.Reflect.Actors
{
    [Actor("70105b78-4dae-4cc1-981e-1d5167a1eb65")]
    public class DynamicEntryFilterActor
    {
#pragma warning disable 649
        NetOutput<SpatialDataChanged> m_SpatialDataChangedOutput;
#pragma warning restore 649

        [NetInput]
        void OnDynamicEntryChanged(NetContext<DynamicEntryChanged> ctx)
        {
            var spatialMergedDelta = CreateSpatialMergedDelta(ctx.Data.Delta);
            m_SpatialDataChangedOutput.Send(new SpatialDataChanged(spatialMergedDelta));
        }

        static Delta<DynamicEntry> CreateSpatialMergedDelta(Delta<DynamicEntry> delta)
        {
            var spatialMergedDelta = new Delta<DynamicEntry>
            {
                Added = delta.Added.FindAll(x => IsSpatialized(x)),
                Removed = delta.Removed.FindAll(x => IsSpatialized(x)),
                Changed = delta.Changed.FindAll(x => IsSpatialized(x.Prev))
            };
            return spatialMergedDelta;
        }

        static bool IsSpatialized(DynamicEntry dynamicEntry)
        {
            return dynamicEntry.Data.Spatial != null &&
                   dynamicEntry.Data.EntryType == typeof(SyncObjectInstance) ||
                   dynamicEntry.Data.EntryType == typeof(SyncNode);
        }
    }
}
