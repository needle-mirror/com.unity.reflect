using Unity.Reflect.Actor;

namespace Unity.Reflect.Streaming
{
    [Actor]
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

        public void Inject()
        {
            ResetCounts();
        }

        public void Shutdown()
        {
            // nothing to do here
        }

        [NetInput]
        void OnEntryDataChanged(NetContext<EntryDataChanged> ctx)
        {
            m_AssetCountData.NbAdded += ctx.Data.Added.Count;
            m_AssetCountData.NbChanged += ctx.Data.Changed.Count;
            m_AssetCountData.NbRemoved += ctx.Data.Removed.Count;
            
            m_AssetCountChangedOutput.Broadcast(new AssetCountChanged(m_AssetCountData));
        }

        [NetInput]
        void OnSpatialDataChanged(NetContext<SpatialDataChanged> ctx)
        {
            m_InstanceCountData.NbAdded += ctx.Data.Added.Count;
            m_InstanceCountData.NbChanged += ctx.Data.Changed.Count;
            m_InstanceCountData.NbRemoved += ctx.Data.Removed.Count;
            
            m_InstanceCountChangedOutput.Broadcast(new InstanceCountChanged(m_InstanceCountData));
        }

        [NetInput]
        void OnGameObjectCreated(NetContext<GameObjectCreated> ctx)
        {
            m_GameObjectCountData.NbAdded++;
            
            m_StreamingProgressedOutput.Broadcast(new StreamingProgressed(
                m_GameObjectCountData.NbAdded - m_GameObjectCountData.NbRemoved, 
                m_InstanceCountData.NbAdded - m_InstanceCountData.NbRemoved));

            m_GameObjectCountChangedOutput.Broadcast(new GameObjectCountChanged(m_InstanceCountData));
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

        }
    }
}
