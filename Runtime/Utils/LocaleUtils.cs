using System;
using System.Runtime.InteropServices;
using Unity.Reflect;
using Unity.Reflect.Utils;
#if UNITY_EDITOR
using UnityEditorInternal;
#endif

namespace UnityEngine.Reflect
{
    public struct EnvironmentInfo
    {
        public RegionUtils.Provider provider;
        public CloudEnvironment cloudEnvironment;
        public string customUrl;
    }

    public static class LocaleUtils
    {
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string GetUserLocale();
#endif
#if UNITY_EDITOR
        private static readonly string k_ChinaBranch = "china";
#else
        private static readonly string k_ChinaLocale = "cn";
        private static readonly string k_DefaultLocale = "us";
#endif

        public class SettingsKeys
        {
            public static string Provider => "Cloud_Provider";
            public static string CloudEnvironment => "Cloud_Environment";
            public static string CustomURL => "Cloud_CustomUrl";
        }

        public static EnvironmentInfo GetEnvironmentInfo()
        {
            EnvironmentInfo info = new EnvironmentInfo();
            info.provider = GetProvider();
            info.cloudEnvironment = GetCloudEnvironment();
            info.customUrl = GetCustomUrl();
            return info;
        }
        
        public static void SaveEnvironmentInfo(EnvironmentInfo info)
        {
            SetProvider(info.provider);
            SetCloudEnvironment(info.cloudEnvironment);
            SetCustomUrl(info.customUrl);
        }

        public static void DeleteCloudEnvironmentSetting()
        {
            PlayerPrefs.DeleteKey( SettingsKeys.CloudEnvironment);
        }

        public static RegionUtils.Provider GetProvider()
        {
            if (PlayerPrefs.HasKey(SettingsKeys.Provider))
            {
                return (RegionUtils.Provider) System.Enum.Parse(typeof(RegionUtils.Provider),
                    PlayerPrefs.GetString(SettingsKeys.Provider));
            }
            
            return GetDefaultProvider();
        }

        private static void SetProvider(RegionUtils.Provider infoProvider)
        {
            PlayerPrefs.SetString(SettingsKeys.Provider, infoProvider.ToString());
        }

        private static CloudEnvironment GetCloudEnvironment()
        {
            if (PlayerPrefs.HasKey(SettingsKeys.CloudEnvironment))
            {
                return (CloudEnvironment) System.Enum.Parse(typeof(CloudEnvironment),
                    PlayerPrefs.GetString(SettingsKeys.CloudEnvironment));
            }

            return CloudConfiguration.GetEnvironment();
        }
        
        private static void SetCloudEnvironment(CloudEnvironment infoCloudEnvironment)
        {
            PlayerPrefs.SetString(SettingsKeys.CloudEnvironment, infoCloudEnvironment.ToString());
        }

        private static string GetCustomUrl()
        {
            return PlayerPrefs.GetString(SettingsKeys.CustomURL);
        }
        
        private static void SetCustomUrl(string infoCustomUrl)
        {
            PlayerPrefs.SetString(SettingsKeys.CustomURL, infoCustomUrl);
        }
        

        private static RegionUtils.Provider GetDefaultProvider()
        {
            return 
#if UNITY_IOS && !UNITY_EDITOR
                GetiOSProvider();
#elif UNITY_EDITOR
                GetEditorProvider();
#else
                GetFallbackProvider();
#endif
        }
 
#if UNITY_IOS && !UNITY_EDITOR
        private static RegionUtils.Provider GetiOSProvider()
        {
            string locale = GetUserLocale();
            return locale.ToLower().EndsWith(k_ChinaLocale) ? RegionUtils.Provider.Tencent : RegionUtils.Provider.GCP;
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
