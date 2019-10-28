#if !NET_4_6
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Labs.Utils
{
    /// <summary>
    /// Polyfill for the built-in ReadOnlyDictionary type in .Net 4.6, under .Net 3.5
    /// Should function identically to that class, except for a bit of garbage allocation
    /// </summary>
    /// <typeparam name="K">The type of the dictionary key</typeparam>
    /// <typeparam name="T">The type of the dictionary value</typeparam>
    public class ReadOnlyDictionary<K, T> : IDictionary<K, T>
    {
        public class ValueCollection : ICollection<T>, ICollection
        {
            Dictionary<K, T>.ValueCollection m_Values;

            public ValueCollection(Dictionary<K, T>.ValueCollection values)
            {
                SyncRoot = new object();
                m_Values = values;
            }

            public IEnumerator<T> GetEnumerator()
            {
                return m_Values.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(T item)
            {
                throw new NotSupportedException(k_UnmodifiableMessage);
            }

            public void Clear()
            {
                throw new NotSupportedException(k_UnmodifiableMessage);
            }

            public bool Contains(T item)
            {
                return m_Values.Contains(item);
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public bool Remove(T item)
            {
                throw new NotSupportedException(k_UnmodifiableMessage);
            }

            public void CopyTo(Array array, int index)
            {
                throw new NotImplementedException();
            }

            int ICollection.Count
            {
                get { return m_Values.Count; }
            }

            public bool IsSynchronized { get; private set; }
            public object SyncRoot { get; private set; }

            int ICollection<T>.Count
            {
                get { return m_Values.Count; }
            }

            public bool IsReadOnly { get; private set; }
        }

        readonly Dictionary<K, T> m_Dictionary;
        readonly Dictionary<K, T>.KeyCollection m_KeyCollection;
        readonly Dictionary<K, T>.ValueCollection m_ValueCollection;

        const string k_UnmodifiableMessage = "Cannot modify a read-only collection";

        public ReadOnlyDictionary(Dictionary<K, T> dictionary)
        {
            m_Dictionary = dictionary;
            m_KeyCollection = dictionary.Keys;
            m_ValueCollection = dictionary.Values;
        }

        public IEnumerator<KeyValuePair<K, T>> GetEnumerator()
        {
            return m_Dictionary.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(KeyValuePair<K, T> item)
        {
            throw new NotSupportedException(k_UnmodifiableMessage);
        }

        public void Clear()
        {
            throw new NotSupportedException(k_UnmodifiableMessage);
        }

        public bool Contains(KeyValuePair<K, T> item)
        {
            return m_Dictionary.Contains(item);
        }

        public void CopyTo(KeyValuePair<K, T>[] array, int arrayIndex)
        {
            throw new NotImplementedException("CopyTo is not implemented for ReadOnlyDictionary polyfill");
        }

        public bool Remove(KeyValuePair<K, T> item)
        {
            throw new NotSupportedException(k_UnmodifiableMessage);
        }

        public int Count
        {
            get { return m_Dictionary.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public void Add(K key, T value)
        {
            throw new NotSupportedException(k_UnmodifiableMessage);
        }

        public bool ContainsKey(K key)
        {
            return m_Dictionary.ContainsKey(key);
        }

        public bool Remove(K key)
        {
            throw new NotSupportedException(k_UnmodifiableMessage);
        }

        public bool TryGetValue(K key, out T value)
        {
            return m_Dictionary.TryGetValue(key, out value);
        }

        public T this[K key]
        {
            get { return m_Dictionary[key]; }
            set { throw new NotSupportedException(k_UnmodifiableMessage); }
        }

        public ICollection<K> Keys
        {
            get { return m_KeyCollection; }
        }

        public ICollection<T> Values
        {
            get { return m_ValueCollection; }
        }
    }
}
#endif
