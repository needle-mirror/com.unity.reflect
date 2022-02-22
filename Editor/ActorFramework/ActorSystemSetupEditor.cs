using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace Unity.Reflect.ActorFramework
{
    [CustomEditor(typeof(ActorSystemSetup))]
    public class ActorSystemSetupEditor : Editor
    {
        static readonly string k_StylePath = "Packages/com.unity.reflect/Editor/ActorFramework/ActorSetupInspectorStyle.uss";

        static class UssClasses
        {
            public static readonly string k_ActorSystemTitle = "actor_system_title";
            public static readonly string k_ActorSystemSettingTitle = "actor_system_setting_title";
            public static readonly string k_ActorSystemSettingEntry = "actor_system_setting_entry";
        }
        
        public override VisualElement CreateInspectorGUI()
        {
            var container = new VisualElement();

            var style = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StylePath);
            container.styleSheets.Add(style);
            
            var customSection = new VisualElement();

            BuildCustomSection(customSection);
            container.Add(customSection);

            return container;
        }

        void BuildCustomSection(VisualElement container)
        {
            var label = new Label("Asset Settings");
            label.AddToClassList(UssClasses.k_ActorSystemTitle);
            container.Add(label);

            var setupsProp = serializedObject.FindProperty($"{nameof(ActorSystemSetup.ActorSetups)}");
            for (var i = 0; i < setupsProp.arraySize; ++i)
            {
                var setupProp = setupsProp.GetArrayElementAtIndex(i);

                var displayNameProp = setupProp.FindPropertyRelative($"{nameof(ActorSetup.DisplayName)}").stringValue;
                var settingsProp = setupProp.FindPropertyRelative($"{nameof(ActorSetup.Settings)}");

                if (settingsProp.managedReferenceFullTypename.Contains(nameof(ActorSettings)))
                    continue;

                var properties = GetVisibleChildren(settingsProp);
                if (properties.Any())
                {
                    var foldout = new Foldout {text = displayNameProp, value = false}; // Should use settingsProp.isExpanded but it's very slow
                    foldout.AddToClassList(UssClasses.k_ActorSystemSettingTitle);
                    container.Add(foldout);

                    foreach (var serializedProperty in properties)
                    {
                        var p = new PropertyField(serializedProperty);
                        p.BindProperty(serializedProperty); // Seems to be required when inside an embedded Editor
                        p.AddToClassList(UssClasses.k_ActorSystemSettingEntry);
                        if (serializedProperty.propertyType == SerializedPropertyType.ExposedReference)
                        {
                            p.RegisterValueChangeCallback(evt =>
                            {
                                if (PrefabUtility.IsPartOfPrefabInstance(evt.changedProperty.serializedObject.context))
                                {
                                    EditorUtility.SetDirty(evt.changedProperty.serializedObject.context);
                                }
                            });
                        }
                        foldout.Add(p);
                    }
                }
            }
            
            label = new Label("Advanced");
            label.AddToClassList(UssClasses.k_ActorSystemTitle);
            container.Add(label);
            
            var assembliesProp = serializedObject.FindProperty(nameof(ActorSystemSetup.ExcludedAssemblies));
            var assembliesPropField = new PropertyField(assembliesProp);
            assembliesPropField.BindProperty(assembliesProp);
            container.Add(assembliesPropField);
        }

        void BuildDefaultSection(VisualElement container)
        {
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
            {
                do
                {
                    var propertyField = new PropertyField(iterator.Copy()) { name = "PropertyField:" + iterator.propertyPath };
                    propertyField.BindProperty(iterator.Copy());
 
                    if (iterator.propertyPath == "m_Script" && serializedObject.targetObject != null)
                        propertyField.SetEnabled(false);
 
                    container.Add(propertyField);
                }
                while (iterator.NextVisible(false));
            }
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
