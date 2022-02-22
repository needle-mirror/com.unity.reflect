using System;
using UnityEngine;

namespace Unity.Reflect.Actors
{
    public class DebugActorComponent : MonoBehaviour
    {
        public Action DrawGizmosCommand { private get; set; }
        public Action GuiCommand { private get; set; }

        void OnDrawGizmos()
        {
            DrawGizmosCommand?.Invoke();
        }

        void OnGUI()
        {
            GuiCommand?.Invoke();
        }
    }
}
