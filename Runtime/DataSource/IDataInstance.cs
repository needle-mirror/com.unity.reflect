using System;
using Unity.Reflect.Model;
using UnityEngine;

namespace UnityEngine.Reflect
{
    public interface IDataInstance
    {
        SyncId Id { get; }
    }
}
