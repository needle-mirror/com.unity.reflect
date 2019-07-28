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
            string m_Name;
            
            public string key;

            public Identifier(SyncObjectInstance instance)
            {
                // A SyncObjectInstance is considered unique when combining its Name with the source SyncObject's Name.
                // Note that instance.PersistentHash() can also be used but its too costly to use.
                m_Name = instance.Name;
                key = instance.Object;
            }
            
            public override string ToString()
            {
                return m_Name + " [" + key + "]";
            }
        }

        public Identifier identifier;

    }
}


