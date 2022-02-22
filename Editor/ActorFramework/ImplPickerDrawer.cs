using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Reflect.ActorFramework
{
    [CustomPropertyDrawer(typeof(ImplPickerAttribute))]
    public class ImplPickerDrawer : PropertyDrawer
    {
        static readonly Dictionary<Type, List<Type>> s_TypeToChildren = new Dictionary<Type, List<Type>>();

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            var propContainer = new VisualElement();
            
            var (type, obj) = GetHandleAtPath(property.serializedObject.targetObject, property.propertyPath);
            
            var types = GetChildrenTypes(type);
            types.Insert(0, typeof(object));
            var selector = new PopupField<Type>(property.displayName, types, 0, FormatTypeNames, FormatTypeNames);
            selector.RegisterCallback<ChangeEvent<Type>>(e => OnSelectionChanged(e, property, propContainer));
            container.Add(selector);
            container.Add(propContainer);
            propContainer.AddToClassList(Foldout.contentUssClassName);

            if (obj != null)
            {
                selector.SetValueWithoutNotify(obj.GetType());
                var children = GetVisibleChildren(property);
                foreach (var child in children)
                {
                    var p = new PropertyField(child);
                    p.BindProperty(child);
                    propContainer.Add(p);
                }
            }

            return container;
        }

        static string FormatTypeNames(Type type) => type == typeof(object) || type == null ? "None" : type.Name;

        static void OnSelectionChanged(ChangeEvent<Type> e, SerializedProperty prop, VisualElement propContainer)
        {
            prop.managedReferenceValue = null;
            prop.serializedObject.ApplyModifiedProperties();
            propContainer.Clear();

            if (e.newValue == typeof(object))
                return;

            var obj = Activator.CreateInstance(e.newValue);
            prop.managedReferenceValue = obj;
            prop.serializedObject.ApplyModifiedProperties();

            var children = GetVisibleChildren(prop);
            foreach (var child in children)
            {
                var p = new PropertyField(child);
                p.BindProperty(child);
                propContainer.Add(p);
            }
        }

        static List<Type> GetChildrenTypes(Type type)
        {
            if (!s_TypeToChildren.TryGetValue(type, out var children))
            {
                children = GetChildrenOfType(type);
                s_TypeToChildren.Add(type, children);
            }

            return children;
        }

        static (Type Type, object Obj) GetHandleAtPath(object obj, string path)
        {
            var segments = path.Split('.');
            var parentObj = obj;
            var parentType = parentObj.GetType();
            
            Type fieldType = null;

            for (var i = 0; i < segments.Length; ++i)
            {
                var fieldName = segments[i];
                
                if (fieldName == "Array" && i < segments.Length - 1 && segments[i + 1].StartsWith("data["))
                {
                    var seg = segments[i + 1];
                    var index = int.Parse(seg.Substring(5, seg.Length - 1 - 5));
                    if (fieldType.IsArray)
                    {
                        var array = (object[])parentObj;
                        var elem = array[index];
                        
                        fieldType = fieldType.GetElementType();
                        parentObj = elem;
                        parentType = elem?.GetType();
                    }
                    else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(List<>))
                    {
                        var list = Unsafe.As<List<object>>(parentObj);
                        var elem = list[index];
                        
                        fieldType = fieldType.GetGenericArguments()[0];
                        parentObj = elem;
                        parentType = elem?.GetType();
                    }
                    else
                        throw new NotSupportedException($"Type '{fieldType}' is not supported");
                    ++i;
                }
                else
                {
                    var field = parentType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    fieldType = field.FieldType;
                    parentObj = field.GetValue(parentObj);
                    parentType = parentObj?.GetType();
                }
            }

            return (fieldType, parentObj);
        }

        static List<Type> GetChildrenOfType(Type type)
        {
            return AppDomain.CurrentDomain
                .GetAssemblies()
                .SelectMany(x => x.GetTypes())
                .Where(x => x.IsClass && x != type && !typeof(UnityEngine.Object).IsAssignableFrom(x)
                            && x.IsSerializable
                            && type.IsAssignableFrom(x)
                            && x.GetConstructor(Type.EmptyTypes) != null)
                .ToList();
        }

        static IEnumerable<SerializedProperty> GetVisibleChildren(SerializedProperty serializedProperty)
        {
            var currentProperty = serializedProperty.Copy();
            var nextSiblingProperty = serializedProperty.Copy();
            {
                nextSiblingProperty.NextVisible(false);
            }

            if (currentProperty.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(currentProperty, nextSiblingProperty))
                        break;
                    yield return currentProperty;
                }
                while (currentProperty.NextVisible(false));
            }
        }
    }
}