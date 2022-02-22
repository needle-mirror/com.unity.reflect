using System;
using Unity.Reflect.Data;
using Unity.Reflect.Model;

namespace Unity.Reflect
{
    public readonly struct StreamKey : IEquatable<StreamKey>
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

        public bool Equals(StreamKey other)
        {
            return string.Equals(source, other.source) && key.Equals(other.key);
        }

        public override bool Equals(object obj)
        {
            return obj is StreamKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = key.GetHashCode();
                if(source != null)
                    hashCode = (hashCode * 397) ^ source.GetHashCode();
                return hashCode;
            }
        }

        public static bool operator ==(StreamKey a, StreamKey b) => a.Equals(b);

        public static bool operator !=(StreamKey a, StreamKey b) => !(a == b);

        public override string ToString()
        {
            return $"[{source}, {key}]";
        }
    }

    public readonly struct SyncedData<T>
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