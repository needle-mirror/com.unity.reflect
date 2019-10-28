using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityObject = UnityEngine.Object;

namespace Unity.Labs.Utils
{
    public static class EditorUtils
    {
        /// <summary>
        /// Gets the tooltip of the property.
        /// This is a workaround for a bug where SerializedProperty.tooltip always returns an empty string:
        /// https://issuetracker.unity3d.com/issues/when-using-custom-propertydrawers-the-tooltip-field-of-the-serializedproperty-is-always-empty
        /// </summary>
        /// <returns>Tooltip specified in the property's TooltipAttribute, if it has one. Otherwise returns an empty string.</returns>
        public static string GetTooltip(this SerializedProperty property)
        {
            var targetType = property.serializedObject.targetObject.GetType();
            var field = targetType.GetFieldRecursively(
                property.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var attributes = (TooltipAttribute[])field.GetCustomAttributes(typeof(TooltipAttribute), true);
            return attributes.Length > 0 ? attributes[0].tooltip : "";
        }

        public static Attribute[] GetMemberAttributes(SerializedProperty property)
        {
            var fi = GetFieldInfoFromProperty(property);
            return fi.GetCustomAttributes(false).Cast<Attribute>().ToArray();
        }

        public static FieldInfo GetFieldInfoFromProperty(SerializedProperty property)
        {
            Type type;
            var memberInfo = GetMemberInfoFromPropertyPath(property.serializedObject.targetObject.GetType(), property.propertyPath, out type);
            if (memberInfo.MemberType != MemberTypes.Field)
                return null;
            return memberInfo as FieldInfo;
        }

        public static MemberInfo GetMemberInfoFromPropertyPath(Type host, string path, out Type type)
        {
            type = host;
            if (host == null)
                return null;
            MemberInfo memberInfo = null;

            var parts = path.Split ('.');
            for (var i = 0; i < parts.Length; i++)
            {
                var member = parts[i];

                // Special handling of array elements.
                // The "Array" and "data[x]" parts of the propertyPath don't correspond to any types,
                // so they should be skipped by the code that drills down into the types.
                // However, we want to change the type from the type of the array to the type of the array
                // element before we do the skipping.
                if (i < parts.Length - 1 && member == "Array" && parts[i + 1].StartsWith ("data["))
                {
                    Type listType = null;
                    // ReSharper disable once PossibleNullReferenceException would have returned if host was null
                    if (type.IsArray)
                    {
                        listType = type.GetElementType();
                    }
                    else if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        listType = type.GetGenericArguments()[0];
                    }
                    if (listType != null)
                        type = listType;

                    // Skip rest of handling for this part ("Array") and the next part ("data[x]").
                    i++;
                    continue;
                }

                // GetField on class A will not find private fields in base classes to A,
                // so we have to iterate through the base classes and look there too.
                // Private fields are relevant because they can still be shown in the Inspector,
                // and that applies to private fields in base classes too.
                MemberInfo foundMember = null;
                for (var currentType = type; foundMember == null && currentType != null; currentType =
                    currentType.BaseType)
                {
                    var foundMembers = currentType.GetMember(member, BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic);
                    if (foundMembers.Length > 0 && foundMembers[0] != null)
                    {
                        foundMember = foundMembers[0];
                    }
                }

                if (foundMember == null)
                {
                    type = null;
                    return null;
                }

                memberInfo = foundMember;
                switch (memberInfo.MemberType) {
                    case MemberTypes.Field:
                        var info = memberInfo as FieldInfo;
                        if (info != null)
                            type = info.FieldType;
                        break;
                    case MemberTypes.Property:
                        var propertyInfo = memberInfo as PropertyInfo;
                        if (propertyInfo != null)
                            type = propertyInfo.PropertyType;
                        break;
                    default:
                        type = memberInfo.DeclaringType;
                        break;
                }
            }

            return memberInfo;
        }

