using System.Collections.Generic;
using System.Linq;
using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Actor("13e5e3f0-dc0b-46c3-8f08-ada9b0eabd61")]
    public class DynamicEntryActor
    {
#pragma warning disable 649
        NetOutput<DynamicEntryChanged> m_DynamicEntryChangedOutput;
#pragma warning restore 649
        
        DynamicEntryHandler m_DynamicEntryHandler = new DynamicEntryHandler();

        [RpcInput]
        void OnUpdateEntryDependencies(RpcContext<UpdateEntryDependencies> ctx)
        {
            m_DynamicEntryHandler.OnUpdateEntryDependencies(ctx, m_DynamicEntryChangedOutput);
        }

        [NetInput]
        void OnEntryDataChanged(NetContext<EntryDataChanged> ctx)
        {
            m_DynamicEntryHandler.OnEntryDataChanged(ctx, m_DynamicEntryChangedOutput);
        }

        [RpcInput]
        void OnAcquireDynamicEntry(RpcContext<AcquireDynamicEntry> ctx)
        {
            m_DynamicEntryHandler.OnAcquireDynamicEntry(ctx);
        }

        [RpcInput]
        void OnGetDynamicIds(RpcContext<GetDynamicIds> ctx)
        {
            m_DynamicEntryHandler.OnGetDynamicIds(ctx);
        }

        [PipeInput]
        void OnClearBeforeSyncUpdate(PipeContext<ClearBeforeSyncUpdate> ctx)
        {
            m_DynamicEntryHandler.OnClearBeforeSyncUpdate(ctx);
        }

        [RpcInput]
        void OnGetStableId(RpcContext<GetStableId<DynamicGuid>> ctx)
        {
            m_DynamicEntryHandler.OnGetStableId(ctx);
        }
    }
}
