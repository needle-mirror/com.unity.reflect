using System;
using Unity.Reflect;

namespace UnityEngine.Reflect
{
    public interface IObjectCache
    {
        SyncObjectBinding CreateInstance(StreamKey objectKey);
    }
}