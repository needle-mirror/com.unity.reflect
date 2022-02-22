using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Actor("9f0decc5-877c-40fc-ad70-5c267269c350")]
    public class IndicatorActor
    {
#pragma warning disable 649
        EventOutput<AssetCountChanged> m_AssetCountChangedOutput;
        EventOutput<InstanceCountChanged> m_InstanceCountChangedOutput;
        EventOutput<GameObjectCountChanged> m_GameObjectCountChangedOutput;
        EventOutput<StreamingProgressed> m_StreamingProgressedOutput;
#pragma warning restore 649

        ItemCount m_AssetCountData;
        ItemCount m_InstanceCountData;
        ItemCount m_GameObjectCountData;

        int m_PrevVisibleInstanceCount;

        public void Inject()
        {
            ResetCounts();
        }

        [NetInput]
        void OnEntryDataChanged(NetContext<EntryDataChanged> ctx)
        {
            m_AssetCountData.NbAdded += ctx.Data.Delta.Added.Count;
            m_AssetCountData.NbChanged += ctx.Data.Delta.Changed.Count;
            m_AssetCountData.NbRemoved += ctx.Data.Delta.Removed.Count;
            
            m_AssetCountChangedOutput.Broadcast(new AssetCountChanged(m_AssetCountData));
        }

        [NetInput]
        void OnUpdateStreaming(NetContext<UpdateStreaming> ctx)
        {
            var visibleInstancesCount = ctx.Data.VisibleInstances.Count;
            var hiddenInstancesCount = ctx.Data.HiddenInstancesSinceLastUpdate.Count;
            // repeated = prev - hidden => added = (total - repeated)
            m_InstanceCountData.NbAdded += visibleInstancesCount - (m_PrevVisibleInstanceCount - hiddenInstancesCount);
            m_InstanceCountData.NbRemoved += hiddenInstancesCount;

            m_PrevVisibleInstanceCount = visibleInstancesCount;
            
            m_InstanceCountChangedOutput.Broadcast(new InstanceCountChanged(m_InstanceCountData));
        }

        [PipeInput]
        void OnGameObjectCreating(PipeContext<GameObjectCreating> ctx)
        {
            m_GameObjectCountData.NbAdded += ctx.Data.GameObjectIds.Count;
            
            m_StreamingProgressedOutput.Broadcast(new StreamingProgressed(
                m_GameObjectCountData.NbAdded - m_GameObjectCountData.NbRemoved, 
                m_InstanceCountData.NbAdded - m_InstanceCountData.NbRemoved));

            m_GameObjectCountChangedOutput.Broadcast(new GameObjectCountChanged(m_InstanceCountData));

            ctx.Continue();
        }

        [PipeInput]
        void OnGameObjectDestroying(PipeContext<GameObjectDestroying> ctx)
        {
            m_GameObjectCountData.NbRemoved += ctx.Data.GameObjectIds.Count;
            
            m_StreamingProgressedOutput.Broadcast(new StreamingProgressed(
                m_GameObjectCountData.NbAdded - m_GameObjectCountData.NbRemoved, 
                m_InstanceCountData.NbAdded - m_InstanceCountData.NbRemoved));

            m_GameObjectCountChangedOutput.Broadcast(new GameObjectCountChanged(m_InstanceCountData));

            ctx.Continue();
        }
        
        void ResetCounts()
        {
            m_AssetCountData.NbAdded = 0;
            m_AssetCountData.NbChanged = 0;
            m_AssetCountData.NbRemoved = 0;
            m_AssetCountChangedOutput.Broadcast(new AssetCountChanged(m_AssetCountData));

            m_InstanceCountData.NbAdded = 0;
            m_InstanceCountData.NbChanged = 0;
            m_InstanceCountData.NbRemoved = 0;
            m_InstanceCountChangedOutput.Broadcast(new InstanceCountChanged(m_InstanceCountData));

            m_GameObjectCountData.NbAdded = 0;
            m_GameObjectCountData.NbChanged = 0;
            m_GameObjectCountData.NbRemoved = 0;
            m_GameObjectCountChangedOutput.Broadcast(new GameObjectCountChanged(m_InstanceCountData));

            m_PrevVisibleInstanceCount = 0;
        }
    }
}
