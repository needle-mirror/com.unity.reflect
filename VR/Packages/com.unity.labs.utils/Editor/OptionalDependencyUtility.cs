using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Unity.Labs.Utils
{
    /// <inheritdoc />
    /// <summary>
    /// Allows for optional assembly references in assembly definition files
    /// On assembly compilation or import of an optional dependencies file in the same folder as any assembly
    /// definition, this class adds the designated optional assembly references, if they exist in the project
    /// </summary>
    [InitializeOnLoad]
    class OptionalDependencyUtility : AssetPostprocessor
    {
        const string k_OptionalDependenciesFileName = "optional_dependencies.json";
        const string k_AsmdefSearchFormat = "*.asmdef";

        // TODO: find a better way to serialize AssemblyDefinitions
        [Serializable]
        class AssemblyDefinition
        {
            [SerializeField]
            string name;

            [SerializeField]
            string[] references;

            [SerializeField]
            string[] optionalUnityReferences;

            [SerializeField]
            string[] includePlatforms;

            [SerializeField]
            string[] excludePlatforms;

            [SerializeField]
            bool allowUnsafeCode;

            public string[] References { get { return references; } set { references = value; } }
        }

        [Serializable]
        class OptionalDependencies
        {
            // Field names must match serialized names
            // We do not support writing these values because we do not write back to this file
#pragma warning disable 649
            [SerializeField]
            string[] references;

            [SerializeField]
            string[] packages;
#pragma warning restore 649

            public string[] References { get { return references; } }
            public string[] Packages { get { return packages; } }
        }

        static OptionalDependencyUtility()
        {
            CompilationPipeline.assemblyCompilationStarted += OnAssemblyCompilationStarted;
        }

        static void OnAssemblyCompilationStarted(string assemblyPath)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            var asmDefPath = CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName(assemblyName);
            bool modifiedAsmdef;
            bool addedPackages;
            UpdateOptionalDependencies(asmDefPath, out modifiedAsmdef, out addedPackages);
        }

        static void UpdateOptionalDependencies(string asmDefPath, out bool modifiedAsmdef, out bool addedPackages)
        {
            modifiedAsmdef = false;
            addedPackages = false;
            try
            {
                if (!File.Exists(asmDefPath))
                    return;

                var optDepsPath = string.Format("{0}/{1}", Path.GetDirectoryName(asmDefPath), k_OptionalDependenciesFileName);
                if (!File.Exists(optDepsPath))
                    return;

                var optionalDependencies = JsonUtility.FromJson<OptionalDependencies>(File.ReadAllText(optDepsPath));
                modifiedAsmdef = UpdateAssemblyDefinition(asmDefPath, optionalDependencies, optDepsPath);
                addedPackages = AddPackages(optionalDependencies, optDepsPath);
            }
            catch (Exception e)
            {
                Debug.LogErrorFormat("Exception resolving optional dependencies: {0}\n{1}", e.Message, e.StackTrace);
            }
        }

        static bool UpdateAssemblyDefinition(string asmDefPath, OptionalDependencies optionalDependencies, string optDepsPath)
        {
            var asmDef = JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(asmDefPath));
            var assemblies = CompilationPipeline.GetAssemblies();
            var assemblyNames = assemblies.Select(assembly => assembly.name).ToList();
            var optionalReferences = optionalDependencies.References;
            if (optionalReferences == null || optionalReferences.Length == 0)
                return false;

            var references = asmDef.References;
            var referenceList = references == null ? new List<string>() : references.ToList();
            var modified = false;
            foreach (var reference in optionalReferences)
            {
                if (!assemblyNames.Contains(reference))
                    continue;

                if (!referenceList.Contains(reference))
                {
                    modified = true;
                    referenceList.Add(reference);
                    Debug.LogFormat("Adding optional assembly reference {0} to {1} as defined in {2}",
                        reference, asmDefPath, optDepsPath);
                }
            }

            if (modified)
            {
                asmDef.References = referenceList.ToArray();
                File.WriteAllText(asmDefPath, JsonUtility.ToJson(asmDef, true));
            }

            return modified;
        }

        static bool AddPackages(OptionalDependencies optionalDependencies, string optDepsPath)
        {
            var packages = optionalDependencies.Packages;
            if (packages == null)
                return false;

            var packageList = new List<string>(packages);
            var list = Client.List();
            while (!list.IsCompleted) { }

            foreach (var result in list.Result)
            {
                packageList.Remove(result.name);
            }

            if (packageList.Count > 0)
            {
                var package = packageList[0];
                Debug.LogFormat("Adding optional package dependency {0} as defined in {1}", package, optDepsPath);
                Client.Add(package);
                return true;
            }

            return false;
        }

        static void OnPostprocessAllAssets(
            string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var modifiedAsmdef = false;
            var modifiedPackages = false;
            foreach (var asset in importedAssets)
            {
                if (asset.Contains(k_OptionalDependenciesFileName))
                {
                    var path = Path.GetDirectoryName(asset);
                    if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                        continue;

                    var asmdefs = Directory.GetFiles(path, k_AsmdefSearchFormat);
                    foreach (var asmdef in asmdefs)
                    {
                        UpdateOptionalDependencies(asmdef, out modifiedAsmdef, out modifiedPackages);
                    }
                }
            }

            // Suppress a warning about calling AssetDatabase.Refresh when adding/removing packages
            if (modifiedAsmdef && !modifiedPackages)
                AssetDatabase.Refresh();
        }
    }
}
