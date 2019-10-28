using Unity.Labs.ModuleLoader;
using UnityEditor.Experimental.EditorVR.Core;

namespace Unity.Labs.EditorXR.Interfaces
{
    /// <summary>
    /// Gives decorated class ability to control haptic feedback
    /// </summary>
    public interface IUsesControlHaptics : IFunctionalitySubscriber<IProvidesControlHaptics>
    {
    }

    public static class UsesControlHapticsMethods
    {
        /// <summary>
        /// Perform a haptic feedback pulse
        /// </summary>
        /// <param name="user">The functionality user</param>
        /// <param name="node">Node on which to control the pulse</param>
        /// <param name="hapticPulse">Haptic pulse to perform</param>
        /// <param name="durationMultiplier">(Optional) Multiplier value applied to the hapticPulse duration</param>
        /// <param name="intensityMultiplier">(Optional) Multiplier value applied to the hapticPulse intensity</param>
        public static void Pulse(this IUsesControlHaptics user, Node node, HapticPulse hapticPulse, float durationMultiplier = 1f, float intensityMultiplier = 1f)
        {
#if !FI_AUTOFILL
            user.provider.Pulse(node, hapticPulse, durationMultiplier, intensityMultiplier);
#endif
        }

        /// <summary>
        /// Stop all haptic feedback on a specific device, or all devices
        /// </summary>
        /// <param name="user">The functionality user</param>
        /// <param name="node">Device RayOrigin/Transform on which to stop all pulses. A NULL value will stop pulses on all devices</param>
        public static void StopPulses(this IUsesControlHaptics user, Node node)
        {

#if !FI_AUTOFILL
            user.provider.StopPulses(node);
#endif
        }
    }
}