        /// <summary>
        ///  Consumes mouse input events. Call this at the end of a GUI window draw function to block clicking
        ///  on controls behind the window.
        /// </summary>
        public static void ConsumeMouseInput()
        {
            var controlId = GUIUtility.GetControlID(FocusType.Passive);
            switch (Event.current.GetTypeForControl(controlId))
            {
                case EventType.MouseDown:
                    GUIUtility.hotControl = controlId;
                    Event.current.Use();
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl != controlId)
                        break;

                    GUIUtility.hotControl = 0;
                    Event.current.Use();
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl != controlId)
                        break;

                    Event.current.Use();
                    break;
                case EventType.ScrollWheel:
                    Event.current.Use();
                    break;
            }
        }

        /// <summary>
        /// Enable / disable a number of common class icons in Scene Views
        /// </summary>
        /// <param name="enabled">whether to enable or disable the icons</param>
        /// <param name="classIconEnabledStates">An empty dictionary to store the previous state of the icons</param>
        /// <returns>A mapping of class to & script class name to original enabled state</returns>
        public static void ToggleCommonIcons(bool enabled, Dictionary<int, KeyValuePair<string, bool>> classIconEnabledStates)
        {
            // see https://docs.unity3d.com/Manual/ClassIDReference.html for class id details
            var classIdsToToggle = new HashSet<int>
            {
                4, // Transform
                20, // Camera
                54, // Rigidbody
                82, // AudioSource
                108 // light
            };

            var annotation = Type.GetType("UnityEditor.Annotation, UnityEditor");
            var classId = annotation.GetField("classID");
            var scriptClass = annotation.GetField("scriptClass");
            var asm = Assembly.GetAssembly(typeof(Editor));
            var type = asm.GetType("UnityEditor.AnnotationUtility");
            if (type != null)
            {
                var getAnnotations = type.GetMethod("GetAnnotations", BindingFlags.Static | BindingFlags.NonPublic);
                var setIconEnabled = type.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);

                var annotations = (Array)getAnnotations.Invoke(null, null);
                foreach (var a in annotations)
                {
                    var classIdValue = (int)classId.GetValue(a);
                    if (classIdsToToggle.Contains(classIdValue))
                    {
                        var iconEnabledField = annotation.GetField("iconEnabled");
                        var iconEnabled = (int)iconEnabledField.GetValue(a);
                        var scriptClassValue = (string)scriptClass.GetValue(a);

                        classIconEnabledStates.Add(classIdValue, new KeyValuePair<string, bool>(scriptClassValue, iconEnabled == 1));
                        setIconEnabled.Invoke(null, new object[] { classIdValue, scriptClassValue, enabled ? 1 : 0 });
                    }
                }
            }
        }

        /// <summary>
        /// Enable / disable class icons by class id & script class name
        /// </summary>
        /// <param name="enabledStates">A mapping of class to & script class name to desired enabled state</param>
        public static void ToggleClassIcons(Dictionary<int, KeyValuePair<string, bool>> enabledStates)
        {
            var asm = Assembly.GetAssembly(typeof(Editor));
            var type = asm.GetType("UnityEditor.AnnotationUtility");
            if (type != null)
            {
                var setIconEnabled = type.GetMethod("SetIconEnabled", BindingFlags.Static | BindingFlags.NonPublic);

                foreach (var kvp in enabledStates)
                {
                    var classId = kvp.Key;
                    var innerKvp = kvp.Value;
                    var scriptClass = innerKvp.Key;
                    var enabled = innerKvp.Value;
                    setIconEnabled.Invoke(null, new object[] { classId, scriptClass, enabled ? 1 : 0 });
                }
            }
        }

        /// <summary>
        /// Get the SceneAsset representing the active scene
        /// </summary>
        /// <returns>The active SceneAsset</returns>
        public static SceneAsset GetActiveSceneAsset()
        {
            var path = SceneManager.GetActiveScene().path;
            return AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }


        /// <summary>
        /// Guess the type of a <c>SerializedProperty</c> and return a <c>System.Type</c>, if one exists.
        /// The guess is done by checking the type of the target object and iterating through its fields looking for
        /// one that matches the property name. This may return null if you give it a <c>SerializedProperty</c> that
        /// represents a native type with no managed equivalent
        /// </summary>
        /// <param name="property">The <c>SerializedProperty</c> to examine</param>
        /// <returns>The best guess type</returns>
        public static Type SerializedPropertyToType(SerializedProperty property)
        {
            var field = SerializedPropertyToField(property);
            return field != null ? field.FieldType : null;
        }

        public static FieldInfo SerializedPropertyToField(SerializedProperty property)
        {
            var parts = property.propertyPath.Split('.');
            if (parts.Length == 0)
                return null;

            var currentType = property.serializedObject.targetObject.GetType();
            FieldInfo field = null;
            foreach (var part in parts)
            {
                if (part == "Array")
                {
                    currentType = field.FieldType.GetElementType();
                    continue;
                }

                field = currentType.GetFieldInTypeOrBaseType(part);
                if (field == null)
                    continue;

                currentType = field.FieldType;
            }

            return field;
        }

        /// <summary>
        /// Special version of EditorGUI.MaskField which ensures that only the chosen bits are set. We need this version of the
        /// function to check explicitly whether only a single bit was set.
        /// </summary>
        public static int MaskField(Rect position, GUIContent label, int mask, string[] displayedOptions, Type propertyType)
        {
            mask = EditorGUI.MaskField(position, label, mask, displayedOptions);
            return ActualEnumFlags(mask, propertyType);
        }

        /// <summary>
        /// Return a value with only bits that can be set with values in the enum to prevent multiple representations of the same state
        /// </summary>
        /// <param name="value">The flags value</param>
        /// <param name="t">The type of enum to use</param>
        /// <returns>The transformed flags value</returns>
        static int ActualEnumFlags(int value, Type t)
        {
            if (value < 0)
            {
                var mask = 0;
                foreach (var enumValue in Enum.GetValues(t))
                {
                    mask |= (int)enumValue;
                }

                value &= mask;
            }

            return value;
        }

        /// <summary>
        /// Strip PPtr<> and $ from a string for getting a System.Type from SerializedProperty.type
        /// TODO: expose internal SerializedProperty.objectReferenceTypeString to remove this hack
        /// </summary>
        /// <param name="type">Type string</param>
        /// <returns>Nicified type string</returns>
        public static string NicifySerializedPropertyType(string type)
        {
            return type.Replace("PPtr<", "").Replace(">", "").Replace("$", "");
        }

        /// <summary>
        /// Search through all assemblies in the current AppDomain for a class that is assignable to UnityObject and matches the given weak name
        /// TODO: expose internal SerializedProperty.ValidateObjectReferenceValue to remove his hack
        /// </summary>
        /// <param name="name">Weak type name</param>
        /// <returns>Best guess System.Type</returns>
        public static Type TypeNameToType(string name)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.Name.Equals(name) && typeof(UnityObject).IsAssignableFrom(type))
                            return type;
                    }
                }
                catch (ReflectionTypeLoadException)
                {
                    // Skip any assemblies that don't load properly
                }
            }

            return typeof(UnityObject);
        }

        /// <summary>
        /// Tries to get an asset preview. If one is not available, waits until IsLoadingAssetPreview is false, and if
        /// preview is still not loaded, returns the result of AssetPreview.GetMiniThumbnail
        /// </summary>
        /// <param name="asset">The asset for which to get a preview</param>
        /// <param name="callback">Called with the preview texture as an argument, when it is available</param>
        /// <returns>An enumerator used to tick the corutine</returns>
        public static IEnumerator GetAssetPreview(UnityObject asset, Action<Texture> callback)
        {
            // GetAssetPreview will start loading the preview, or return one if available
            var texture = AssetPreview.GetAssetPreview(asset);

            // If the preview is not available, IsLoadingAssetPreview will be true until loading has finished
            while (AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID()))
            {
                texture = AssetPreview.GetAssetPreview(asset);
                yield return null;
            }

            // If loading a preview fails, fall back to the MiniThumbnail
            if (!texture)
                texture = AssetPreview.GetMiniThumbnail(asset);

            callback(texture);
        }
    }
}
