using System;
using System.Collections.Generic;

namespace UnityEngine.Reflect
{
    [Serializable]
    public class SerializedDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] List<TKey> m_Keys = new List<TKey>();
        [SerializeField] List<TValue> m_Values = new List<TValue>();

        public Dictionary<TKey, TValue> dictionary
        {
            // To not breaking backward compatibility, before the dictionary was a field instead of inheriting
            get { return this; }
        }

        public void OnBeforeSerialize()
        {
            m_Keys.Clear();
            m_Values.Clear();

            foreach (var keyPair in this)
            {
                m_Keys.Add(keyPair.Key);
                m_Values.Add(keyPair.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            Clear();

            for (int i = 0; i < m_Keys.Count; ++i)
                Add(m_Keys[i], m_Values[i]);
        }
    }
}
