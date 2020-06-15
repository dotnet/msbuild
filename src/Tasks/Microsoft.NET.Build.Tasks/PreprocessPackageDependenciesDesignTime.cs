// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.NET.Build.Tasks
{
    /// <summary>
    /// Filters and projects items produced by <see cref="ResolvePackageDependencies"/> for consumption by
    /// the dependencies tree, via design-time builds.
    /// </summary>
    /// <remarks>
    /// Only top-level package references are retained (i.e. those referenced directly by the project, not
    /// those only brought in transitively).
    ///
    /// Only package references applicable to <see cref="TargetFrameworkMoniker"/> are retained.
    /// 
    /// Changes to the implementation of this class must be coordinated with <c>PackageRuleHandler</c>
    /// in the dotnet/project-system repo.
    /// </remarks>
    public class PreprocessPackageDependenciesDesignTime : TaskBase
    {
        public const string ResolvedMetadata = "Resolved";

        /// <summary>
        /// Information about each package in the project, with metadata:
        /// - Name = "MetadataExtractor"
        /// - Path = "metadataextractor/1.0.0"
        /// - ResolvedPath = "C:\Users\drnoakes\.nuget\packages\metadataextractor\1.0.0"
        /// - Type = "package"
        /// - Version = "2.3.0"
        /// - DiagnosticLevel = ""
        /// </summary>
        [Required]
        public ITaskItem[] PackageDefinitions { get; set; }

        /// <summary>
        /// Items with metadata "ParentTarget" and "ParentPackage", which allows determining the hierarchy of package references.
        /// </summary>
        [Required]
        public ITaskItem[] PackageDependencies { get; set; }

        /// <summary>
        /// Eg: "Microsoft.NETCore.App;NETStandard.Library"
        /// </summary>
        [Required]
        public string DefaultImplicitPackages { get; set; }

        /// <summary>
        /// Eg: ".NETCoreApp,Version=v5.0".
        /// Only packages targeting this framework will be returned.
        /// </summary>
        [Required]
        public string TargetFrameworkMoniker { get; set; }

        [Output]
        public ITaskItem[] PackageDependenciesDesignTime { get; private set; }

        protected override void ExecuteCore()
        {
            var implicitPackageReferences = GetImplicitPackageReferences(DefaultImplicitPackages);

            // We have two types of data:
            //
            // 1) "PackageDependencies" which place a package in a given target/hierarchy
            // 2) "PackageDefinitions" which provide general metadata about a package
            //
            // First, we scan PackageDependencies to build the set of packages in our target.

            var allowItemSpecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dependency in PackageDependencies)
            {
                if (dependency.HasMetadataValue(MetadataKeys.ParentPackage))
                {
                    // ignore non-top-level packages (those with ParentPackage)
                    continue;
                }

                var target = dependency.GetMetadata(MetadataKeys.ParentTarget);

                if (!StringComparer.OrdinalIgnoreCase.Equals(target, TargetFrameworkMoniker))
                {
                    // skip dependencies for other targets
                    continue;
                }

                allowItemSpecs.Add(dependency.ItemSpec);
            }

            // Second, find PackageDefinitions that match our allowed item specs

            var outputItems = new List<ITaskItem>(allowItemSpecs.Count);

            foreach (var packageDef in PackageDefinitions)
            {
                if (!allowItemSpecs.Contains(packageDef.ItemSpec))
                {
                    // We are not interested in this definition (not top-level, or wrong target)
                    continue;
                }

                var dependencyType = GetDependencyType(packageDef.GetMetadata(MetadataKeys.Type));
                
                if (dependencyType == DependencyType.Package ||
                    dependencyType == DependencyType.Unresolved)
                {
                    var name = packageDef.GetMetadata(MetadataKeys.Name);

                    if (string.IsNullOrEmpty(name))
                    {
                        // Name is required
                        continue;
                    }

                    var version = packageDef.GetMetadata(MetadataKeys.Version) ?? string.Empty;
                    var resolvedPath = packageDef.GetMetadata(MetadataKeys.ResolvedPath);
                    var resolved = !string.IsNullOrEmpty(resolvedPath);
                    var path = (resolved
                        ? resolvedPath
                        : packageDef.GetMetadata(MetadataKeys.Path)) ?? string.Empty;
                    var isImplicitlyDefined = implicitPackageReferences.Contains(name);
                    var diagnosticLevel = packageDef.GetMetadata(MetadataKeys.DiagnosticLevel) ?? string.Empty;

                    var outputItem = new TaskItem(packageDef.ItemSpec);
                    outputItem.SetMetadata(MetadataKeys.Name, name);
                    outputItem.SetMetadata(MetadataKeys.Version, version);
                    outputItem.SetMetadata(MetadataKeys.Path, path);
                    outputItem.SetMetadata(MetadataKeys.IsImplicitlyDefined, isImplicitlyDefined.ToString());
                    outputItem.SetMetadata(MetadataKeys.DiagnosticLevel, diagnosticLevel);
                    outputItem.SetMetadata(ResolvedMetadata, resolved.ToString());

                    outputItems.Add(outputItem);
                }
            }

            PackageDependenciesDesignTime = outputItems.ToArray();
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

        private static DependencyType GetDependencyType(string dependencyTypeString)
        {
            Enum.TryParse(dependencyTypeString, ignoreCase: true, out DependencyType dependencyType);
            return dependencyType;
        }
    }
}
