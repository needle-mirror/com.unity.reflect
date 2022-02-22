using System.Collections.Generic;

namespace UnityEditor.Reflect
{
    public static class AppLinksHelper
    {
        public static readonly List<string> SupportedDomains = new List<string>
        {
            "test.reflect.unity3d.com",
            "ci-stg.reflect.unity3d.com",
            "stg.reflect.unity3d.com",
            "reflect.unity3d.com",
            "test.reflect.unity.cn",
            "stg.reflect.unity.cn",
            "reflect.unity.cn"
        };
        
        public static readonly List<string> AppLinksDomains = new List<string>
        {
            "*.reflect.unity3d.com",
            "*.reflect.unity.cn"
        };
    }
}