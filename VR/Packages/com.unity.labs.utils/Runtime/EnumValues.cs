using System;

namespace Unity.Labs.Utils
{
    public static class EnumValues<T>
    {
        public static readonly T[] Values = (T[])Enum.GetValues(typeof(T));
    }
}
