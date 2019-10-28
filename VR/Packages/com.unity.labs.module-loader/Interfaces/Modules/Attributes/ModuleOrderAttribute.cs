using System;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Suggests the order for this module in the list of all modules
    /// This affects the order in which Load and Unload are called
    /// </summary>
    public class ModuleOrderAttribute : Attribute
    {
        public int order { get; private set; }

        public ModuleOrderAttribute(int order)
        {
            this.order = order;
        }
    }
}
