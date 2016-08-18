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
    /// If any changes made here, make sure corresponding changes are made to NuGetDependenciesSubTreeProvider
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

        private Dictionary<string, DependencyMetadata> Targets { get; set; }
                    = new Dictionary<string, DependencyMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, DependencyMetadata> Packages { get; set; }
                    = new Dictionary<string, DependencyMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, DependencyMetadata> Assemblies { get; set; }
                    = new Dictionary<string, DependencyMetadata>(StringComparer.OrdinalIgnoreCase);

        private Dictionary<string, DependencyMetadata> DependenciesWorld { get; set; }
                    = new Dictionary<string, DependencyMetadata>(StringComparer.OrdinalIgnoreCase);

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

            // prepare output collection
            DependenciesDesignTime = DependenciesWorld.Select(kvp =>
            {
                var newTaskItem = new TaskItem(kvp.Key);
                newTaskItem.SetMetadata(MetadataKeys.RuntimeIdentifier, kvp.Value.RuntimeIdentifier);
                newTaskItem.SetMetadata(MetadataKeys.TargetFrameworkMoniker, kvp.Value.TargetFrameworkMoniker);
                newTaskItem.SetMetadata(MetadataKeys.FrameworkName, kvp.Value.FrameworkName);
                newTaskItem.SetMetadata(MetadataKeys.FrameworkVersion, kvp.Value.FrameworkVersion);
                newTaskItem.SetMetadata(MetadataKeys.Name, kvp.Value.Name);
                newTaskItem.SetMetadata(MetadataKeys.Version, kvp.Value.Version);
                newTaskItem.SetMetadata(MetadataKeys.Path, kvp.Value.Path);
                newTaskItem.SetMetadata(MetadataKeys.Type, kvp.Value.DependencyType.ToString());
                newTaskItem.SetMetadata(ResolvedMetadata, kvp.Value.Resolved.ToString());
                newTaskItem.SetMetadata(DependenciesMetadata, string.Join(";", kvp.Value.Dependencies));

                return newTaskItem;
            }).ToArray();

            return true;
        }

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

                var target = new DependencyMetadata(
                                runtimeIdentifier: targetDef.GetMetadata(MetadataKeys.RuntimeIdentifier),
                                targetFrameworkMoniker: targetDef.GetMetadata(MetadataKeys.TargetFrameworkMoniker),
                                frameworkName: targetDef.GetMetadata(MetadataKeys.FrameworkName),
                                frameworkVersion: targetDef.GetMetadata(MetadataKeys.FrameworkVersion),
                                type: dependencyType);
                Targets[targetDef.ItemSpec] = target;

                // add target to the world now, since it does not have parents
                DependenciesWorld[targetDef.ItemSpec] = target;
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

        private void PopulatePackages()
        {
            // populate unique packages 
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

                var dependency = new DependencyMetadata(name: packageDef.GetMetadata(MetadataKeys.Name),
                                                        version: packageDef.GetMetadata(MetadataKeys.Version),
                                                        type: DependencyType.Package,
                                                        path: packageDef.GetMetadata(MetadataKeys.Path),
                                                        resolvedPath: packageDef.GetMetadata(MetadataKeys.ResolvedPath));
                Packages[packageDef.ItemSpec] = dependency;
            }
        }

        private void PopulateAssemblies()
        {
            // populate unique assembly files 
            foreach (var fileDef in FileDefinitions)
            {
                var dependencyType = GetDependencyType(fileDef.GetMetadata(MetadataKeys.Type));
                if (dependencyType != DependencyType.Assembly &&
                    dependencyType != DependencyType.FrameworkAssembly)
                {
                    continue;
                }

                var name = Path.GetFileName(fileDef.ItemSpec);
                var assembly = new DependencyMetadata(name: name,
                                                      type: dependencyType,
                                                      path: fileDef.GetMetadata(MetadataKeys.Path),
                                                      resolvedPath: fileDef.GetMetadata(MetadataKeys.ResolvedPath));
                Assemblies[fileDef.ItemSpec] = assembly;
            }
        }

        private void AddDependenciesToTheWorld(Dictionary<string, DependencyMetadata> items,
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

                if (shouldSkipItemCheck != null && shouldSkipItemCheck.Invoke(dependency))
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
                DependencyMetadata parentDependency = null;
                if (DependenciesWorld.TryGetValue(parentDependencyId, out parentDependency))
                {

                    parentDependency.Dependencies.Add(currentItemId);
                }
                else
                {
                    // create new parent
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

        private class DependencyMetadata
        {
            public DependencyMetadata(string name = null,
                                      string version = null,
                                      DependencyType type = DependencyType.Unknown,
                                      string path = null,
                                      string resolvedPath = null,
                                      string runtimeIdentifier = null,
                                      string targetFrameworkMoniker = null,
                                      string frameworkName = null,
                                      string frameworkVersion = null)
            {
                Name = name ?? string.Empty;
                Version = version ?? string.Empty;
                DependencyType = type;

                Resolved = DependencyType != DependencyType.Unknown && !string.IsNullOrEmpty(resolvedPath);

                Path = Resolved 
                        ? resolvedPath
                        : path ?? string.Empty;

                RuntimeIdentifier = runtimeIdentifier ?? string.Empty;
                TargetFrameworkMoniker = targetFrameworkMoniker ?? string.Empty;
                FrameworkName = frameworkName ?? string.Empty;
                FrameworkVersion = frameworkVersion ?? string.Empty;

                Dependencies = new List<string>();
            }

            // dependency properties
            public string Name { get; private set; }
            public string Version { get; private set; }
            public DependencyType DependencyType { get; private set; }
            public string Path { get; private set; }
            public bool Resolved { get; private set; }

            // target framework properties
            public string RuntimeIdentifier { get; private set; }
            public string TargetFrameworkMoniker { get; private set; }
            public string FrameworkName { get; private set; }
            public string FrameworkVersion { get; private set; }

            /// <summary>
            /// a list of name/version strings to specify dependencies identities
            /// </summary>
            public IList<string> Dependencies { get; private set; }
        }
    }
}