using System.Collections.Generic;

namespace Unity.Labs.Utils
{
    public static class DictionaryExtensions
    {
        /// <summary>
        /// Like LINQ's .First(), but does not allocate
        /// </summary>
        /// <param name="dictionary">Dictionary to retrieve the element from</param>
        /// <typeparam name="TKey">Dictionary's Key type</typeparam>
        /// <typeparam name="TValue">Dictionary's Value type</typeparam>
        /// <returns>The first element in the dictionary</returns>
        public static KeyValuePair<TKey, TValue> First<TKey, TValue>(this Dictionary<TKey, TValue> dictionary)
        {
            KeyValuePair<TKey, TValue> kvp = default(KeyValuePair<TKey, TValue>);
            var enumerator = dictionary.GetEnumerator();
            if (enumerator.MoveNext())
            {
                kvp = enumerator.Current;
            }

            enumerator.Dispose();
            return kvp;
        }
    }
}
