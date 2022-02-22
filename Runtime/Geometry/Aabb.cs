using System.Numerics;

namespace Unity.Reflect.Geometry
{
	public struct Aabb
    {
        public static FromCenter FromCenterTag = new FromCenter();
        public static FromMinMax FromMinMaxTag = new FromMinMax();

		public Vector3 Center;
		public Vector3 Extents;

        public Vector3 Size => Extents * 2.0f;
        public Vector3 Min => Center - Extents;
        public Vector3 Max => Center + Extents;

        public float Volume
        {
            get
            {
                var size = Size;
                return size.X * size.Y * size.Z;
            }
        }

        public Aabb(Vector3 center, Vector3 extents, FromCenter _)
		{
            Center = center;
            Extents = extents;
		}

        public Aabb(Vector3 min, Vector3 max, FromMinMax _)
        {
            Center = (min + max) * 0.5f;
            Extents = (max - min) * 0.5f;
        }

        public void Encapsulate(Aabb add)
        {
            var min = Min;
            var max = Max;
            var addMin = add.Min;
            var addMax = add.Max;

            if (addMin.X < min.X)
                min.X = addMin.X;
            if (addMin.Y < min.Y)
                min.Y = addMin.Y;
            if (addMin.Z < min.Z)
                min.Z = addMin.Z;

            if (addMax.X > max.X)
                max.X = addMax.X;
            if (addMax.Y > max.Y)
                max.Y = addMax.Y;
            if (addMax.Z > max.Z)
                max.Z = addMax.Z;

            Center = (min + max) * 0.5f;
            Extents = (max - min) * 0.5f;
        }

        /// <summary>
        ///     Positive value is a distance on the specified axis, negative value is the overlap on the specified axis
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public Vector3 Overlap(Aabb other)
        {
            return Vector3.Abs(other.Center - Center) - other.Extents - Extents;
        }

        public bool Contains(Vector3 point)
        {
            return point.X >= Min.X && point.X <= Max.X &&
                   point.Y >= Min.Y && point.Y <= Max.Y &&
                   point.Z >= Min.Z && point.Z <= Max.Z;
        }

        public bool Equals(Aabb other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }

        public override bool Equals(object obj)
        {
            return obj is Aabb other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Min.GetHashCode() * 397) ^ Max.GetHashCode();
            }
        }

        public static bool operator ==(Aabb x, Aabb y)
        {
            return x.Equals(y);
        }

        public static bool operator !=(Aabb x, Aabb y) 
        {
            return !(x == y);
        }

        public static Aabb operator *(Matrix4x4 matrix4X4, Aabb box)
        {
            //        d--------c max
            //       /|       /|
            //      a--------b |
            //      | h------|-g
            //      |/       |/
            //  min e--------f

            var min = box.Min;
            var max = box.Max;

            var a = matrix4X4.MultiplyPoint3x4(new Vector3(min.X, max.Y, min.Z));
            var b = matrix4X4.MultiplyPoint3x4(new Vector3(max.X, max.Y, min.Z));
            var c = matrix4X4.MultiplyPoint3x4(max);
            var d = matrix4X4.MultiplyPoint3x4(new Vector3(min.X, max.Y, max.Z));
            var e = matrix4X4.MultiplyPoint3x4(min);
            var f = matrix4X4.MultiplyPoint3x4(new Vector3(max.X, min.Y, min.Z));
            var g = matrix4X4.MultiplyPoint3x4(new Vector3(max.X, min.Y, max.Z));
            var h = matrix4X4.MultiplyPoint3x4(new Vector3(min.X, min.Y, max.Z));

            min.X = ReflectMath.Min(a.X, b.X, c.X, d.X, e.X, f.X, g.X, h.X);
            max.X = ReflectMath.Max(a.X, b.X, c.X, d.X, e.X, f.X, g.X, h.X);

            min.Y = ReflectMath.Min(a.Y, b.Y, c.Y, d.Y, e.Y, f.Y, g.Y, h.Y);
            max.Y = ReflectMath.Max(a.Y, b.Y, c.Y, d.Y, e.Y, f.Y, g.Y, h.Y);

            min.Z = ReflectMath.Min(a.Z, b.Z, c.Z, d.Z, e.Z, f.Z, g.Z, h.Z);
            max.Z = ReflectMath.Max(a.Z, b.Z, c.Z, d.Z, e.Z, f.Z, g.Z, h.Z);

            return new Aabb(min, max, FromMinMaxTag);
        }

        public struct FromCenter { }
        public struct FromMinMax { }
    }
}
