using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Unity.Reflect.ActorFramework;

namespace Unity.Reflect.Actors
{
    [Component("1d53bb5e-801c-42a3-9098-cef05ea1474f", isExcludedFromGraph: true)]
    public class LiveSyncComponent : IAsyncComponent
    {
        IPlayerClient m_Client;
        MpscSynchronizer m_Synchronizer = new MpscSynchronizer();
        ConcurrentQueue<Message> m_Messages = new ConcurrentQueue<Message>();

        public event Action<SyncConnectionStateChanged> SyncConnectionStateChanged;
        public event Action<RemoteManifestChanged> RemoteManifestChanged;

        [ComponentCtor]
        public LiveSyncComponent(IPlayerClient client)
        {
            m_Client = client;
        }

        public void Start()
        {
            if (m_Client == null)
                return;

            m_Client.ConnectionStatusChanged += OnConnectionStatusChanged;
            m_Client.ManifestUpdated += OnManifestUpdated;
        }

        public void Stop()
        {
            if (m_Client == null)
                return;

            m_Client.ConnectionStatusChanged -= OnConnectionStatusChanged;
            m_Client.ManifestUpdated -= OnManifestUpdated;
        }

        public void Shutdown()
        {
            m_Messages = new ConcurrentQueue<Message>();
        }

        public TickResult Tick(TimeSpan endTime)
        {
            while (m_Messages.TryDequeue(out var msg))
            {
                if (msg.Type == MessageType.ConnectionStateChanged)
                {
                    SyncConnectionStateChanged?.Invoke(new SyncConnectionStateChanged(msg.State));
                }
                else if (msg.Type == MessageType.ManifestUpdated)
                {
                    RemoteManifestChanged?.Invoke(new RemoteManifestChanged(msg.SourceId));
                }
            }

            return TickResult.Wait;
        }

        public async Task<WaitResult> WaitAsync(CancellationToken token)
        {
            await m_Synchronizer.WaitAsync(token);
            return WaitResult.Continuing;
        }

        void OnConnectionStatusChanged(ConnectionStatus state)
        {
            m_Messages.Enqueue(new Message{ Type = MessageType.ConnectionStateChanged, State = state });
            m_Synchronizer.Set();
        }

        void OnManifestUpdated(object sender, ManifestUpdatedEventArgs args)
        {
            m_Messages.Enqueue(new Message{ Type = MessageType.ManifestUpdated, SourceId = args.SourceId });
            m_Synchronizer.Set();
        }

        enum MessageType
        {
            ConnectionStateChanged,
            ManifestUpdated
        }

        struct Message
        {
            public MessageType Type;
            public ConnectionStatus State;
            public string SourceId;
        }
    }
}
