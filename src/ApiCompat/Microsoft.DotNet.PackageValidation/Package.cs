// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// This class represents a NuGet package.
    /// </summary>
    public class Package
    {
        private readonly ContentItemCollection _contentItemCollection;
        private readonly ManagedCodeConventions _conventions;
        private static RuntimeGraph? s_runtimeGraph;

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
        /// List of assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> PackageAssets { get; }

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
        /// List of assembly references grouped by target framework.
        /// </summary>
        public Dictionary<NuGetFramework, IEnumerable<string>>? AssemblyReferences { get; }

        /// <summary>
        /// List of the frameworks in the package.
        /// </summary>
        public IReadOnlyList<NuGetFramework> FrameworksInPackage { get; }

        public Package(string packagePath,
            string packageId,
            string version,
            IEnumerable<string> packageAssets,
            Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies,
            Dictionary<NuGetFramework, IEnumerable<string>>? assemblyReferences = null)
        {
            PackagePath = packagePath;
            PackageId = packageId;
            Version = version;
            PackageDependencies = packageDependencies;

            _conventions = new ManagedCodeConventions(s_runtimeGraph);
            _contentItemCollection = new ContentItemCollection();
            _contentItemCollection.Load(packageAssets);

            PackageAssets = _contentItemCollection.FindItems(_conventions.Patterns.AnyTargettedFile);
            RefAssets = _contentItemCollection.FindItems(_conventions.Patterns.CompileRefAssemblies);
            LibAssets = _contentItemCollection.FindItems(_conventions.Patterns.CompileLibAssemblies);
            CompileAssets = RefAssets.Any() ? RefAssets : LibAssets;
            RuntimeAssets = _contentItemCollection.FindItems(_conventions.Patterns.RuntimeAssemblies);
            RuntimeSpecificAssets = RuntimeAssets.Where(t => t.Path.StartsWith("runtimes")).ToArray();
            Rids = RuntimeSpecificAssets.Select(t => (string)t.Properties["rid"])
                .Distinct()
                .ToArray();
            FrameworksInPackage = CompileAssets.Select(t => (NuGetFramework)t.Properties["tfm"])
                .Concat(RuntimeAssets.Select(t => (NuGetFramework)t.Properties["tfm"]))
                .Distinct()
                .ToArray();
            AssemblyReferences = assemblyReferences;
        }

        public static void InitializeRuntimeGraph(string runtimeGraph)
        {
            s_runtimeGraph = JsonRuntimeFormat.ReadRuntimeGraph(runtimeGraph);
        }

        /// <summary>
        /// Creates a package object from a given package path and optional assembly references.
        /// </summary>
        /// <param name="packagePath">The path to the package path.</param>
        /// <param name="packageAssemblyReferences">Optional assembly references with target framework information.</param>
        public static Package Create(string? packagePath, Dictionary<NuGetFramework, IEnumerable<string>>? packageAssemblyReferences = null)
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
            IEnumerable<string> packageAssets = packageReader.GetFiles().Where(t => t.EndsWith(".dll")).ToArray();

            Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies = new();
            foreach (PackageDependencyGroup item in nuspecReader.GetDependencyGroups())
            {
                packageDependencies.Add(item.TargetFramework, item.Packages);
            }

            return new Package(packagePath!, packageId, version, packageAssets, packageDependencies, packageAssemblyReferences);
        }

        /// <summary>
        /// Finds the best runtime asset for for a specific framework.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <returns>A ContentItem representing the best runtime asset</returns>
        public IReadOnlyList<ContentItem>? FindBestRuntimeAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            IList<ContentItem>? items = _contentItemCollection.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items;

            return items != null ?
                new ReadOnlyCollection<ContentItem>(items.Where(t => !t.Path.StartsWith("runtimes")).ToArray()) :
                null;
        }

        /// <summary>
        /// Finds the best runtime specific asset for for a specific framework.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <returns>A ContentItem representing the best runtime specific asset.</returns>
        public IReadOnlyList<ContentItem>? FindBestRuntimeSpecificAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            IList<ContentItem>? items = _contentItemCollection.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items;

            return items != null ?
                new ReadOnlyCollection<ContentItem>(items.Where(t => t.Path.StartsWith("runtimes")).ToArray()) :
                null;
        }

        /// <summary>
        /// Finds the best runtime asset for a framework-rid pair.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <param name="rid">The rid where the package needs to be installed.</param>
        /// <returns>A ContentItem representing the best runtime asset</returns>
        public IReadOnlyList<ContentItem>? FindBestRuntimeAssetForFrameworkAndRuntime(NuGetFramework framework, string rid)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFrameworkAndRuntime(framework, rid);
            IList<ContentItem>? items = _contentItemCollection.FindBestItemGroup(managedCriteria,
                _conventions.Patterns.RuntimeAssemblies)?.Items;

            return items != null ?
                new ReadOnlyCollection<ContentItem>(items) :
                null;
        }

        /// <summary>
        /// Finds the best compile time asset for a specific framework.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <returns>A ContentItem representing the best compile time asset.</returns>
        public IReadOnlyList<ContentItem>? FindBestCompileAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            PatternSet patternSet = RefAssets.Any() ?
                _conventions.Patterns.CompileRefAssemblies :
                _conventions.Patterns.CompileLibAssemblies;
            IList<ContentItem>? items = _contentItemCollection.FindBestItemGroup(managedCriteria, patternSet)?.Items;

            return items != null ?
                new ReadOnlyCollection<ContentItem>(items) :
                null;
        }

        /// <summary>
        /// Finds the best assembly references for a specific framework.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <returns>The assembly references for the specified framework.</returns>
        public IEnumerable<string>? FindBestAssemblyReferencesForFramework(NuGetFramework framework)
        {
            if (AssemblyReferences is null)
                return null;

            // Fast path: return for direct matches
            if (AssemblyReferences.TryGetValue(framework, out IEnumerable<string>? references))
            {
                return references;
            }

            // Search for the nearest newer assembly references framework.
            Queue<NuGetFramework> tfmQueue = new(AssemblyReferences.Keys);
            while (tfmQueue.Count > 0)
            {
                NuGetFramework assemblyReferencesFramework = tfmQueue.Dequeue();

                NuGetFramework? bestAssemblyReferencesFramework = NuGetFrameworkUtility.GetNearest(tfmQueue.Concat(new NuGetFramework[] { framework }), assemblyReferencesFramework, (key) => key);
                if (bestAssemblyReferencesFramework == framework)
                {
                    return AssemblyReferences[assemblyReferencesFramework];
                }
            }

            return null;
        }
    }
}
