using System;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Suggests the order for this module in the list of build callback modules
    /// This affects the order in which build callbacks are called
    /// </summary>
    public class ModuleBuildCallbackOrderAttribute : ModuleOrderAttribute
    {
        public ModuleBuildCallbackOrderAttribute(int order) : base (order) { }
    }
}
