using System;
using System.Collections.Generic;
using Unity.Reflect.Data;
using Unity.Reflect.Model;
using UnityEngine;

namespace UnityEngine.Reflect
{
    [DisallowMultipleComponent]
    public class SyncObjectBinding : MonoBehaviour
    {
        [Serializable]
        public struct Identifier
        {
            const string k_StringFormat = "{0} [{1}]";
            
            [SerializeField]
            string m_Id;
            
            public string key;

            public Identifier(SyncObjectInstance instance)
            {
                // A SyncObjectInstance is considered unique when combining its Name with the source SyncObject's Name.
                // Note that instance.PersistentHash() can also be used but its too costly to use.
                m_Id = instance.Id.Value;
                key = instance.ObjectId.Value;
            }
            
            public override string ToString()
            {
                return string.Format(k_StringFormat, m_Id, key);
            }
            
            public bool Equals(Identifier other)
            {
                return m_Id == other.m_Id && key == other.key;
            }

            public override bool Equals(object obj)
            {
                return obj is Identifier other && Equals(other);
            }

            public override int GetHashCode()
            {
                return m_Id.GetHashCode() * 397 ^ key.GetHashCode();
            }
        }

        public static Action<GameObject> OnCreated;
        public static Action<GameObject> OnDestroyed;

        public Identifier identifier;
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


