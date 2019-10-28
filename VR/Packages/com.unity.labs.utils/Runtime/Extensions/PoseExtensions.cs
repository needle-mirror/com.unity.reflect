using UnityEngine;

namespace Unity.Labs.Utils
{
    public static class PoseExtensions
    {
        /// <summary>
        /// Offsets the given pose by this parent pose
        /// </summary>
        /// <param name="pose">The pose which will be used to apply the offset</param>
        /// <param name="otherPose">The pose that will be offset</param>
        /// <returns>A pose offset by <paramref name="pose"/></returns>
        public static Pose ApplyOffsetTo(this Pose pose, Pose otherPose)
        {
            var rotation = pose.rotation;
            return new Pose(
                rotation * otherPose.position + pose.position,
                rotation * otherPose.rotation);
        }

        /// <summary>
        /// Offsets the given position by this pose
        /// </summary>
        /// <param name="pose">The pose which will be used to apply the offset</param>
        /// <param name="position">The position which will be offset</param>
        /// <returns>A position offset by <paramref name="pose"/></returns>
        public static Vector3 ApplyOffsetTo(this Pose pose, Vector3 position)
        {
            return pose.rotation * position + pose.position;
        }

        /// <summary>
        /// Offsets the given position by the inverse of this pose
        /// </summary>
        /// <param name="pose">The pose which will be used to apply the offset</param>
        /// <param name="position">The position which will be offset</param>
        /// <returns>A position offset by the inverse of <paramref name="pose"/></returns>
        public static Vector3 ApplyInverseOffsetTo(this Pose pose, Vector3 position)
        {
            return Quaternion.Inverse(pose.rotation) * (position - pose.position);
        }

        /// <summary>
        /// Translates this pose by <paramref name="translation"/>, relative to this pose's local axes
        /// </summary>
        /// <param name="pose">The pose to which the translation should be applied</param>
        /// <param name="translation">Positional offset to apply to the pose</param>
        /// <returns>A pose translated in local space by <paramref name="translation"/></returns>
        public static Pose TranslateLocal(this Pose pose, Vector3 translation)
        {
            pose.position = pose.position + pose.rotation * translation;
            return pose;
        }
    }
}
