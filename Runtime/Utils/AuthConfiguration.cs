using System;
using Unity.Reflect.Utils;
using UnityEngine.Reflect;
using Unity.Reflect.Runtime;

namespace UnityEngine.Reflect
{
    public static class AuthConfiguration
    {
        public static readonly string RegistryUrlProtocol = "URL Protocol";
        public static readonly string RegistryShellKey = @"shell\open\command";

        public static readonly string UriScheme = "reflect";
        public static readonly string JwtTokenFileName = "jwttoken.data";
        public static readonly string JwtArgsName = "jwt";

        public static string LoginUrl => GenesisUtils.GetLoginUrl(LocaleUtils.GetProvider()).ToString();
#if UNITY_IOS || UNITY_ANDROID
        // TODO replace with redirect uri to https://reflect.unity3d.com/logout once Manifests are upated and deployed
        public static string LogoutUrl => GenesisUtils.GetLogoutUrl(LocaleUtils.GetProvider(), (LocaleUtils.GetProvider().Equals(RegionUtils.Provider.GCP) ? new Uri("https://reflect.unity3d.com/p/logout") : new Uri("https://reflect.unity.cn/p/logout"))).ToString();
#else
        public static string LogoutUrl => GenesisUtils.GetLogoutUrl(LocaleUtils.GetProvider()).ToString();
#endif

    }
}