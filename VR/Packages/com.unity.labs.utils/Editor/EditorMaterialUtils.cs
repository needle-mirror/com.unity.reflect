using System.Globalization;
using UnityEngine;

namespace Unity.Labs.Utils
{
    /// <summary>
    /// Editor only Material utilities
    /// </summary>
    public static class EditorMaterialUtils
    {
        /// <summary>
        /// Used to get editor preference colors setting
        /// </summary>
        /// <param name="pref">Name of color preference inf the from of `EditorPrefs.GetString("HEADER/PARAMETER")`</param>
        /// <returns>Color form Unity Editor Preferences</returns>
        public static Color PrefToColor(string pref)
        {
            var split = pref.Split(';');
            if (split.Length != 5)
            {
                Debug.LogWarningFormat("Parsing PrefColor failed on {0}", pref);
                return default(Color);
            }

            split[1] = split[1].Replace(',', '.');
            split[2] = split[2].Replace(',', '.');
            split[3] = split[3].Replace(',', '.');
            split[4] = split[4].Replace(',', '.');
            float r, g, b, a;
            var success = float.TryParse(split[1], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out r);
            success &= float.TryParse(split[2], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out g);
            success &= float.TryParse(split[3], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out b);
            success &= float.TryParse(split[4], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out a);

            if (success)
                return new Color(r, g, b, a);

            Debug.LogWarningFormat("Parsing PrefColor failed on {0}", pref);
            return default(Color);
        }

        public static string ColorToColorPref(string path, Color value)
        {
            var colorString = string.Format("{0:0.000};{1:0.000};{2:0.000};{3:0.000}", value.r, value.g, value.b, value.a).Replace('.', ',');
            return string.Format("{0};{1}", path, colorString);
        }
    }
}
