using System;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Suggests the order for this module in the list of scene callback modules
    /// This affects the order in which scene callbacks are called
    /// </summary>
    public class ModuleSceneCallbackOrderAttribute : ModuleOrderAttribute
    {
        public ModuleSceneCallbackOrderAttribute(int order) : base(order) { }
    }
}
