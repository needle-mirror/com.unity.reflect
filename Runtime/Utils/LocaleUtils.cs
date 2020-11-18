using System.Runtime.InteropServices;
using Unity.Reflect.Utils;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace UnityEngine.Reflect
{
    public static class LocaleUtils
    {
#if UNITY_IOS && !UNITY_EDITOR
        private const string k_DefaultSetting = "Default";
        private static readonly string k_AutoSetting = "Auto";
        private static readonly string k_ChinaSetting = "China (中国)";
        
        [DllImport("__Internal")]
        private static extern string GetUserLocale();

        [DllImport("__Internal")]
        private static extern string GetRegionalSettings();
#endif
#if UNITY_EDITOR
        private static readonly string k_ChinaBranch = "china";
#else
        private static readonly string k_ChinaLocale = "cn";
        private static readonly string k_DefaultLocale = "us";
#endif

        public static RegionUtils.Provider GetProvider()
        {
            return
#if UNITY_IOS && !UNITY_EDITOR
                GetiOSProvider();
#elif UNITY_ANDROID && !UNITY_EDITOR
                RegionUtils.Provider.GCP; // Android app is not available in China
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
                GetWindowsProvider();
#elif UNITY_STANDALONE_OSX && !UNITY_EDITOR
                RegionUtils.Provider.GCP; // macOS Viewer is not available in China
#elif UNITY_EDITOR
                GetEditorProvider();
#else
                GetFallbackProvider();
#endif
        }
 
#if UNITY_IOS && !UNITY_EDITOR
        private static RegionUtils.Provider GetiOSProvider()
        {
            var userRegionalSetting = GetRegionalSettings();
            string locale;
            if (userRegionalSetting == k_AutoSetting)
            {
                locale = GetUserLocale();
            }

            else if (userRegionalSetting == k_DefaultSetting)
            {
                locale = k_DefaultLocale;
            }

            else if (userRegionalSetting == k_ChinaSetting)
            {
                locale = k_ChinaLocale;
            }
            else
            {
                locale = k_DefaultLocale;
            }

            return locale.ToLower().EndsWith(k_ChinaLocale) ? RegionUtils.Provider.Tencent : RegionUtils.Provider.GCP;
        }
#elif UNITY_STANDALONE_WIN && !UNITY_EDITOR
        private static RegionUtils.Provider GetWindowsProvider()
        {
            return RegionUtils.GetProvider();
        }
#elif UNITY_EDITOR
        private static RegionUtils.Provider GetEditorProvider()
        {
            return InternalEditorUtility.GetUnityBuildBranch().Contains(k_ChinaBranch) 
                ? RegionUtils.Provider.Tencent
                : RegionUtils.Provider.GCP;
        }
#else
        private static RegionUtils.Provider GetFallbackProvider()
        {
            return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName.ToLower().Equals(k_ChinaLocale)
                ? RegionUtils.Provider.Tencent
                : RegionUtils.Provider.GCP;
        }
#endif
    }
}
