using Unity.Reflect.Utils;


namespace UnityEngine.Reflect
{
    public static class AuthConfiguration
    {
        public static readonly string RegistryUrlProtocol = "URL Protocol";
        public static readonly string RegistryShellKey = @"shell\open\command";

        public static readonly string UriScheme = "reflect";
        public static readonly string JwtTokenFileName = "jwttoken.data";
        public static readonly string JwtParamName = "?jwt=";

        public static string LoginUrl => GenesisUtils.GetLoginUrl(LocaleUtils.GetProvider()).ToString();
        public static string LogoutUrl => GenesisUtils.GetLogoutUrl(LocaleUtils.GetProvider()).ToString();
    }
}