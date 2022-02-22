using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Actor("ee6620fb-def2-4613-82fb-0b3a5e7ac45a")]
    public class LiveSyncActor
    {
#pragma warning disable 649
        LiveSyncComponent m_Sync;

        EventOutput<SyncConnectionStateChanged> m_SyncConnectionStateChangedOutput;
        EventOutput<RemoteManifestChanged> m_RemoteManifestChangedOutput;
#pragma warning restore 649

        void Inject()
        {
            m_Sync.SyncConnectionStateChanged += OnSyncConnectionStateChanged;
            m_Sync.RemoteManifestChanged += OnRemoteManifestChanged;
            m_Sync.Start();
        }

        void Shutdown()
        {
            m_Sync.Stop();
            m_Sync.Shutdown();
            m_Sync.SyncConnectionStateChanged -= OnSyncConnectionStateChanged;
            m_Sync.RemoteManifestChanged -= OnRemoteManifestChanged;
        }

        void OnSyncConnectionStateChanged(SyncConnectionStateChanged msg)
        {
            m_SyncConnectionStateChangedOutput.Broadcast(msg);
        }

        void OnRemoteManifestChanged(RemoteManifestChanged msg)
        {
            m_RemoteManifestChangedOutput.Broadcast(msg);
        }
    }
}
