using System.Numerics;

namespace Unity.Reflect.Geometry
{
	public struct AABB
	{
		public Vector3 Min;
		public Vector3 Max;

		public AABB(Vector3 min, Vector3 max)
		{
			Min = min;
			Max = max;
		}
	}
}