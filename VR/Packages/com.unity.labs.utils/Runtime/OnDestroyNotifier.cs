using System;
using UnityEngine;

namespace Unity.Labs.Utils
{
    [ExecuteInEditMode]
    public class OnDestroyNotifier : MonoBehaviour
    {
        public Action<OnDestroyNotifier> destroyed { private get; set; }

        void OnDestroy()
        {
            if (destroyed != null)
                destroyed(this);
        }
    }
}
