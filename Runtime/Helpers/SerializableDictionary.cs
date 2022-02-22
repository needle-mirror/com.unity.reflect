using System;
using System.Collections.Generic;

namespace UnityEngine.Reflect.Pipeline
{
    [Serializable]
    abstract class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField]
        List<TKey> m_Keys = new List<TKey>();

        [SerializeField]
        List<TValue> m_Values = new List<TValue>();
        
        public void OnBeforeSerialize()
        {
            m_Keys = new List<TKey>();
            m_Values = new List<TValue>();
            
            foreach (var entry in this)
            {
                m_Keys.Add(entry.Key);
                m_Values.Add(entry.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            for (var i = 0; i < m_Keys.Count; ++i)
            {
                this[m_Keys[i]] = m_Values[i];
            }
        }
    }
}
