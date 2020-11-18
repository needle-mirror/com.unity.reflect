using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

delegate void ProgressEventHandler(float percent);

// TODO ReflectWindow reference, can now move to Unity.Reflect.Editor namespace.
public interface IProgressTask
{
    void Cancel();

    event Action<float, string> progressChanged;
    event Action taskCompleted;
}
