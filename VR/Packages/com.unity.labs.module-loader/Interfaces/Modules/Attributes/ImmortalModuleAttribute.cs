using System;

namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Decorate a module which can never be deactivated
    /// </summary>
    public class ImmortalModuleAttribute : Attribute
    {
        public ImmortalModuleAttribute() { }
    }
}
