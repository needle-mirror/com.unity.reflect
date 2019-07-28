using System;

namespace UnityEngine.Reflect
{
    public struct ListControlItemData
    {
        [Flags]
        public enum Option {
            None = 0,
            Open = 1 << 0,
            LocalFiles = 1 << 1,
            Download = 1 << 2,
            UpToDate = 1 << 3,
            Connected = 1 << 4
        }

        public string id;
        public string title;
        public string description;
        public string projectId;
        public DateTime date;
        public Option options;
        public Sprite image;
        public bool enabled;
        public object payload;
    }
}
