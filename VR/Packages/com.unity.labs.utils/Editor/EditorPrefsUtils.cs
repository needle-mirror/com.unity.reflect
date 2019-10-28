#if NET_4_6
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.Utils
{
    /// <summary>
    /// Utilities for getting and setting editor preferences that caches the values of those preferences.
    /// </summary>
    public static class EditorPrefsUtils
    {
        static readonly Dictionary<string, object> k_EditorPrefsValueSessionCache = new Dictionary<string, object>();

        /// <summary>
        /// Gets the Editor Preference Key by combining the parent object type's full name and the property name
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="propertyName">Name of calling property</param>
        /// <returns>Editor Preference Key for property</returns>
        public static string GetPrefKey(string typeName, string propertyName)
        {
            return $"{typeName}.{propertyName}";
        }

        /// <summary>
        /// Get the bool value stored in the Editor Preferences for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="defaultValue">Value to be used as default.</param>
        /// <param name="propertyName">Name of calling Property</param>
        /// <returns>The bool value stored in the Editor Preferences for the calling property.</returns>
        public static bool GetBool(string typeName, bool defaultValue = false,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            return GetEditorPrefsValueOrDefault(prefsKey, defaultValue);
        }

        /// <summary>
        /// Sets the bool value to the Editor Preferences stored value for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="value">Value to set in Editor Preferences</param>
        /// <param name="propertyName">Name of calling Property</param>
        public static void SetBool(string typeName, bool value,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            SetEditorPrefsValue(prefsKey, value);
        }

        /// <summary>
        /// Get the float value stored in the Editor Preferences for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="defaultValue">Value to be used as default.</param>
        /// <param name="propertyName">Name of calling Property</param>
        /// <returns>The float value stored in the Editor Preferences for the calling property.</returns>
        public static float GetFloat(string typeName, float defaultValue = 0f,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            return GetEditorPrefsValueOrDefault(prefsKey, defaultValue);
        }

        /// <summary>
        /// Sets the float value to the Editor Preferences stored value for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="value">Value to set in Editor Preferences</param>
        /// <param name="propertyName">Name of calling Property</param>
        public static void SetFloat(string typeName, float value,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            SetEditorPrefsValue(prefsKey, value);
        }

        /// <summary>
        /// Get the int value stored in the Editor Preferences for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="defaultValue">Value to be used as default.</param>
        /// <param name="propertyName">Name of calling Property</param>
        /// <returns>The int value stored in the Editor Preferences for the calling property.</returns>
        public static int GetInt(string typeName, int defaultValue = 0,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            return GetEditorPrefsValueOrDefault(prefsKey, defaultValue);
        }

        /// <summary>
        /// Sets the int value to the Editor Preferences stored value for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="value">Value to set in Editor Preferences</param>
        /// <param name="propertyName">Name of calling Property</param>
        public static void SetInt(string typeName, int value,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            SetEditorPrefsValue(prefsKey, value);
        }

        /// <summary>
        /// Get the string value stored in the Editor Preferences for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="defaultValue">Value to be used as default.</param>
        /// <param name="propertyName">Name of calling Property</param>
        /// <returns>The string value stored in the Editor Preferences for the calling property.</returns>
        public static string GetString(string typeName, string defaultValue = "",
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            return GetEditorPrefsValueOrDefault(prefsKey, defaultValue);
        }

        /// <summary>
        /// Sets the string value to the Editor Preferences stored value for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="value">Value to set in Editor Preferences</param>
        /// <param name="propertyName">Name of calling Property</param>
        public static void SetString(string typeName, string value,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            SetEditorPrefsValue(prefsKey, value);
        }

        /// <summary>
        /// Get the color value stored in the Editor Preferences for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="defaultValue">Value to be used as default.</param>
        /// <param name="propertyName">Name of calling Property</param>
        /// <returns>The color value stored in the Editor Preferences for the calling property.</returns>
        public static Color GetColor(string typeName, Color defaultValue,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            return GetEditorPrefsValueOrDefault(prefsKey, defaultValue);
        }

        /// <summary>
        /// Sets the color value to the Editor Preferences stored value for the calling property.
        /// </summary>
        /// <param name="typeName">The name of the type which defines the property</param>
        /// <param name="value">Value to set in Editor Preferences</param>
        /// <param name="propertyName">Name of calling Property</param>
        public static void SetColor(string typeName, Color value,
            [CallerMemberName] string propertyName = null)
        {
            var prefsKey = GetPrefKey(typeName, propertyName);
            SetEditorPrefsValue(prefsKey, value);
        }

        /// <summary>
        /// Rests the cached Editor Prefs Values stored in the Editor Prefs Utils
        /// </summary>
        public static void RestEditorPrefsValueSessionCache()
        {
            k_EditorPrefsValueSessionCache.Clear();
        }

        static void SetEditorPrefsValue<T>(string prefsKey, T value)
        {
            T cachedValue;
            if (TryGetCachedEditorPrefsValue(prefsKey, out cachedValue) && cachedValue.Equals(value))
                return;

            var type = typeof(T);

            if (type == typeof(bool))
            {
                EditorPrefs.SetBool(prefsKey, (bool)(object)value);
            }
            else if (type == typeof(int) && value is int)
            {
                EditorPrefs.SetInt(prefsKey, (int)(object)value);
            }
            else if (type == typeof(float) && value is float)
            {
                EditorPrefs.SetFloat(prefsKey, (float)(object)value);
            }
            else if (type == typeof(string) && value is string)
            {
                EditorPrefs.SetString(prefsKey, (string)(object)value);
            }
            else if (type.IsAssignableFromOrSubclassOf(typeof(Enum))
                && value.GetType().IsAssignableFromOrSubclassOf(typeof(Enum)))
            {
                EditorPrefs.SetInt(prefsKey, (int)(object)value);
            }
            else if (type == typeof(Color) && value is Color)
            {
                EditorPrefs.SetString(prefsKey,EditorMaterialUtils.ColorToColorPref(prefsKey, (Color)(object)value));
            }
            else
            {
                Debug.LogError(string.Format("Could not set Editor Preference Value of type : {0} with value {1} !",
                    type, value));
                return;
            }

            if (k_EditorPrefsValueSessionCache.ContainsKey(prefsKey))
                k_EditorPrefsValueSessionCache[prefsKey] = value;
            else
                k_EditorPrefsValueSessionCache.Add(prefsKey, value);
        }

        static void GetEditorPrefsValue<T>(string prefsKey, out T prefValue)
        {
            if (TryGetCachedEditorPrefsValue(prefsKey, out prefValue))
                return;

            var type = typeof(T);
            var prefsSet = false;
            if (type == typeof(bool))
            {
                prefValue = (T)(object)EditorPrefs.GetBool(prefsKey);
                prefsSet = true;
            }
            else if (type == typeof(int))
            {
                prefValue = (T)(object)EditorPrefs.GetInt(prefsKey);
                prefsSet = true;
            }
            else if (type == typeof(float))
            {
                prefValue = (T)(object)EditorPrefs.GetFloat(prefsKey);
                prefsSet = true;
            }
            else if (type == typeof(string))
            {
                prefValue = (T)(object)EditorPrefs.GetString(prefsKey);
                prefsSet = true;
            }
            else if (type.IsAssignableFromOrSubclassOf(typeof(Enum)))
            {
                prefValue = (T)(object)EditorPrefs.GetInt(prefsKey);
                prefsSet = true;
            }
            else if (type == typeof(Color))
            {
                prefValue = (T)(object)EditorMaterialUtils.PrefToColor(EditorPrefs.GetString(prefsKey));
                prefsSet = true;
            }
            else
            {
                Debug.LogError(string.Format("Could not get Editor Preference Default of type : {0} Type is not supported!",
                    type));
            }

            if (prefsSet && prefValue != null)
            {
                SetEditorPrefsValue(prefsKey, prefValue);
                return;
            }

            SetEditorPrefsValue(prefsKey, default(T));
            prefValue = default(T);
        }

        static bool TryGetCachedEditorPrefsValue<T>(string prefsKey, out T prefValue)
        {
            object cachedObj;
            if (k_EditorPrefsValueSessionCache.TryGetValue(prefsKey, out cachedObj))
            {
                if (cachedObj is T || cachedObj.GetType().IsAssignableFromOrSubclassOf(typeof(T)))
                {
                    prefValue = (T)cachedObj;
                    return true;
                }
            }

            prefValue = default(T);
            return false;
        }

        static T GetEditorPrefsValueOrDefault<T>(string prefsKey, T defaultValue = default(T))
        {
            var value = defaultValue;
            if (!EditorPrefs.HasKey(prefsKey))
                SetEditorPrefsValue(prefsKey, value);
            else
                GetEditorPrefsValue(prefsKey, out value);

            return value;
        }
    }
}
#endif
