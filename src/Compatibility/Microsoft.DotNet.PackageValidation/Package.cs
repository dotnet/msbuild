// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NuGet.Client;
using NuGet.ContentModel;
using NuGet.Frameworks;
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
        private ContentItemCollection _packageAssets = new();
        private string _packagePath;

        public Package(string packageId, string version, IEnumerable<string> packageAssets, Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies, RuntimeGraph runtimeGraph)
        {
            PackageId = packageId;
            Version = version;
            PackageDependencies = packageDependencies;
            _packageAssets.Load(packageAssets);
            _conventions = new ManagedCodeConventions(runtimeGraph);

            PackageAssets = _packageAssets.FindItems(_conventions.Patterns.AnyTargettedFile);
            RefAssets = _packageAssets.FindItems(_conventions.Patterns.CompileRefAssemblies);
            LibAssets = _packageAssets.FindItems(_conventions.Patterns.CompileLibAssemblies);
            CompileAssets = RefAssets.Any() ? RefAssets : LibAssets;

            RuntimeSpecificAssets = _packageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies).Where(t => t.Path.StartsWith("runtimes"));
            RuntimeAssets = _packageAssets.FindItems(_conventions.Patterns.RuntimeAssemblies);

            Rids = RuntimeSpecificAssets?.Select(t => (string)t.Properties["rid"]);

            List<NuGetFramework> FrameworksInPackageList = CompileAssets?.Select(t => (NuGetFramework)t.Properties["tfm"]).ToList();
            FrameworksInPackageList.AddRange(RuntimeAssets?.Select(t => (NuGetFramework)t.Properties["tfm"]).ToList());
            FrameworksInPackage = FrameworksInPackageList.Distinct();
        }

        /// <summary>
        /// The name of the package
        /// </summary>
        public string PackageId { get; private set; }

        /// <summary>
        /// The version of the package.
        /// </summary>
        public string Version { get; private set; }

        /// <summary>
        /// Local path of the package.
        /// </summary>
        public string PackagePath 
        {
            get
            {
                return _packagePath;
            }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException(Resources.EmptyPackagePath);
                }

                if (!File.Exists(Path.GetFullPath(value)))
                {
                    throw new FileNotFoundException(string.Format(Resources.NonExistentPackagePath, value));
                }
                _packagePath = value;
            }
        }

        public Dictionary<NuGetFramework, IEnumerable<PackageDependency>> PackageDependencies { get; set; }

        /// <summary>
        /// List of compile assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> CompileAssets { get; private set; }

        /// <summary>
        /// List of assets under ref in the package.
        /// </summary>
        public IEnumerable<ContentItem> RefAssets { get; private set; }

        /// <summary>
        /// List of assets under lib in the package.
        /// </summary>

        public IEnumerable<ContentItem> LibAssets { get; private set; }
        
        /// <summary>
        /// List of all the assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> PackageAssets { get; private set; }

        /// <summary>
        /// List of all the runtime specific assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> RuntimeSpecificAssets { get; private set; }

        /// <summary>
        /// List of all the runtime assets in the package.
        /// </summary>
        public IEnumerable<ContentItem> RuntimeAssets { get; private set; }

        /// <summary>
        /// Checks if there are any assemblies under ref folder in the package.
        /// </summary>
        public bool HasRefAssemblies => RefAssets.Any();

        /// <summary>
        /// List of rids in the package.
        /// </summary>
        public IEnumerable<string> Rids { get; private set; }

        /// <summary>
        /// List of the frameworks in the package.
        /// </summary>
        public IEnumerable<NuGetFramework> FrameworksInPackage { get; private set; }

        /// <summary>
        /// Finds the best runtime asset for for a specific framework.
        /// </summary>
        /// <param name="framework">The framework where the package needs to be installed.</param>
        /// <returns>A ContentItem representing the best runtime asset</returns>
        public ContentItem FindBestRuntimeAssetForFramework(NuGetFramework framework)
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
        public ContentItem FindBestRuntimeAssetForFrameworkAndRuntime(NuGetFramework framework, string rid)
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
        public ContentItem FindBestCompileAssetForFramework(NuGetFramework framework)
        {
            SelectionCriteria managedCriteria = _conventions.Criteria.ForFramework(framework);
            if (RefAssets.Any())
            {
                return _packageAssets.FindBestItemGroup(managedCriteria,
                    _conventions.Patterns.CompileRefAssemblies)?.Items.FirstOrDefault(); ;
            }
            else
            {
                return _packageAssets.FindBestItemGroup(managedCriteria,
                    _conventions.Patterns.CompileLibAssemblies)?.Items.FirstOrDefault(); ;
                
            }
        }
    }
}
