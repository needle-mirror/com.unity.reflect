#if UNITY_EDITOR
using System;

namespace Unity.Labs.Utils
{
    public sealed class RequiresLayerAttribute : Attribute
    {
        public string layer;

        public RequiresLayerAttribute(string layer)
        {
            this.layer = layer;
        }
    }
}
#endif
