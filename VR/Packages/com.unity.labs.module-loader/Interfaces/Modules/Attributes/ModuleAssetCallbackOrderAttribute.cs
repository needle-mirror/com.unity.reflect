using System;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Suggests the order for this module in the list of asset callback modules
    /// This affects the order in which asset callbacks are called
    /// </summary>
    public class ModuleAssetCallbackOrderAttribute : ModuleOrderAttribute
    {
        public ModuleAssetCallbackOrderAttribute(int order) : base(order) { }
    }
}
