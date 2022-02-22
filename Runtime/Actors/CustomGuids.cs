using System;
using System.Runtime.CompilerServices;

namespace Unity.Reflect.Actors
{
    [Serializable]
    public struct EntryGuid : IFormattable, IComparable, IComparable<EntryGuid>, IEquatable<EntryGuid>
    {
        readonly Guid m_Guid;

        public EntryGuid(byte[] b) => m_Guid = new Guid(b);
        public Guid GetUntypedGuid => m_Guid;
        public override string ToString() => m_Guid.ToString();
        public string ToString(string format, IFormatProvider formatProvider) => m_Guid.ToString(format, formatProvider);
        public int CompareTo(EntryGuid other) => m_Guid.CompareTo(other.m_Guid);
        public bool Equals(EntryGuid other) => m_Guid.Equals(other.m_Guid);
        public override bool Equals(object obj) => obj is EntryGuid g && m_Guid.Equals(g.m_Guid);
        public override int GetHashCode() => m_Guid.GetHashCode();
        public int CompareTo(object obj)
        {
            if (!(obj is EntryGuid g))
                throw new ArgumentException($"Argument is not {nameof(EntryGuid)}");

            return m_Guid.CompareTo(g.m_Guid);
        }
        public static bool operator ==(EntryGuid a, EntryGuid b) => a.Equals(b);
        public static bool operator !=(EntryGuid a, EntryGuid b) => !a.Equals(b);
        public static EntryGuid NewGuid()
        {
            var g = Guid.NewGuid();
            return Unsafe.As<Guid, EntryGuid>(ref g);
        }
    }

    [Serializable]
    public struct EntryStableGuid : IFormattable, IComparable, IComparable<EntryStableGuid>, IEquatable<EntryStableGuid>
    {
        readonly Guid m_Guid;

        public EntryStableGuid(byte[] b) => m_Guid = new Guid(b);
        public Guid GetUntypedGuid => m_Guid;
        public override string ToString() => m_Guid.ToString();
        public string ToString(string format, IFormatProvider formatProvider) => m_Guid.ToString(format, formatProvider);
        public int CompareTo(EntryStableGuid other) => m_Guid.CompareTo(other.m_Guid);
        public bool Equals(EntryStableGuid other) => m_Guid.Equals(other.m_Guid);
        public override bool Equals(object obj) => obj is EntryStableGuid g && m_Guid.Equals(g.m_Guid);
        public override int GetHashCode() => m_Guid.GetHashCode();
        public int CompareTo(object obj)
        {
            if (!(obj is EntryStableGuid g))
                throw new ArgumentException($"Argument is not {nameof(EntryStableGuid)}");

            return m_Guid.CompareTo(g.m_Guid);
        }
        public static bool operator ==(EntryStableGuid a, EntryStableGuid b) => a.Equals(b);
        public static bool operator !=(EntryStableGuid a, EntryStableGuid b) => !a.Equals(b);
        public static EntryStableGuid NewGuid()
        {
            var g = Guid.NewGuid();
            return Unsafe.As<Guid, EntryStableGuid>(ref g);
        }
    }

    [Serializable]
    public struct ManifestGuid : IFormattable, IComparable, IComparable<ManifestGuid>, IEquatable<ManifestGuid>
    {
        readonly Guid m_Guid;
        public override string ToString() => m_Guid.ToString();
        public string ToString(string format, IFormatProvider formatProvider) => m_Guid.ToString(format, formatProvider);
        public int CompareTo(ManifestGuid other) => m_Guid.CompareTo(other.m_Guid);
        public bool Equals(ManifestGuid other) => m_Guid.Equals(other.m_Guid);
        public override bool Equals(object obj) => obj is ManifestGuid g && m_Guid.Equals(g.m_Guid);
        public override int GetHashCode() => m_Guid.GetHashCode();
        public int CompareTo(object obj)
        {
            if (!(obj is ManifestGuid g))
                throw new ArgumentException($"Argument is not {nameof(ManifestGuid)}");

            return m_Guid.CompareTo(g.m_Guid);
        }
        public static bool operator ==(ManifestGuid a, ManifestGuid b) => a.Equals(b);
        public static bool operator !=(ManifestGuid a, ManifestGuid b) => !a.Equals(b);
        public static ManifestGuid NewGuid()
        {
            var g = Guid.NewGuid();
            return Unsafe.As<Guid, ManifestGuid>(ref g);
        }
    }

    [Serializable]
    public struct ManifestStableGuid : IFormattable, IComparable, IComparable<ManifestStableGuid>, IEquatable<ManifestStableGuid>
    {
        readonly Guid m_Guid;
        public override string ToString() => m_Guid.ToString();
        public string ToString(string format, IFormatProvider formatProvider) => m_Guid.ToString(format, formatProvider);
        public int CompareTo(ManifestStableGuid other) => m_Guid.CompareTo(other.m_Guid);
        public bool Equals(ManifestStableGuid other) => m_Guid.Equals(other.m_Guid);
        public override bool Equals(object obj) => obj is ManifestStableGuid g && m_Guid.Equals(g.m_Guid);
        public override int GetHashCode() => m_Guid.GetHashCode();
        public int CompareTo(object obj)
        {
            if (!(obj is ManifestStableGuid g))
                throw new ArgumentException($"Argument is not {nameof(ManifestStableGuid)}");

            return m_Guid.CompareTo(g.m_Guid);
        }
        public static bool operator ==(ManifestStableGuid a, ManifestStableGuid b) => a.Equals(b);
        public static bool operator !=(ManifestStableGuid a, ManifestStableGuid b) => !a.Equals(b);
        public static ManifestStableGuid NewGuid()
        {
            var g = Guid.NewGuid();
            return Unsafe.As<Guid, ManifestStableGuid>(ref g);
        }
    }

    [Serializable]
    public struct DynamicGuid : IFormattable, IComparable, IComparable<DynamicGuid>, IEquatable<DynamicGuid>
    {
        readonly Guid m_Guid;
        public override string ToString() => m_Guid.ToString();
        public string ToString(string format, IFormatProvider formatProvider) => m_Guid.ToString(format, formatProvider);
        public int CompareTo(DynamicGuid other) => m_Guid.CompareTo(other.m_Guid);
        public bool Equals(DynamicGuid other) => m_Guid.Equals(other.m_Guid);
        public override bool Equals(object obj) => obj is DynamicGuid g && m_Guid.Equals(g.m_Guid);
        public override int GetHashCode() => m_Guid.GetHashCode();
        public int CompareTo(object obj)
        {
            if (!(obj is DynamicGuid g))
                throw new ArgumentException($"Argument is not {nameof(DynamicGuid)}");

            return m_Guid.CompareTo(g.m_Guid);
        }
        public static bool operator ==(DynamicGuid a, DynamicGuid b) => a.Equals(b);
        public static bool operator !=(DynamicGuid a, DynamicGuid b) => !a.Equals(b);
        public static DynamicGuid NewGuid()
        {
            var g = Guid.NewGuid();
            return Unsafe.As<Guid, DynamicGuid>(ref g);
        }
    }
}
