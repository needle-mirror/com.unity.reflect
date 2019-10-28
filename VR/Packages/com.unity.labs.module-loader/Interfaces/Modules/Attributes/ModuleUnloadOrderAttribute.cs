namespace Unity.Labs.ModuleLoader
{
    /// <summary>
    /// Suggests the order for this module during unloading
    /// </summary>
    public class ModuleUnloadOrderAttribute : ModuleOrderAttribute
    {
        public ModuleUnloadOrderAttribute(int order) : base (order) { }
    }
}
