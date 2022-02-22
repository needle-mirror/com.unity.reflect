using System.Numerics;
using System.Runtime.CompilerServices;

namespace Unity.Reflect
{
	public static class Matrix4x4Extensions
	{
		public static Matrix4x4 TRS(Vector3 position, Quaternion rotation, Vector3 scale)
		{
			var matrix4x4 = Matrix4x4.Identity;

			matrix4x4.M11 = (1f - 2f * (rotation.Y * rotation.Y + rotation.Z * rotation.Z)) * scale.X;
			matrix4x4.M12 = (rotation.X * rotation.Y + rotation.Z * rotation.W) * scale.X * 2f;
			matrix4x4.M13 = (rotation.X * rotation.Z - rotation.Y * rotation.W) * scale.X * 2f;
			matrix4x4.M14 = 0f;
			matrix4x4.M21 = (rotation.X * rotation.Y - rotation.Z * rotation.W) * scale.Y * 2f;
			matrix4x4.M22 = (1f - 2f * (rotation.X * rotation.X + rotation.Z * rotation.Z)) * scale.Y;
			matrix4x4.M23 = (rotation.Y * rotation.Z + rotation.X * rotation.W) * scale.Y * 2f;
			matrix4x4.M24 = 0f;
			matrix4x4.M31 = (rotation.X * rotation.Z + rotation.Y * rotation.W) * scale.Z * 2f;
			matrix4x4.M32 = (rotation.Y * rotation.Z - rotation.X * rotation.W) * scale.Z * 2f;
			matrix4x4.M33 = (1f - 2f * (rotation.X * rotation.X + rotation.Y * rotation.Y)) * scale.Z;
			matrix4x4.M34 = 0f;
			matrix4x4.M41 = position.X;
			matrix4x4.M42 = position.Y;
			matrix4x4.M43 = position.Z;
			matrix4x4.M44 = 1f;

			return matrix4x4;
		}

		public static Vector3 MultiplyPoint3x4(this Matrix4x4 matrix, Vector3 point)
		{
			Vector3 vector3;
			vector3.X = (float) ((double) matrix.M11 * (double) point.X + (double) matrix.M21 * (double) point.Y + (double) matrix.M31 * (double) point.Z) + matrix.M41;
			vector3.Y = (float) ((double) matrix.M12 * (double) point.X + (double) matrix.M22 * (double) point.Y + (double) matrix.M32 * (double) point.Z) + matrix.M42;
			vector3.Z = (float) ((double) matrix.M13 * (double) point.X + (double) matrix.M23 * (double) point.Y + (double) matrix.M33 * (double) point.Z) + matrix.M43;

			return vector3;
		}
		
		public static UnityEngine.Matrix4x4 ToUnity(this Matrix4x4 matrix)
		{
			return Unsafe.As<Matrix4x4, UnityEngine.Matrix4x4>(ref matrix);
		}
		
		public static Matrix4x4 ToNumerics(this UnityEngine.Matrix4x4 matrix)
		{
			return Unsafe.As<UnityEngine.Matrix4x4, Matrix4x4>(ref matrix);
		}
	}
}