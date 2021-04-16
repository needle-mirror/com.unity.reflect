using System;
using System.Collections.Generic;
using Unity.Reflect;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;

namespace UnityEngine.Reflect
{
    [DisallowMultipleComponent]
    public class SyncObjectBinding : MonoBehaviour
    {
        public static Action<GameObject> OnCreated;
        public static Action<GameObject> OnDestroyed;

        public StreamKey streamKey;
#if UNITY_EDITOR
        public Bounds bounds;
#endif
        protected void Start()
        {
            OnCreated?.Invoke(gameObject);
        }

        protected void OnDestroy()
        {
            OnDestroyed?.Invoke(gameObject);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.color = Color.blue; 
            UnityEditor.Handles.DrawWireCube(bounds.center, bounds.size);
        }
#endif
    }
}


