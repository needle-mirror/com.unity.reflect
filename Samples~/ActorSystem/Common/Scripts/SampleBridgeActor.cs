using Unity.Reflect.Streaming;
using UnityEngine;

namespace Unity.Reflect.Actor.Samples
{
    [Actor]
    public class SampleBridgeActor
    {
#pragma warning disable 649
        RpcOutput<UpdateManifests> m_UpdateManifestsOutput;
#pragma warning restore 649

        public void SendUpdateManifests()
        {
            var rpc = m_UpdateManifestsOutput.Call(this, (object)null, (object)null, new UpdateManifests());
            rpc.Success<object>((self, ctx, userCtx, res) =>
            {
                Debug.Log("Manifests loaded");
            });
            rpc.Failure((self, ctx, userCtx, ex) =>
            {
                Debug.LogException(ex);
            });
        }
    }
}
