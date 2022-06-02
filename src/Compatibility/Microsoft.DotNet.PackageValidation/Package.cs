// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// This class represents a dotnet package.
    /// </summary>
    public class Package
    {
        private readonly ManagedCodeConventions _conventions;
        private readonly ContentItemCollection _packageAssets = new();

        /// <summary>
        /// The name of the package
        /// </summary>
        public string PackageId { get; }

        /// <summary>
        /// The version of the package.
        /// </summary>
        public string Version { get; }

        /// <summary>
        /// Local path of the package.
        /// </summary>
        public string PackagePath { get; }

        /// <summary>
        /// List of package dependencies per framework.
        /// </summary>
        public Dictionary<NuGetFramework, IEnumerable<PackageDependency>> PackageDependencies { get; }

        /// <summary>
        /// List of compile assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> CompileAssets { get; }

        /// <summary>
        /// List of assets under ref in the package.
        /// </summary>
        public IEnumerable<ContentItem> RefAssets { get; }

        /// <summary>
        /// List of assets under lib in the package.
        /// </summary>

        public IEnumerable<ContentItem> LibAssets { get; }

        /// <summary>
        /// List of all the assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> PackageAssets { get; }

        /// <summary>
        /// List of all the runtime specific assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> RuntimeSpecificAssets { get; }

        /// <summary>
        /// List of all the runtime assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> RuntimeAssets { get; }

        /// <summary>
        /// List of rids in the package.
        /// </summary>
        public IEnumerable<string> Rids { get; }

        /// <summary>
        /// List of the frameworks in the package.
        /// </summary>
        public IEnumerable<NuGetFramework> FrameworksInPackage { get; }

        internal Package(string packagePath, string packageId, string version, IEnumerable<string> packageAssets, Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies, RuntimeGraph? runtimeGraph)
        {
            PackagePath = packagePath;
            PackageId = packageId;
            Version = version;
            PackageDependencies = packageDependencies;

            _packageAssets.Load(packageAssets);
            _conventions = new ManagedCodeConventions(runtimeGraph);

            PackageAssets = _packageAssets.FindItems(_conventions.Patterns.AnyTargettedFile);
            RefAssets = _packageAssets.FindItems(_conventions.Patterns.CompileRefAssemblies);
            LibAssets = _packageAssets.FindItems(_conventions.Patterns.CompileLibAssemblies);
            CompileAssets = RefAssets.Any() ? RefAssets : LibAssets;
            RuntimeAssets = _packageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies);
            RuntimeSpecificAssets = RuntimeAssets.Where(t => t.Path.StartsWith("runtimes")).ToArray();
            Rids = RuntimeSpecificAssets.Select(t => (string)t.Properties["rid"]).ToArray();
            FrameworksInPackage = CompileAssets.Select(t => (NuGetFramework)t.Properties["tfm"])
                .Concat(RuntimeAssets.Select(t => (NuGetFramework)t.Properties["tfm"]))
                .Distinct()
                .ToArray();
        }

        /// <summary>
        /// Creates the package object from package path.
        /// </summary>
        /// <param name="packagePath">The path to the package path.</param>
        /// <param name="runtimeGraph">The path to the the runtime graph.</param>
        /// <returns>The package object.</returns>
        public static Package Create(string? packagePath, string? runtimeGraph)
        {
            if (string.IsNullOrEmpty(packagePath))
            {
                throw new ArgumentException(Resources.EmptyPackagePath);
            }

            if (!File.Exists(Path.GetFullPath(packagePath)))
            {
                throw new FileNotFoundException(string.Format(Resources.NonExistentPackagePath, packagePath));
            }

            using PackageArchiveReader packageReader = new(packagePath);
            NuspecReader nuspecReader = packageReader.NuspecReader;
            string packageId = nuspecReader.GetId();
            string version = nuspecReader.GetVersion().ToString();
            IEnumerable<string> packageAssets = packageReader.GetFiles().Where(t => t.EndsWith(packageId + ".dll")).ToArray();

            Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies = new();
            foreach (PackageDependencyGroup item in nuspecReader.GetDependencyGroups())
            {
                packageDependencies.Add(item.TargetFramework, item.Packages);
            }

            RuntimeGraph? nugetRuntimeGraph = null;
            if (!string.IsNullOrEmpty(runtimeGraph))
            {
                nugetRuntimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(runtimeGraph);
            }

            return new Package(packagePath!, packageId, version, packageAssets, packageDependencies, nugetRuntimeGraph);
        }

        /// <summary>
        /// Finds the best runtime asset for for a specific framework.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <returns>A ContentItem representing the best runtime asset</returns>
        public ContentItem? FindBestRuntimeAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            return _packageAssets.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items.FirstOrDefault();
        }

        /// <summary>
        /// Finds the best runtime asset for a framework-rid pair.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <param name="rid">The rid where the package needs to be installed.</param>
        /// <returns>A ContentItem representing the best runtime asset</returns>
        public ContentItem? FindBestRuntimeAssetForFrameworkAndRuntime(NuGetFramework framework, string rid)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFrameworkAndRuntime(framework, rid);
            return _packageAssets.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items.FirstOrDefault();
        }

        /// <summary>
        /// Finds the best compile time assset for a specific framework.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <returns>A ContentItem representing the best compile time asset.</returns>
        public ContentItem? FindBestCompileAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            PatternSet patternSet = RefAssets.Any() ?
                _conventions.Patterns.CompileRefAssemblies :
                _conventions.Patterns.CompileLibAssemblies;

            return _packageAssets.FindBestItemGroup(managedCriteria, patternSet)?.Items.FirstOrDefault();
        }
    }
}
