using System;
using UnityEngine.Events;

namespace UnityEngine.Reflect.Pipeline
{
    [Serializable]
    public class ProjectStreamerSettings
    {
        [Header("Events")]
        public UnityEvent onProjectStreamBegin;
        public UnityEvent onProjectStreamEnd;
        
        public void OnProjectStreamBegin()
        {
            onProjectStreamBegin?.Invoke();
        }

        public void OnProjectStreamEnd()
        {
            onProjectStreamEnd?.Invoke();
        }
    }
}
