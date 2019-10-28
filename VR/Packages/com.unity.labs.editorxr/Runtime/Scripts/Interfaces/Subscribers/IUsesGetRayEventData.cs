using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR.Modules;
using UnityEngine;

namespace Unity.Labs.EditorXR.Interfaces
{
    /// <summary>
    /// Gives decorated class the ability to add get pointer event data  from the system
    /// </summary>
    interface IUsesGetRayEventData : IFunctionalitySubscriber<IProvidesGetRayEventData>
    {
    }

    static class UsesGetRayEventData
    {
        public static RayEventData GetRayEventData(this IUsesGetRayEventData user, Transform rayOrigin)
        {
#if FI_AUTOFILL
            return default(RayEventData);
#else
            return user.provider.GetPointerEventData(rayOrigin);
#endif
        }
    }
}
