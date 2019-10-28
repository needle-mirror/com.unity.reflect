using UnityEngine;

namespace Unity.Labs.Utils
{
    public static class CameraExtensions
    {
        const float k_OneOverSqrt2 = 0.707106781f;

        public static float GetVerticalFOV(this Camera camera, float aspectNeutralFOV)
        {
            var verticalHalfFovTangent = Mathf.Tan(aspectNeutralFOV * 0.5f * Mathf.Deg2Rad) *
                k_OneOverSqrt2 / Mathf.Sqrt(camera.aspect);
            return Mathf.Atan(verticalHalfFovTangent) * 2 * Mathf.Rad2Deg;
        }

        public static float GetHorizontalFOV(this Camera camera)
        {
            var halfFov = camera.fieldOfView * 0.5f;
            return Mathf.Rad2Deg * Mathf.Atan(Mathf.Tan(halfFov * Mathf.Deg2Rad) * camera.aspect);
        }

        public static float GetVerticalOrthoSize(this Camera camera, float size)
        {
            return size * k_OneOverSqrt2 / Mathf.Sqrt(camera.aspect);
        }
    }
}
