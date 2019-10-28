using System;
using Unity.Labs.EditorXR.Interfaces;
using UnityEngine;

namespace UnityEditor.Experimental.EditorVR.Core
{
    /// <summary>
    /// Provide the ability to request a corresponding ray origin for a node
    /// </summary>
    public interface INodeToRay
    {
    }

    public static class INodeToRayMethods
    {
        internal static Func<Node, Transform> requestRayOriginFromNode { private get; set; }

        /// <summary>
        /// Get the corresponding ray origin for a given node
        /// </summary>
        /// <param name="node">The node to request a ray origin for</param>
        public static Transform RequestRayOriginFromNode(this INodeToRay obj, Node node)
        {
            return requestRayOriginFromNode(node);
        }
    }
}
