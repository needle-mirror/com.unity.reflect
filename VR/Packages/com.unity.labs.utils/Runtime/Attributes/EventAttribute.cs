using System;

namespace Unity.Labs.Utils
{
    public sealed class EventAttribute : Attribute
    {
        public readonly Type type;

        public EventAttribute(Type type)
        {
            this.type = type;
        }
    }
}
