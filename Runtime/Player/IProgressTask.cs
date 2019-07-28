using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

delegate void ProgressEventHandler(float percent);

public interface IProgressTask
{
    void Cancel();

    event Action<float, string> progressChanged;
    event Action taskCompleted;
}
