using System;

namespace Unity.Reflect.Actor
{
    public class ActorAttribute : Attribute
    {
        public string Id { get; }
        public bool IsBoundToMainThread { get; }
        public string GroupName { get; }
        public string DisplayName { get; }

        public ActorAttribute() { }
        public ActorAttribute(string guid = null, bool isBoundToMainThread = false, string groupName = null, string displayName = null)
        {
            Id = guid;
            if (guid != null && !Guid.TryParse(guid, out  _))
                throw new ArgumentException($"{nameof(guid)} must be convertible to {nameof(Guid)}");

            IsBoundToMainThread = isBoundToMainThread;
            GroupName = groupName;
            DisplayName = displayName;
        }
    }
}
