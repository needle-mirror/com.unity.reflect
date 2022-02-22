using Unity.Reflect.ActorFramework;
using Unity.Reflect.Model;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    [Actor("29f18007-0e1c-4a7a-aa02-70cf2c819256")]
    public class UnityTextureActor : UnityResourceActor<Texture, SyncTexture, AcquireUnityTexture>
    {
#pragma warning disable 649
        RpcOutput<ConvertResource<SyncTexture>> m_ConvertSyncTextureOutput;
#pragma warning restore 649
        
        [RpcInput]
        void OnAcquireUnityTexture(RpcContext<AcquireUnityTexture> ctx)
        {
            AcquireResource(ctx, m_ConvertSyncTextureOutput);
        }
        
        [NetInput]
        void OnReleaseUnityTexture(NetContext<ReleaseUnityTexture> ctx)
        {
            ReleaseUnityResource(ctx.Data.Resource);
        }
    }
}
