using System;
using Unity.Reflect.Model;

namespace Unity.Reflect
{
    public class StreamingErrorEvent
    {
        public readonly StreamKey Key;
        public readonly SyncBoundingBox BoundingBox;
        public Exception Exception;

        public StreamingErrorEvent(StreamKey key, SyncBoundingBox boundingBox, Exception ex)
        {
            Key = key;
            BoundingBox = boundingBox;
            Exception = ex;
        }
    }
}
