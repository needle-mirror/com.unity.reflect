using System;
using System.Collections.Generic;
using Unity.Labs.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.ModuleLoader
{
    [CustomPropertyDrawer(typeof(FunctionalityIsland.DefaultProvider))]
    public class DefaultProviderPropertyDrawer : PropertyDrawer
    {
        static readonly string[] k_DefaultOptions = { };
        static readonly string[] k_SerializableProviderTypeNames;
        static readonly string[] k_ReadableProviderTypeNames;

        static readonly string[] k_AllReadableProviderTypeNames;
        static readonly string[] k_AllSerializableProviderTypeNames;
        static readonly List<string[]> k_AllSerializableImplementorNames = new List<string[]>();
        static readonly List<string[]> k_AllReadableImplementorNames = new List<string[]>();

        // Local method use only -- created here to reduce garbage collection. Collections must be cleared before use
        // Reference type collections must also be cleared after use
        static readonly string[] k_TempSerializableIProviderTypeNames = new string[1];
        static readonly string[] k_TempReadableProviderTypeNames = new string[1];

        static DefaultProviderPropertyDrawer()
        {
            var serializableProviderTypeNames = new List<string>();
            var readableProviderTypeNames = new List<string>();

            var allSerializableProviderTypeNames = new List<string>();
            var allReadableProviderTypeNames = new List<string>();

            var implementors = new List<Type>();
            ReflectionUtils.ForEachType(t =>
            {
                if (typeof(IFunctionalityProvider).IsAssignableFrom(t) && t.IsInterface && t != typeof(IFunctionalityProvider))
                {
                    var serializableName = t.FullName;
                    var readableName = t.GetNameWithGenericArguments();
                    allSerializableProviderTypeNames.Add(serializableName);
                    allReadableProviderTypeNames.Add(readableName);
                    implementors.Clear();
                    t.GetImplementationsOfInterface(implementors);

                    var count = implementors.Count;
                    var serializableNames = new string[count];
                    var readableNames = new string[count];
                    for (var i = 0; i < count; i++)
                    {
                        var implementor = implementors[i];
                        serializableNames[i] = implementor.FullName;
                        readableNames[i] = implementor.GetNameWithGenericArguments();
                    }

                    if (implementors.Count > 1)
                    {
                        serializableProviderTypeNames.Add(serializableName);
                        readableProviderTypeNames.Add(readableName);
                    }

                    k_AllSerializableImplementorNames.Add(serializableNames);
                    k_AllReadableImplementorNames.Add(readableNames);
                }
            });

            k_SerializableProviderTypeNames = serializableProviderTypeNames.ToArray();
            k_ReadableProviderTypeNames = readableProviderTypeNames.ToArray();

            k_AllSerializableProviderTypeNames = allSerializableProviderTypeNames.ToArray();
            k_AllReadableProviderTypeNames = allReadableProviderTypeNames.ToArray();
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var providerTypeProperty = property.FindPropertyRelative("m_ProviderTypeName");
            var prefabProperty = property.FindPropertyRelative("m_DefaultProviderPrefab");
            var defaultProviderProperty = property.FindPropertyRelative("m_DefaultProviderTypeName");

            const float deleteButtonWidth = 20f;
            const float deleteButtonMargin = 5f;
            var width = position.width - deleteButtonWidth;
            var thirdWidth = (width - deleteButtonMargin) * (1 / 3f);
            var left = position;
            left.width = thirdWidth;
            var mid = position;
            mid.width = thirdWidth;
            mid.position += Vector2.right * thirdWidth;
            var right = position;
            right.width = thirdWidth;
            right.position += thirdWidth * 2 * Vector2.right;

            using (var check = new EditorGUI.ChangeCheckScope())
            {
                var showAllProviderTypes = FunctionalityIslandEditor.ShowAllProviderTypes;

                var serializableProviderTypeName = providerTypeProperty.stringValue;
                var serializableProviderTypeNames = showAllProviderTypes ? k_AllSerializableProviderTypeNames : k_SerializableProviderTypeNames;
                var readableProviderTypeNames = showAllProviderTypes ? k_AllReadableProviderTypeNames : k_ReadableProviderTypeNames;

                var providerTypeIndex = Array.IndexOf(serializableProviderTypeNames, serializableProviderTypeName);
                var allProviderTypeIndex = Array.IndexOf(k_AllSerializableProviderTypeNames, serializableProviderTypeName);
                var validAllProviderIndex = allProviderTypeIndex > -1 && allProviderTypeIndex < k_AllSerializableProviderTypeNames.Length;
                if (providerTypeIndex == -1 && !string.IsNullOrEmpty(serializableProviderTypeName))
                {
                    k_TempSerializableIProviderTypeNames[0] = serializableProviderTypeName;
                    serializableProviderTypeNames = k_TempSerializableIProviderTypeNames;

                    var readableTypeName = "Missing Type";
                    if (validAllProviderIndex)
                        readableTypeName = k_AllReadableProviderTypeNames[allProviderTypeIndex];

                    providerTypeIndex = 0; // Override to use single-option list

                    k_TempReadableProviderTypeNames[0] = readableTypeName;
                    readableProviderTypeNames = k_TempReadableProviderTypeNames;
                }

                providerTypeIndex = EditorGUI.Popup(left, providerTypeIndex, readableProviderTypeNames);
                var validProviderIndex = providerTypeIndex > -1 && providerTypeIndex < serializableProviderTypeNames.Length;
                if (validProviderIndex)
                    serializableProviderTypeName = serializableProviderTypeNames[providerTypeIndex];

                var prefabObject = prefabProperty.objectReferenceValue;
                prefabObject = EditorGUI.ObjectField(right, prefabObject, typeof(GameObject), false);

                var defaultProviderName = defaultProviderProperty.stringValue;
                var disabled = prefabProperty.objectReferenceValue != null;
                using (new EditorGUI.DisabledScope(disabled))
                {
                    if (validAllProviderIndex)
                    {
                        var serializableNames = k_AllSerializableImplementorNames[allProviderTypeIndex];
                        var readableNames = k_AllReadableImplementorNames[allProviderTypeIndex];
                        var defaultProviderIndex = Array.IndexOf(serializableNames, defaultProviderName);
                        defaultProviderIndex = EditorGUI.Popup(mid, defaultProviderIndex, readableNames);

                        if (defaultProviderIndex > -1 && defaultProviderIndex < serializableNames.Length)
                            defaultProviderName = serializableNames[defaultProviderIndex];
                    }
                    else
                    {
                        EditorGUI.Popup(mid, 0, k_DefaultOptions);
                    }
                }

                if (check.changed)
                {
                    prefabProperty.objectReferenceValue = prefabObject;
                    providerTypeProperty.stringValue = serializableProviderTypeName;
                    defaultProviderProperty.stringValue = defaultProviderName;
                    property.serializedObject.ApplyModifiedProperties();
                }
            }

            var deleteButtonRect = position;
            deleteButtonRect.width = deleteButtonWidth;
            deleteButtonRect.position += width * Vector2.right;
            if (GUI.Button(deleteButtonRect, "X"))
            {
                var propertyPath = property.propertyPath;
                var indexMatch = System.Text.RegularExpressions.Regex.Match(propertyPath, @"(\d+)(?!.*\d)").Value;
                if (string.IsNullOrEmpty(indexMatch))
                {
                    Debug.LogErrorFormat("Could not find array element index in property path {0}", propertyPath);
                    return;
                }

                int index;
                if (!int.TryParse(indexMatch, out index))
                {
                    Debug.LogErrorFormat("Could not parse int from {0}", indexMatch);
                    return;
                }

                propertyPath = propertyPath.Substring(0, propertyPath.LastIndexOf(".data"));
                var arrayProperty = property.serializedObject.FindProperty(propertyPath);
                arrayProperty.DeleteArrayElementAtIndex(index);
                property.serializedObject.ApplyModifiedProperties();
            }
        }
    }
}
