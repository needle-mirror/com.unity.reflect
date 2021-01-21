using System;
using Unity.Reflect;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{   
    public interface IMaterialCache
    {
        Material GetMaterial(StreamKey id);
    }
}