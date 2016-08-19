// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    /// <summary>
    /// Task combines data returned from ResolvePackageDependencies into single items collection
    /// that can be consumed by DesignTime build and contains all info needed to expand packages
    /// dependency graph.
    /// If any changes are made here, make sure corresponding changes are made to NuGetDependenciesSubTreeProvider
    /// in roslyn-project-system repo and corresponding tests.
    /// 
    /// TODO: Add support for diagnostics, related issue https://github.com/dotnet/sdk/issues/26
    /// </summary>
    public class PreprocessPackageDependenciesDesignTime : Task
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

        [Output]
        public ITaskItem[] DependenciesDesignTime { get; set; }

        private Dictionary<string, ItemMetadata> Targets { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> Packages { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> Assemblies { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> DependenciesWorld { get; set; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        public override bool Execute()
        {
            PopulateTargets();

            PopulatePackages();

            PopulateAssemblies();

            AddDependenciesToTheWorld(Packages, PackageDependencies);

            AddDependenciesToTheWorld(Assemblies, FileDependencies, (item) =>
            {
                var fileGroup = item.GetMetadata(MetadataKeys.FileGroup);
                return string.IsNullOrEmpty(fileGroup) || !fileGroup.Equals(CompileTimeAssemblyMetadata);
            });

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

            return true;
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
                Packages[packageDef.ItemSpec] = dependency;
            }
        }

        /// <summary>
        /// Adds assemblies and framework assemblies from FileDefinitons to dependencies world dictionary.
        /// </summary>
        private void PopulateAssemblies()
        {
            foreach (var fileDef in FileDefinitions)
            {
                var dependencyType = GetDependencyType(fileDef.GetMetadata(MetadataKeys.Type));
                if (dependencyType != DependencyType.Assembly &&
                    dependencyType != DependencyType.FrameworkAssembly)
                {
                    continue;
                }

                var name = Path.GetFileName(fileDef.ItemSpec);
                var assembly = new AssemblyMetadata(dependencyType, fileDef, name);
                Assemblies[fileDef.ItemSpec] = assembly;
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

                var parentTargetId = dependency.GetMetadata(MetadataKeys.ParentTarget);
                if (parentTargetId.Contains("/") || !Targets.Keys.Contains(parentTargetId))
                {
                    // skip "target/rid"s and only consume actual targets and ignore non-existent parent targets
                    continue;
                }

                var parentPackageId = dependency.GetMetadata(MetadataKeys.ParentPackage);
                if (!string.IsNullOrEmpty(parentPackageId) && !Packages.Keys.Contains(parentPackageId))
                {
                    // ignore non-existent parent packages
                    continue;
                }

                var currentPackageUniqueId = $"{parentTargetId}/{currentItemId}";
                // add current package to dependencies world
                DependenciesWorld[currentPackageUniqueId] = items[currentItemId];

                // update parent
                var parentDependencyId = $"{parentTargetId}/{parentPackageId}".Trim('/');
                ItemMetadata parentDependency = null;
                if (DependenciesWorld.TryGetValue(parentDependencyId, out parentDependency))
                {

                    parentDependency.Dependencies.Add(currentItemId);
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

            public override IDictionary<string, string> ToDictionary()
            {
                return new Dictionary<string, string>
                {
                    { MetadataKeys.Name, Name },
                    { MetadataKeys.Version, Version },
                    { MetadataKeys.Path, Path },
                    { MetadataKeys.Type, Type.ToString() },
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
    }
}