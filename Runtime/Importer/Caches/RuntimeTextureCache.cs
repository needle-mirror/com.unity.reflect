using System;
using Unity.Reflect;
using Unity.Reflect.Model;

namespace UnityEngine.Reflect
{
    public interface ITextureCache
    {
        Texture2D GetTexture(StreamKey id);
    }
}