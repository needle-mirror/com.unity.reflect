using System;
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
                return m_Id + " [" + key + "]";
            }
        }

        public static Action<GameObject> OnCreated;
        public static Action<GameObject> OnDestroyed;

        public Identifier identifier;

        protected void Start()
        {
            OnCreated?.Invoke(gameObject);
        }

        protected void OnDestroy()
        {
            OnDestroyed?.Invoke(gameObject);
        }
    }
}


