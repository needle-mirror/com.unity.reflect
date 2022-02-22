using System.IO;
using Unity.Reflect.IO;

namespace UnityEngine.Reflect
{
    public static class Storage
    {
        public static PlayerStorage main { get; }
        public static PlayerStorage cache { get; } 
        
        static Storage()
        {
            var root = ProjectServer.ProjectDataPath;
            main = new PlayerStorage(Path.Combine(root, "Storage"), true, false);
            cache = new PlayerStorage(Path.Combine(root, "Cache"), true, false);
        }
    }
}
