using System;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Suggests the order for this module in the list of update modules
    /// This affects the order in which update is called
    /// </summary>
    public class ModuleBehaviorCallbackOrderAttribute : ModuleOrderAttribute
    {
        public ModuleBehaviorCallbackOrderAttribute(int order) : base(order) { }
    }
}
