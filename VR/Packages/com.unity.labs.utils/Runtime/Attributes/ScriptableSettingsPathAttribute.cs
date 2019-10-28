using System;

namespace Unity.Labs.Utils
{
    /// <summary>
    /// Allows a class inheriting from <see cref="ScriptableSettings{T}"/> to specify that its instance Asset
    /// should be saved under "Assets/[<see cref="path"/>]/Resources/ScriptableSettings/".
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ScriptableSettingsPathAttribute : Attribute
    {
        public string path { get; private set; }

        public ScriptableSettingsPathAttribute(string path = "")
        {
            this.path = path;
        }
    }
}
