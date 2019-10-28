using System;
using UnityEngine;

namespace Unity.Labs.Utils
{
    public static class MathUtility
    {
        public static double Clamp(double input, double min, double max)
        {
            if (input > max)
                return max;

            return input < min ? min : input;
        }

        public static double ShortestAngleDistance(double start, double end, double halfMax, double max)
        {
            var angleDelta = end - start;
            angleDelta = Math.Abs(angleDelta) % max;
            if (angleDelta > halfMax)
                angleDelta = -(max - angleDelta);

            return angleDelta;
        }

        public static float ShortestAngleDistance(float start, float end, float halfMax, float max)
        {
            var angleDelta = end - start;
            angleDelta = Math.Abs(angleDelta) % max;
            if (angleDelta > halfMax)
                angleDelta = -(max - angleDelta);

            return angleDelta;
        }

        public static bool IsUndefined(this float value)
        {
            return float.IsInfinity(value) || float.IsNaN(value);
        }

        public static bool IsAxisAligned(this Vector3 v)
        {
            return Mathf.Approximately(v.x * v.y, 0) && Mathf.Approximately(v.y * v.z, 0) && Mathf.Approximately(v.z * v.x, 0);
        }

        public static bool IsPositivePowerOfTwo(int x)
        {
            return x > 0 && (x & (x - 1)) == 0;
        }

        public static int FirstActiveFlagIndex(int x)
        {
            if (x == 0)
                return 0;

            for (var i = 0; i < 32; i++)
                if ((x & 1 << i) != 0)
                    return i;

            return 0;
        }
    }
}
