using UnityEngine;

namespace Unity.Labs.Utils
{
    public static class CoefficientUtils
    {
        /// <summary>
        /// Returns the interpolation value covered by a given range.
        /// Ensure that max != min to avoid dividing by zero
        /// </summary>
        /// <param name="a">Either of the pair of points</param>
        /// <param name="b">Either of the pair of points</param>
        /// <param name="min">the distance at which the coefficient will be 0</param>
        /// <param name="max">the distance at which the coefficient will be 1</param>
        /// <returns>the interpolation value covered by a given range</returns>
        public static float FromDistance(Vector3 a, Vector3 b, float min, float max)
        {
            return Mathf.Clamp01((Vector3.Distance(a, b) - min) / (max - min));
        }

        /// <summary>
        /// Returns the interpolation value covered by a given range.
        /// Ensure that max != min to avoid dividing by zero
        /// </summary>
        /// <param name="distance">The actual distance value</param>
        /// <param name="min">the distance at which the coefficient will be 0</param>
        /// <param name="max">the distance at which the coefficient will be 1</param>
        /// <returns>the interpolation value covered by a given range</returns>
        public static float FromDistance(float distance, float min, float max)
        {
            return Mathf.Clamp01((distance - min) / (max - min));
        }

        /// <summary>
        /// Returns the interpolation value covered by a given inverse range.
        /// Ensure that max != min to avoid dividing by zero
        /// </summary>
        /// <param name="a">Either of the pair of points</param>
        /// <param name="b">Either of the pair of points</param>
        /// <param name="min">the distance at which the coefficient will be 0</param>
        /// <param name="max">the distance at which the coefficient will be 1</param>
        /// <returns>the interpolation value covered by a given inverse range</returns>
        public static float FromInverseDistance(Vector3 a, Vector3 b, float max, float min)
        {
            return Mathf.Clamp01((max - Vector3.Distance(a, b)) / (max - min));
        }
    }
}
