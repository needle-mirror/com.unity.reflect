using UnityEngine;

namespace Unity.Labs.Utils
{
    public static class TransformExtensions
    {
        /// <summary>
        /// Gets the local position and rotation as a Pose
        /// </summary>
        /// <param name="transform">The transform from which to get the pose</param>
        public static Pose GetLocalPose(this Transform transform)
        {
            return new Pose(transform.localPosition, transform.localRotation);
        }

        /// <summary>
        /// Gets the world position and rotation as a Pose
        /// </summary>
        /// <param name="transform">The transform from which to get the pose</param>
        public static Pose GetWorldPose(this Transform transform)
        {
            return new Pose(transform.position, transform.rotation);
        }

        /// <summary>
        /// Sets the local position and rotation from a Pose
        /// </summary>
        /// <param name="transform">The transform on which to set the pose</param>
        /// <param name="pose">Pose specifying the new position and rotation</param>
        public static void SetLocalPose(this Transform transform, Pose pose)
        {
            transform.localPosition = pose.position;
            transform.localRotation = pose.rotation;
        }

        /// <summary>
        /// Sets the world position and rotation from a Pose
        /// </summary>
        /// <param name="transform">The transform on which to set the pose</param>
        /// <param name="pose">Pose specifying the new position and rotation</param>
        public static void SetWorldPose(this Transform transform, Pose pose)
        {
            transform.position = pose.position;
            transform.rotation = pose.rotation;
        }
    }
}
