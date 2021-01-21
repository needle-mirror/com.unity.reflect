using Unity.Reflect.Data;
using Unity.Reflect.Model;

namespace Unity.Reflect
{
    public struct StreamKey
    {
        public readonly string source;
        public readonly PersistentKey key;

        public StreamKey(string sourceId, PersistentKey key)
        {
            this.key = key;
            source = sourceId;
        }
        
        public static StreamKey FromSyncId<T>(string sourceId, SyncId id) where T : ISyncModel
        {
            return new StreamKey(sourceId, PersistentKey.GetKey<T>(id));
        }
    }

    public struct SyncedData<T>
    {
        public readonly StreamKey key;
        public readonly T data;

        public SyncedData(StreamKey key, T data)
        {
            this.key = key;
            this.data = data;
        }
    }
}