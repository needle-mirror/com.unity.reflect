using Unity.Labs.ModuleLoader;
using UnityEngine;

namespace Unity.Labs.EditorXR.Interfaces
{
    /// <summary>
    /// Gives decorated class the ability to get the preview origins
    /// </summary>
    public interface IUsesGetPreviewOrigin : IFunctionalitySubscriber<IProvidesGetPreviewOrigin>
    {
    }

    public static class UsesGetPreviewOriginMethods
    {
        /// <summary>
        /// Get the preview transform attached to the given rayOrigin
        /// </summary>
        /// <param name="user">The functionality user</param>
        /// <param name="rayOrigin">The rayOrigin where the preview will occur</param>
        /// <returns>The preview origin</returns>
        public static Transform GetPreviewOriginForRayOrigin(this IUsesGetPreviewOrigin user, Transform rayOrigin)
        {
#if FI_AUTOFILL
            return default(Transform);
#else
            return user.provider.GetPreviewOriginForRayOrigin(rayOrigin);
#endif
        }
    }
}
