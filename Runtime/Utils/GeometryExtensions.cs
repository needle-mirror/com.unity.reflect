using Unity.Reflect.Model;
using UnityEngine;
using UnityEngine.Reflect;

namespace Unity.Reflect.Geometry
{
    public static class GeometryExtensions
    {
        public static Bounds ToUnity(this Aabb box)
        {
            return new Bounds(box.Center.ToUnity(), box.Size.ToUnity());
        }

        public static Aabb ToReflect(this Bounds box)
        {
            return new Aabb(box.center.ToNumerics(), box.extents.ToNumerics(), Aabb.FromCenterTag);
        }

        public static Aabb ToReflect(this SyncBoundingBox box)
        {
            return new Aabb(box.Min, box.Max, Aabb.FromMinMaxTag);
        }
    }
}
