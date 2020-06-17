using System;

namespace UnityEngine.Reflect
{
    public interface IUpdateDelegate
    {
        event Action<float> onUpdate;
    }
}