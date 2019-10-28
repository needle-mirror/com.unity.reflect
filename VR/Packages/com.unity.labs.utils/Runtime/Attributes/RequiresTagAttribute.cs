#if UNITY_EDITOR
using System;

namespace Unity.Labs.Utils
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public sealed class RequiresTagAttribute : Attribute
    {
        public string tag;

        public RequiresTagAttribute(string tag)
        {
            this.tag = tag;
        }
    }
}
#endif
