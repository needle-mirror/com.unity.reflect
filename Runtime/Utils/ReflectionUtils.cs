using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Unity.Reflect
{
    public static class ReflectionUtils
    {
        static List<Assembly> s_PotentialAssemblies = new List<Assembly>();
        static Dictionary<string, Type> s_PotentialTypes = new Dictionary<string, Type>();

        /// <summary>
        ///     Convert a type (string) to an hierarchy of <see cref="StringTypeElem"/>,
        ///     effectively extracting general information about the string.
        /// </summary>
        /// <param name="fullName">The full name of the type, including generic type if there is any.</param>
        /// <returns></returns>
        public static StringTypeElem ExtractTypes(string fullName)
        {
            return GetGenericElems(fullName)[0];
        }

        /// <summary>
        ///     Get the closed type for the <see cref="closedType"/> received.
        /// </summary>
        /// <remarks>
        ///     This method works with nested generic type.
        ///     If the string is not a closed type, this method may throw.
        ///     If the type does not exist, this method may throw.
        /// </remarks>
        /// <param name="closedType">The type as a string without the assembly qualifier e.g. "System.Int32", "System.Collections.Generic.List`1[System.Int32]"</param>
        /// <returns>The type found in any loaded assembly.</returns>
        public static Type GetClosedTypeFromAnyAssembly(string closedType)
        {
            var elem = ExtractTypes(closedType);
            return BuildClosedType(elem, closedType);
        }

        /// <summary>
        ///     Check if 2 types are related through interface implementation, parent/child relation or if they are the same type.
        /// </summary>
        /// <param name="t1"></param>
        /// <param name="t2"></param>
        /// <returns></returns>
        public static bool AreRelated(Type t1, Type t2)
        {
            return IsPartOf(t1, t2) || IsPartOf(t2, t1);
        }

        public static void FlushCache()
        {
            s_PotentialAssemblies = new List<Assembly>();
            s_PotentialTypes = new Dictionary<string, Type>();
        }

        static bool IsPartOf(Type parent, Type child)
        {
            if (child == parent)
                return true;

            if (child.GetInterfaces().Contains(parent))
                return true;

            return child.IsSubclassOf(parent);
        }

        static Type BuildClosedType(StringTypeElem elem, string fullName)
        {
            var typeStr = fullName.Substring(elem.NameStartIndex, elem.OpenGenericEndIndex);
            var type = GetTypeFromAnyAssembly(typeStr);
            if (type == null)
                return null;

            if (type.IsGenericTypeDefinition)
            {
                var typeArgs = elem.GenericElems.Select(x => BuildClosedType(x, fullName)).ToArray();
                if (typeArgs.Any(x => x == null))
                    return null;

                type = type.MakeGenericType();
            }

            return type;
        }

        static Type GetTypeFromAnyAssembly(string closedNonGenericOrUnboundGeneric)
        {
            if (s_PotentialTypes.TryGetValue(closedNonGenericOrUnboundGeneric, out var type))
                return type;

            type = s_PotentialAssemblies
                .SelectMany(x => x.GetTypes())
                .FirstOrDefault(x => x.ToString() == closedNonGenericOrUnboundGeneric);

            if (type != null)
            {
                s_PotentialTypes.Add(closedNonGenericOrUnboundGeneric, type);
                return type;
            }

            type = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .FirstOrDefault(x => x.ToString() == closedNonGenericOrUnboundGeneric);

            if (type != null)
            {
                s_PotentialAssemblies.Add(type.Assembly);
                s_PotentialTypes.Add(closedNonGenericOrUnboundGeneric, type);
            }

            return type;
        }

        static List<StringTypeElem> GetGenericElems(string generics)
        {
            var elems = new List<StringTypeElem>{ new StringTypeElem() };
            var activeElem = elems[0];

            for (var i = 0; i < generics.Length; ++i)
            {
                switch (generics[i])
                {
                    case '[':
                    {
                        activeElem.OpenGenericEndIndex = i;
                        activeElem.IsGeneric = true;

                        var elem = new StringTypeElem{ NameStartIndex = i + 1, Parent = activeElem };
                        activeElem.GenericElems.Add(elem);
                        activeElem = elem;
                        break;
                    }
                    case ']':
                        activeElem.NameEndIndex = i;
                        if (!activeElem.IsGeneric)
                            activeElem.OpenGenericEndIndex = i;

                        activeElem = activeElem.Parent;
                        activeElem.NameEndIndex = i + 1;
                        break;
                    case ',':
                    {
                        activeElem.NameEndIndex = i;
                        if (!activeElem.IsGeneric)
                            activeElem.OpenGenericEndIndex = i;

                        var elem = new StringTypeElem { NameStartIndex = i + 1, Parent = activeElem.Parent };
                        activeElem.Parent.GenericElems.Add(elem);
                        activeElem = elem;
                        break;
                    }
                }
            }
            
            activeElem.NameEndIndex = generics.Length;
            if (!activeElem.IsGeneric)
                activeElem.OpenGenericEndIndex = generics.Length;

            return elems;
        }

        public class StringTypeElem
        {
            public int NameStartIndex;
            public int NameEndIndex;
            public int OpenGenericEndIndex;
            public bool IsGeneric;
            public StringTypeElem Parent;
            public List<StringTypeElem> GenericElems = new List<StringTypeElem>();
        }
    }
}
