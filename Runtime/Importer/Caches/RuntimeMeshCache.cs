using System;
using Unity.Reflect;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public interface IMeshCache
    {
        Mesh GetMesh(StreamKey id);
    }
}