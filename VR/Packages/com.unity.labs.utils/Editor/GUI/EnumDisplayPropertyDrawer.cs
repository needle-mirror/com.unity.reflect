using System;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.Utils.GUI
{
    [CustomPropertyDrawer(typeof(EnumDisplayAttribute))]
    sealed class EnumDisplayPropertyDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var enumDisplayAttribute = (EnumDisplayAttribute)attribute;
            var currentEnumValue = property.intValue;
            property.intValue = EditorGUI.IntPopup(position, label.text, currentEnumValue, enumDisplayAttribute.names, enumDisplayAttribute.values);
        }
    }
}
