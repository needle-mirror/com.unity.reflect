using System.Collections.Generic;

namespace Unity.Labs.Utils
{
    public static class ListExtensions
    {
        public static List<T> Fill<T>(this List<T> list, int count)
            where T: new()
        {
            for (var i = 0; i < count; i++)
            {
                list.Add(new T());
            }

            return list;
        }
    }
}
