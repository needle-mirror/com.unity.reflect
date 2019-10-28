#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.Utils.GUI
{
    public static class ScreenGUIUtils
    {
        /// <summary>
        /// Gets the width of the screen, in points (pixels at 100% DPI)
        /// </summary>
        public static float pointWidth
        {
            get { return Screen.width / EditorGUIUtility.pixelsPerPoint; }
        }

        /// <summary>
        /// Gets the height of the screen, in points (pixels at 100% DPI)
        /// </summary>
        public static float pointHeight
        {
            get { return Screen.height / EditorGUIUtility.pixelsPerPoint; }
        }
    }
}
#endif
