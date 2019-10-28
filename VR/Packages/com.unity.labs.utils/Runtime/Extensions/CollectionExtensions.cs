using System.Collections.Generic;
using System.Text;

namespace Unity.Labs.Utils
{
    public static class CollectionExtensions
    {
        static readonly StringBuilder k_String = new StringBuilder();

        public static string Stringify<T>(this ICollection<T> collection)
        {
            k_String.Length = 0;
            var endIndex = collection.Count - 1;
            var counter = 0;
            foreach (var t in collection)
            {
                k_String.AppendFormat(counter++ == endIndex ? "{0}" : "{0}, ", t);
            }

            return k_String.ToString();
        }
    }
}
