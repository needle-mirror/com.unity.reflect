using System;
using System.Collections.Generic;
using Unity.Labs.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.Labs.ModuleLoader
{
    class ModuleLoaderSettingsEditor
    {
        internal class ModuleRow
        {
            public readonly Type type;
            public int excludeIndex;
            public MonoScript script;
            public readonly List<Type> dependencies = new List<Type>();
            public readonly List<Type> dependentModules = new List<Type>();
            public readonly bool isImmortal;
            public readonly string readableTypeName;

            readonly string m_SerializableTypeName;

            public bool show;
            public bool showDependencies;
            public bool showDependentModules;

            public ModuleRow(Type type)
            {
                this.type = type;
                var customAttributes = type.GetCustomAttributes(typeof(ImmortalModuleAttribute), true);
                isImmortal = customAttributes.Length > 0;

                readableTypeName = type.GetNameWithGenericArguments();
                m_SerializableTypeName = type.FullName;
            }

            public void IncludeDependencies(Dictionary<Type, ModuleRow> moduleRows, List<string> excludedTypes, SerializedProperty excludedTypesProperty)
            {
                foreach (var dependency in dependencies)
                {
                    ModuleRow dependencyRow;
                    if (moduleRows.TryGetValue(dependency, out dependencyRow))
                    {
                        var dependencyExcludeIndex = excludedTypes.IndexOf(dependencyRow.m_SerializableTypeName);
                        if (dependencyExcludeIndex != -1)
                        {
                            excludedTypesProperty.DeleteArrayElementAtIndex(dependencyExcludeIndex);
                            excludedTypes.RemoveAt(dependencyExcludeIndex);

                            dependencyRow.IncludeDependencies(moduleRows, excludedTypes, excludedTypesProperty);
                        }
                    }
                }
            }
        }

        internal class NamespaceGroup
        {
            public bool expanded = true;
            public readonly SortedDictionary<string, NamespaceGroup> children = new SortedDictionary<string, NamespaceGroup>();
            public readonly List<ModuleRow> modules = new List<ModuleRow>();

            public void SetExpandedRecursively(bool value)
            {
                expanded = value;
                foreach (var kvp in children)
                {
                    kvp.Value.SetExpandedRecursively(value);
                }
            }

            public void SortModulesRecursively()
            {
                modules.Sort((a, b) => a.type.Name.CompareTo(b.type.Name));

                foreach (var kvp in children)
                {
                    kvp.Value.SortModulesRecursively();
                }
            }
        }

        const float k_ToggleWidth = 15f;
        static readonly List<Type> k_ModuleTypes = new List<Type>();

        ModuleLoaderSettingsOverride m_Settings;

        readonly NamespaceGroup m_RootNamespaceGroup = new NamespaceGroup();
        readonly Dictionary<Type, ModuleRow> m_ModuleRows = new Dictionary<Type, ModuleRow>();

        static ModuleLoaderSettingsEditor()
        {
            ModuleLoaderCore.GetModuleTypes(k_ModuleTypes);
        }

        public ModuleLoaderSettingsEditor()
        {
            foreach (var moduleType in k_ModuleTypes)
            {
                var namespaceParts = moduleType.Namespace.Split('.');
                var group = m_RootNamespaceGroup;
                foreach (var part in namespaceParts)
                {
                    var lastGroup = group;
                    if (!group.children.TryGetValue(part, out group))
                    {
                        group = new NamespaceGroup();
                        lastGroup.children.Add(part, group);
                    }
                }

                var moduleRow = new ModuleRow(moduleType);
                group.modules.Add(moduleRow);
                m_ModuleRows[moduleType] = moduleRow;
            }

            m_RootNamespaceGroup.SortModulesRecursively();

            foreach (var moduleType in k_ModuleTypes)
            {
                foreach (var @interface in moduleType.GetInterfaces())
                {
                    if (@interface.IsGenericType && @interface.GetGenericTypeDefinition() == typeof(IModuleDependency<>))
                    {
                        var genericArguments = @interface.GetGenericArguments();
                        if (genericArguments.Length != 1)
                        {
                            Debug.LogErrorFormat("Error drawing module row. {0} must have exactly one generic argument", @interface.Name);
                            continue;
                        }

                        var dependency = genericArguments[0];
                        ModuleRow row;
                        if (m_ModuleRows.TryGetValue(moduleType, out row))
                            row.dependencies.Add(dependency);
                    }
                }
            }

            var scripts = AssetDatabase.FindAssets("t:script", new[] { "Assets", "Packages" });
            foreach (var guid in scripts)
            {
                var importer = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as MonoImporter;
                if (importer)
                {
                    var script = importer.GetScript();
                    var type = script.GetClass();
                    if (type == null)
                        continue;

                    ModuleRow row;
                    if (m_ModuleRows.TryGetValue(type, out row))
                        row.script = script;
                }
            }
        }

        internal bool DrawEnabledModules(List<string> excludedTypes, SerializedProperty excludedTypesProperty)
        {
            var wasShowing = m_RootNamespaceGroup.expanded;
            var isShowing = EditorGUILayout.Foldout(wasShowing, "Enabled Modules", true);
            m_RootNamespaceGroup.expanded = isShowing;
            if (wasShowing != isShowing && Event.current.alt)
                m_RootNamespaceGroup.SetExpandedRecursively(isShowing);

            if (!isShowing)
                return false;

            var modified = false;
            UpdateDependentTypes(excludedTypes);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawNamespaceGroup(m_RootNamespaceGroup, excludedTypes, excludedTypesProperty);
            }

            if (excludedTypesProperty.serializedObject.ApplyModifiedProperties())
                modified = true;

            return modified;
        }

        void DrawNamespaceGroup(NamespaceGroup group, List<string> excludedTypes, SerializedProperty excludedTypesProperty)
        {
            foreach (var kvp in group.children)
            {
                var groupName = kvp.Key;
                var child = kvp.Value;
                var wasExpanded = child.expanded;
                var isExpanded = EditorGUILayout.Foldout(wasExpanded, groupName, true);
                if (wasExpanded != isExpanded)
                {
                    if (Event.current.alt)
                        child.SetExpandedRecursively(isExpanded);
                    else
                        child.expanded = isExpanded;
                }

                if (isExpanded)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        DrawNamespaceGroup(kvp.Value, excludedTypes, excludedTypesProperty);
                    }
                }
            }

            foreach (var row in group.modules)
            {
                var moduleType = row.type;
                var show = row.show;
                var excludeIndex = row.excludeIndex;
                var included = excludeIndex == -1;
                var disabled = included && row.isImmortal | row.dependentModules.Count > 0;
                using (new EditorGUI.DisabledScope(disabled))
                {
                    using (new GUILayout.HorizontalScope())
                    {
                        var readableTypeName = row.readableTypeName;
                        var serializableTypeName = moduleType.FullName;
                        show = EditorGUILayout.Foldout(show, readableTypeName, true);
                        row.show = show;

                        var indentedRect = EditorGUI.IndentedRect(Rect.zero);
                        var nowIncluded = EditorGUILayout.Toggle("", included, GUILayout.Width(k_ToggleWidth + indentedRect.x));

                        if (included && !nowIncluded)
                        {
                            var newIndex = excludedTypes.Count;
                            excludedTypesProperty.InsertArrayElementAtIndex(newIndex);
                            var element = excludedTypesProperty.GetArrayElementAtIndex(newIndex);
                            element.stringValue = serializableTypeName;
                        }

                        if (!included && nowIncluded)
                        {
                            excludedTypesProperty.DeleteArrayElementAtIndex(excludeIndex);
                            excludedTypes.RemoveAt(excludeIndex);
                            row.IncludeDependencies(m_ModuleRows, excludedTypes, excludedTypesProperty);
                        }
                    }
                }

                if (show)
                    DrawModuleDependencies(row);
            }
        }

        void UpdateDependentTypes(List<string> excludedTypes)
        {
            foreach (var kvp in m_ModuleRows)
            {
                var moduleType = kvp.Key;
                var serializableTypeName = moduleType.FullName;
                var row = kvp.Value;
                row.excludeIndex = excludedTypes.IndexOf(serializableTypeName);
                row.dependentModules.Clear();
            }

            foreach (var kvp in m_ModuleRows)
            {
                var row = kvp.Value;
                if (row.excludeIndex != -1)
                    continue;

                var moduleType = kvp.Key;
                foreach (var dependency in row.dependencies)
                {
                    ModuleRow dependencyRow;
                    if (m_ModuleRows.TryGetValue(dependency, out dependencyRow))
                    {
                        dependencyRow.dependentModules.Add(moduleType);
                    }
                }
            }
        }

        void DrawModuleDependencies(ModuleRow row)
        {
            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(row.script, typeof(MonoScript), false);
                }

                var dependencies = row.dependencies;
                if (dependencies.Count > 0)
                {
                    var showDependencies = row.showDependencies;
                    showDependencies = EditorGUILayout.Foldout(showDependencies, "Dependencies", true);
                    row.showDependencies = showDependencies;
                    if (showDependencies)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (var moduleType in dependencies)
                            {
                                DrawModule(moduleType);
                            }
                        }
                    }
                }

                var dependentModules = row.dependentModules;
                if (dependentModules.Count > 0)
                {
                    var showDependentModules = row.showDependentModules;
                    showDependentModules = EditorGUILayout.Foldout(showDependentModules, "Dependent Modules", true);
                    row.showDependentModules = showDependentModules;
                    if (showDependentModules)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (var moduleType in dependentModules)
                            {
                                DrawModule(moduleType);
                            }
                        }
                    }
                }
            }
        }

        internal void DrawModule(Type moduleType)
        {
            ModuleRow row;
            if (m_ModuleRows.TryGetValue(moduleType, out row))
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.ObjectField(row.script, typeof(MonoScript), false);
                }
            }
            else
            {
                EditorGUILayout.LabelField(moduleType.GetNameWithGenericArguments());
            }
        }
    }
}
