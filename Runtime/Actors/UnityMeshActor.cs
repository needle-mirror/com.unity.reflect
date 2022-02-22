using Unity.Reflect.ActorFramework;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    [Actor("bfc97fc8-cff7-41cf-a975-d42686dda1a2")]
    public class UnityMeshActor : UnityResourceActor<Mesh, SyncMesh, AcquireUnityMesh>
    {
#pragma warning disable 649
        RpcOutput<ConvertResource<SyncMesh>> m_ConvertSyncMeshOutput;
#pragma warning restore 649
        
        [RpcInput]
        void OnAcquireUnityMesh(RpcContext<AcquireUnityMesh> ctx)
        {
            AcquireResource(ctx, m_ConvertSyncMeshOutput);
        }
        
        [NetInput]
        void OnReleaseUnityMesh(NetContext<ReleaseUnityMesh> ctx)
        {
            ReleaseUnityResource(ctx.Data.Resource);
        }
    }
}
