// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Task combines data returned from ResolvePackageDependencies into single items collection
    /// that can be consumed by DesignTime build and contains all info needed to expand packages
    /// dependency graph.
    /// If any changes are made here, make sure corresponding changes are made to NuGetDependenciesSubTreeProvider
    /// in roslyn-project-system repo and corresponding tests.
    /// </summary>
    public class PreprocessPackageDependenciesDesignTime : TaskBase
    {
        public const string DependenciesMetadata = "Dependencies";
        public const string CompileTimeAssemblyMetadata = "CompileTimeAssembly";
        public const string ResolvedMetadata = "Resolved";

        [Required]
        public ITaskItem[] TargetDefinitions { get; set; }

        [Required]
        public ITaskItem[] PackageDefinitions { get; set; }

        [Required]
        public ITaskItem[] FileDefinitions { get; set; }

        [Required]
        public ITaskItem[] PackageDependencies { get; set; }

        [Required]
        public ITaskItem[] FileDependencies { get; set; }

        [Required]
        public ITaskItem[] PackageReferences { get; set; }

        public ITaskItem[] InputDiagnosticMessages { get; set; }

        [Output]
        public ITaskItem[] DependenciesDesignTime { get; set; }

        private Dictionary<string, ItemMetadata> Targets { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> Packages { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> Assemblies { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> DiagnosticsMap { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> DependenciesWorld { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> ImplicitPackageReferences { get; set; }
                    = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        protected override void ExecuteCore()
        {
            PopulateImplicitPackageReferences();

            PopulateTargets();

            PopulatePackages();

            PopulateAssemblies();

            InputDiagnosticMessages = InputDiagnosticMessages ?? Array.Empty<ITaskItem>();
            PopulateDiagnosticsMap();

            AddDependenciesToTheWorld(Packages, PackageDependencies);

            AddDependenciesToTheWorld(Assemblies, FileDependencies, (item) =>
            {
                // We keep analyzers and assemblies with CompileTimeAssembly metadata; skip everything else.

                ItemMetadata itemMetadata = null;                
                if (Assemblies.TryGetValue(item.ItemSpec, out itemMetadata) && 
                    itemMetadata.Type == DependencyType.AnalyzerAssembly)
                {
                    return false;
                }

                var fileGroup = item.GetMetadata(MetadataKeys.FileGroup);
                return string.IsNullOrEmpty(fileGroup) || !fileGroup.Equals(CompileTimeAssemblyMetadata);
            });

            AddDependenciesToTheWorld(DiagnosticsMap, InputDiagnosticMessages);

            // prepare output collection: add corresponding metadata to ITaskItem based in item type
            DependenciesDesignTime = DependenciesWorld.Select(itemKvp =>
            {
                var newTaskItem = new TaskItem(itemKvp.Key);
                foreach(var metadataKvp in itemKvp.Value.ToDictionary())
                {
                    newTaskItem.SetMetadata(metadataKvp.Key, metadataKvp.Value);
                }

                return newTaskItem;
            }).ToArray();
        }

        private void PopulateImplicitPackageReferences()
        {
            foreach(var packageReference in PackageReferences)
            {
                var isImplicitlyDefinedString = packageReference.GetMetadata(MetadataKeys.IsImplicitlyDefined);
                if (string.IsNullOrEmpty(isImplicitlyDefinedString))
                {
                    continue;
                }

                bool isImplicitlyDefined;
                if (!Boolean.TryParse(isImplicitlyDefinedString, out isImplicitlyDefined)
                    || !isImplicitlyDefined)
                {
                    continue;
                }

                ImplicitPackageReferences.Add(packageReference.ItemSpec);
            }
        }

        /// <summary>
        /// Adds targets from TargetDefinitions to dependencies world dictionary
        /// </summary>
        private void PopulateTargets()
        {
            foreach (var targetDef in TargetDefinitions)
            {
                if (string.IsNullOrEmpty(targetDef.ItemSpec) || targetDef.ItemSpec.Contains("/"))
                {
                    // skip "target/rid"s and only consume actual targets
                    continue;
                }

                var dependencyType = GetDependencyType(targetDef.GetMetadata(MetadataKeys.Type));
                if (dependencyType != DependencyType.Target)
                {
                    // keep only targets here
                    continue;
                }

                var target = new TargetMetadata(targetDef);
                                
                Targets[targetDef.ItemSpec] = target;

                // add target to the world now, since it does not have parents
                DependenciesWorld[targetDef.ItemSpec] = target;
            }
        }

        /// <summary>
        /// Adds packages from PackageDefinitions to the dependencies world dictionary.
        /// </summary>
        private void PopulatePackages()
        {
            foreach (var packageDef in PackageDefinitions)
            {
                var dependencyType = GetDependencyType(packageDef.GetMetadata(MetadataKeys.Type));
                if (dependencyType != DependencyType.Package &&
                    dependencyType != DependencyType.Unresolved)
                {
                    // we ignore all other dependency types since 
                    //      - assemblies we handle separatelly below 
                    //      - projects we don't care here, since they are sent to project system via other route
                    continue;
                }

                var dependency = new PackageMetadata(packageDef);
                dependency.IsImplicitlyDefined = ImplicitPackageReferences.Contains(dependency.Name);

                Packages[packageDef.ItemSpec] = dependency;
            }
        }

        /// <summary>
        /// Adds assemblies, analyzers and framework assemblies from FileDefinitons to dependencies world dictionary.
        /// </summary>
        private void PopulateAssemblies()
        {
            foreach (var fileDef in FileDefinitions)
            {
                var dependencyType = GetDependencyType(fileDef.GetMetadata(MetadataKeys.Type));
                if (dependencyType != DependencyType.Assembly &&
                    dependencyType != DependencyType.FrameworkAssembly &&
                    dependencyType != DependencyType.AnalyzerAssembly)
                {
                    continue;
                }

                var name = Path.GetFileName(fileDef.ItemSpec);
                var assembly = new AssemblyMetadata(dependencyType, fileDef, name);
                Assemblies[fileDef.ItemSpec] = assembly;
            }
        }

        private void PopulateDiagnosticsMap()
        {
            foreach (var diagnostic in InputDiagnosticMessages)
            {
                var metadata = new DiagnosticMetadata(diagnostic);
                DiagnosticsMap[diagnostic.ItemSpec] = metadata;
            }
        }

        private DependencyType GetDependencyType(string dependencyTypeString)
        {
            var dependencyType = DependencyType.Unknown;
            if (!string.IsNullOrEmpty(dependencyTypeString))
            {
                Enum.TryParse(dependencyTypeString, /* ignoreCase */ true, out dependencyType);
            }

            return dependencyType;
        }

        private void AddDependenciesToTheWorld(Dictionary<string, ItemMetadata> items,
                                               ITaskItem[] itemDependencies,
                                               Func<ITaskItem, bool> shouldSkipItemCheck = null)
        {
            foreach (var dependency in itemDependencies)
            {
                var currentItemId = dependency.ItemSpec;
                if (!items.Keys.Contains(currentItemId))
                {
                    // if this package definition does not even exist - skip it
                    continue;
                }

                if (shouldSkipItemCheck != null && shouldSkipItemCheck(dependency))
                {
                    continue;
                }

                var parentTargetId = dependency.GetMetadata(MetadataKeys.ParentTarget) ?? string.Empty;
                if (parentTargetId.Contains("/") || !Targets.Keys.Contains(parentTargetId))
                {
                    // skip "target/rid"s and only consume actual targets and ignore non-existent parent targets
                    continue;
                }

                var parentPackageId = dependency.GetMetadata(MetadataKeys.ParentPackage) ?? string.Empty;
                if (!string.IsNullOrEmpty(parentPackageId) && !Packages.Keys.Contains(parentPackageId))
                {
                    // ignore non-existent parent packages
                    continue;
                }

                var currentPackageUniqueId = $"{parentTargetId}/{currentItemId}";
                // add current package to dependencies world
                var currentItem = items[currentItemId];
                DependenciesWorld[currentPackageUniqueId] = currentItem;

                // update parent
                var parentDependencyId = $"{parentTargetId}/{parentPackageId}".Trim('/');
                ItemMetadata parentDependency = null;
                if (DependenciesWorld.TryGetValue(parentDependencyId, out parentDependency))
                {
                    parentDependency.Dependencies.Add(currentItemId);
                    if (parentDependency.Type == DependencyType.Target)
                    {
                        currentItem.IsTopLevelDependency = true;
                    }
                }
                else
                {
                    // Update parent's Dependencies count and make sure parent is in the dependencies world
                    if (!string.IsNullOrEmpty(parentPackageId))
                    {
                        parentDependency = Packages[parentPackageId];
                    }
                    else
                    {
                        parentDependency = Targets[parentTargetId];
                        currentItem.IsTopLevelDependency = true;
                    }

                    parentDependency.Dependencies.Add(currentItemId);
                    DependenciesWorld[parentDependencyId] = parentDependency;
                }
            }
        }

        private abstract class ItemMetadata
        {
            public ItemMetadata(DependencyType type)
            {
                Type = type;
                Dependencies = new List<string>();
            }

            public DependencyType Type { get; protected set; }
            public bool IsTopLevelDependency { get; set; }

            /// <summary>
            /// A list of name/version strings to specify dependency identities.
            /// Note: identity here is just a "name/version" and does not have TFM part in front.
            /// </summary>
            public IList<string> Dependencies { get; }

            /// <summary>
            /// Returns name/value pairs for metadata specific to given item type's implementation.
            /// </summary>
            /// <returns></returns>
            public abstract IDictionary<string, string> ToDictionary();
        }

        private class TargetMetadata : ItemMetadata
        {
            public TargetMetadata(ITaskItem item)
                :base(DependencyType.Target)
            {
                RuntimeIdentifier = item.GetMetadata(MetadataKeys.RuntimeIdentifier) ?? string.Empty;
                TargetFrameworkMoniker = item.GetMetadata(MetadataKeys.TargetFrameworkMoniker) ?? string.Empty;
                FrameworkName = item.GetMetadata(MetadataKeys.FrameworkName) ?? string.Empty;
                FrameworkVersion = item.GetMetadata(MetadataKeys.FrameworkVersion) ?? string.Empty;
            }

            public string RuntimeIdentifier { get; }
            public string TargetFrameworkMoniker { get; }
            public string FrameworkName { get; }
            public string FrameworkVersion { get; }

            public override IDictionary<string, string> ToDictionary()
            {
                return new Dictionary<string, string>
                {
                    { MetadataKeys.RuntimeIdentifier, RuntimeIdentifier },
                    { MetadataKeys.TargetFrameworkMoniker, TargetFrameworkMoniker },
                    { MetadataKeys.FrameworkName, FrameworkName },
                    { MetadataKeys.FrameworkVersion, FrameworkVersion },
                    { MetadataKeys.Type, Type.ToString() },
                    { DependenciesMetadata, string.Join(";", Dependencies) }
                };
            }
        }

        private class PackageMetadata : ItemMetadata
        {
            public PackageMetadata(ITaskItem item)
                : base(DependencyType.Package)
            {
                Name = item.GetMetadata(MetadataKeys.Name) ?? string.Empty;
                Version = item.GetMetadata(MetadataKeys.Version) ?? string.Empty;
                Resolved = Type != DependencyType.Unknown && !string.IsNullOrEmpty(item.GetMetadata(MetadataKeys.ResolvedPath));
                Path = (Resolved
                        ? item.GetMetadata(MetadataKeys.ResolvedPath)
                        : item.GetMetadata(MetadataKeys.Path)) ?? string.Empty;
            }

            public string Name { get; protected set; }
            public string Version { get; }
            public string Path { get; }
            public bool Resolved { get; }
            public bool IsImplicitlyDefined { get; set; }

            public override IDictionary<string, string> ToDictionary()
            {
                return new Dictionary<string, string>
                {
                    { MetadataKeys.Name, Name },
                    { MetadataKeys.Version, Version },
                    { MetadataKeys.Path, Path },
                    { MetadataKeys.Type, Type.ToString() },
                    { MetadataKeys.IsImplicitlyDefined, IsImplicitlyDefined.ToString() },
                    { MetadataKeys.IsTopLevelDependency, IsTopLevelDependency.ToString() },
                    { ResolvedMetadata, Resolved.ToString() },
                    { DependenciesMetadata, string.Join(";", Dependencies) }
                };
            }
        }

        private class AssemblyMetadata : PackageMetadata
        {
            public AssemblyMetadata(DependencyType type,
                                    ITaskItem item,
                                    string name)
                : base(item)
            {
                Name = name ?? string.Empty;
                Type = type;
            }
        }

        private sealed class DiagnosticMetadata : ItemMetadata
        {
            public DiagnosticMetadata(ITaskItem item)
                : base(DependencyType.Diagnostic)
            {
                DiagnosticCode = item.GetMetadata(MetadataKeys.DiagnosticCode) ?? string.Empty;
                Message = item.GetMetadata(MetadataKeys.Message) ?? string.Empty;
                FilePath = item.GetMetadata(MetadataKeys.FilePath) ?? string.Empty;
                Severity = item.GetMetadata(MetadataKeys.Severity) ?? string.Empty;
                StartLine = item.GetMetadata(MetadataKeys.StartLine) ?? string.Empty;
                StartColumn = item.GetMetadata(MetadataKeys.StartColumn) ?? string.Empty;
                EndLine = item.GetMetadata(MetadataKeys.EndLine) ?? string.Empty;
                EndColumn = item.GetMetadata(MetadataKeys.EndColumn) ?? string.Empty;
            }

            public string DiagnosticCode { get; }
            public string Message { get; }
            public string FilePath { get; }
            public string Severity { get; }
            public string StartLine { get; }
            public string StartColumn { get; }
            public string EndLine { get; }
            public string EndColumn { get; }

            public override IDictionary<string, string> ToDictionary()
            {
                return new Dictionary<string, string>
                {
                    { MetadataKeys.Name, Message },
                    { MetadataKeys.DiagnosticCode, DiagnosticCode },
                    { MetadataKeys.Message, Message },
                    { MetadataKeys.FilePath, FilePath },
                    { MetadataKeys.Severity, Severity },
                    { MetadataKeys.StartLine, StartLine },
                    { MetadataKeys.StartColumn, StartColumn },
                    { MetadataKeys.EndLine, EndLine },
                    { MetadataKeys.EndColumn, EndColumn },
                    { MetadataKeys.Type, Type.ToString() },
                    { DependenciesMetadata, string.Join(";", Dependencies) }
                };
            }
        }
    }
}