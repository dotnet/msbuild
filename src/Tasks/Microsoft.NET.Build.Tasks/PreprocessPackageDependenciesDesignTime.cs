// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Task combines data returned from <see cref="ResolvePackageDependencies"/> into single items collection
    /// that can be consumed by DesignTime build and contains all info needed to expand packages
    /// dependency graph.
    /// If any changes are made here, make sure corresponding changes are made to PackageRuleHandler
    /// in the dotnet/project-system repo and corresponding tests.
    /// </summary>
    public class PreprocessPackageDependenciesDesignTime : TaskBase
    {
        public const string DependenciesMetadata = "Dependencies";
        public const string ResolvedMetadata = "Resolved";
        public const string VisibleMetadata = "Visible";

        [Required]
        public ITaskItem[] TargetDefinitions { get; set; }

        [Required]
        public ITaskItem[] PackageDefinitions { get; set; }

        [Required]
        public ITaskItem[] PackageDependencies { get; set; }

        [Required]
        public ITaskItem[] FileDependencies { get; set; }

        [Required]
        public string DefaultImplicitPackages { get; set; }

        [Required]
        public ITaskItem[] References { get; set; }

        [Required]
        public string TargetFrameworkMoniker { get; set; }

        [Output]
        public ITaskItem[] DependenciesDesignTime { get; set; }

        private Dictionary<string, ItemMetadata> Targets { get; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> Packages { get; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, ItemMetadata> DependenciesWorld { get; }
                    = new Dictionary<string, ItemMetadata>(StringComparer.OrdinalIgnoreCase);

        protected override void ExecuteCore()
        {
            PopulateTargets();

            PopulatePackages();

            AddDependenciesToTheWorld(Packages, PackageDependencies);

            // prepare output collection: add corresponding metadata to ITaskItem based in item type
            DependenciesDesignTime = DependenciesWorld.Select(itemKvp =>
            {
                var newTaskItem = new TaskItem(itemKvp.Key);
                itemKvp.Value.SetMetadataOnTaskItem(newTaskItem);
                return newTaskItem;
            }).ToArray<ITaskItem>();
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
            var implicitPackageReferences = GetImplicitPackageReferences(DefaultImplicitPackages);

            foreach (var packageDef in PackageDefinitions)
            {
                var dependencyType = GetDependencyType(packageDef.GetMetadata(MetadataKeys.Type));
                if (dependencyType != DependencyType.Package &&
                    dependencyType != DependencyType.Unresolved)
                {
                    // we ignore all other dependency types since 
                    //      - assemblies we handle separately below 
                    //      - projects we don't care here, since they are sent to project system via other route
                    continue;
                }

                var dependency = new PackageMetadata(packageDef);
                dependency.IsImplicitlyDefined = implicitPackageReferences.Contains(dependency.Name);

                Packages[packageDef.ItemSpec] = dependency;
            }
        }

        private static DependencyType GetDependencyType(string dependencyTypeString)
        {
            Enum.TryParse(dependencyTypeString, ignoreCase: true, out DependencyType dependencyType);
            return dependencyType;
        }

        private void AddDependenciesToTheWorld(Dictionary<string, ItemMetadata> items,
                                               ITaskItem[] itemDependencies,
                                               Func<ITaskItem, bool> shouldSkipItemCheck = null)
        {
            foreach (var dependency in itemDependencies)
            {
                var currentItemId = dependency.ItemSpec;
                if (!items.ContainsKey(currentItemId))
                {
                    // if this package definition does not even exist - skip it
                    continue;
                }

                if (shouldSkipItemCheck != null && shouldSkipItemCheck(dependency))
                {
                    continue;
                }

                var parentTargetId = dependency.GetMetadata(MetadataKeys.ParentTarget) ?? string.Empty;
                if (parentTargetId.Contains("/") || !Targets.ContainsKey(parentTargetId))
                {
                    // skip "target/rid"s and only consume actual targets and ignore non-existent parent targets
                    continue;
                }

                var parentPackageId = dependency.GetMetadata(MetadataKeys.ParentPackage) ?? string.Empty;
                if (!string.IsNullOrEmpty(parentPackageId) && !Packages.ContainsKey(parentPackageId))
                {
                    // ignore non-existent parent packages
                    continue;
                }

                var currentPackageUniqueId = $"{parentTargetId}/{currentItemId}";
                // add current package to dependencies world
                var currentItem = GetItem(items, currentItemId);
                DependenciesWorld[currentPackageUniqueId] = currentItem;

                // update parent
                var parentDependencyId = $"{parentTargetId}/{parentPackageId}".Trim('/');
                if (DependenciesWorld.TryGetValue(parentDependencyId, out ItemMetadata parentDependency))
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
                        parentDependency = GetItem(Packages, parentPackageId);
                    }
                    else
                    {
                        parentDependency = GetItem(Targets, parentTargetId);
                        currentItem.IsTopLevelDependency = true;
                    }

                    parentDependency.Dependencies.Add(currentItemId);
                    DependenciesWorld[parentDependencyId] = parentDependency;
                }
            }

            return;

            ItemMetadata GetItem(Dictionary<string, ItemMetadata> items, string id)
            {
                return Targets.Count > 1 ? items[id].Clone() : items[id];
            }
        }

        private abstract class ItemMetadata
        {
            protected ItemMetadata(DependencyType type, IList<string> dependencies = null, bool isTopLevelDependency = false)
            {
                Type = type;
                Dependencies = dependencies == null ? new List<string>() : new List<string>(dependencies);
                IsTopLevelDependency = isTopLevelDependency;
            }

            public DependencyType Type { get; }
            public bool IsTopLevelDependency { get; set; }

            /// <summary>
            /// A list of name/version strings to specify dependency identities.
            /// Note: identity here is just a "name/version" and does not have TFM part in front.
            /// </summary>
            public List<string> Dependencies { get; }

            /// <summary>
            /// Populates metadata on <paramref name="taskItem"/>.
            /// </summary>
            public abstract void SetMetadataOnTaskItem(TaskItem taskItem);

            /// <summary>
            /// Creates a copy of the item
            /// </summary>
            public abstract ItemMetadata Clone();
        }

        private class TargetMetadata : ItemMetadata
        {
            public TargetMetadata(ITaskItem item)
                : base(DependencyType.Target)
            {
                RuntimeIdentifier = item.GetMetadata(MetadataKeys.RuntimeIdentifier) ?? string.Empty;
                TargetFrameworkMoniker = item.GetMetadata(MetadataKeys.TargetFrameworkMoniker) ?? string.Empty;
                FrameworkName = item.GetMetadata(MetadataKeys.FrameworkName) ?? string.Empty;
                FrameworkVersion = item.GetMetadata(MetadataKeys.FrameworkVersion) ?? string.Empty;
            }

            private TargetMetadata(
                DependencyType type,
                IList<string> dependencies,
                bool isTopLevelDependency,
                string runtimeIdentifier,
                string targetFrameworkMoniker,
                string frameworkName,
                string frameworkVersion)
                : base(type, dependencies, isTopLevelDependency)
            {
                RuntimeIdentifier = runtimeIdentifier;
                TargetFrameworkMoniker = targetFrameworkMoniker;
                FrameworkName = frameworkName;
                FrameworkVersion = frameworkVersion;
            }

            public string RuntimeIdentifier { get; }
            public string TargetFrameworkMoniker { get; }
            public string FrameworkName { get; }
            public string FrameworkVersion { get; }

            public override void SetMetadataOnTaskItem(TaskItem taskItem)
            {
                taskItem.SetMetadata(MetadataKeys.RuntimeIdentifier, RuntimeIdentifier);
                taskItem.SetMetadata(MetadataKeys.TargetFrameworkMoniker, TargetFrameworkMoniker);
                taskItem.SetMetadata(MetadataKeys.FrameworkName, FrameworkName);
                taskItem.SetMetadata(MetadataKeys.FrameworkVersion, FrameworkVersion);
                taskItem.SetMetadata(MetadataKeys.Type, Type.ToString());
                taskItem.SetMetadata(DependenciesMetadata, string.Join(";", Dependencies));
            }

            public override ItemMetadata Clone()
            {
                return new TargetMetadata(
                    Type,
                    Dependencies,
                    IsTopLevelDependency,
                    RuntimeIdentifier,
                    TargetFrameworkMoniker,
                    FrameworkName,
                    FrameworkVersion);
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

            private PackageMetadata(
                DependencyType type,
                IList<string> dependencies,
                bool isTopLevelDependency,
                string name,
                string version,
                string path,
                bool resolved,
                bool isImplicitlyDefined)
                : base(type, dependencies, isTopLevelDependency)
            {
                Name = name;
                Version = version;
                Path = path;
                Resolved = resolved;
                IsImplicitlyDefined = isImplicitlyDefined;
            }

            public string Name { get; }
            public string Version { get; }
            public string Path { get; }
            public bool Resolved { get; }
            public bool IsImplicitlyDefined { get; set; }

            public override void SetMetadataOnTaskItem(TaskItem taskItem)
            {
                taskItem.SetMetadata(MetadataKeys.Name, Name);
                taskItem.SetMetadata(MetadataKeys.Version, Version);
                taskItem.SetMetadata(MetadataKeys.Path, Path);
                taskItem.SetMetadata(MetadataKeys.Type, Type.ToString());
                taskItem.SetMetadata(MetadataKeys.IsImplicitlyDefined, IsImplicitlyDefined.ToString());
                taskItem.SetMetadata(MetadataKeys.IsTopLevelDependency, IsTopLevelDependency.ToString());
                taskItem.SetMetadata(ResolvedMetadata, Resolved.ToString());
                taskItem.SetMetadata(DependenciesMetadata, string.Join(";", Dependencies));
            }

            public override ItemMetadata Clone()
            {
                return new PackageMetadata(
                    Type,
                    Dependencies,
                    IsTopLevelDependency,
                    Name,
                    Version,
                    Path,
                    Resolved,
                    IsImplicitlyDefined);
            }
        }

        internal static HashSet<string> GetImplicitPackageReferences(string defaultImplicitPackages)
        {
            var implicitPackageReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(defaultImplicitPackages))
            {
                return implicitPackageReferences;
            }

            var packageNames = defaultImplicitPackages.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (packageNames.Length == 0)
            {
                return implicitPackageReferences;
            }

            foreach (var packageReference in packageNames)
            {
                implicitPackageReferences.Add(packageReference);
            }

            return implicitPackageReferences;
        }
    }
}
