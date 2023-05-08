// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.RuntimeModel;

namespace Microsoft.DotNet.PackageValidation
{
    /// <summary>
    /// Creates a package object from the nupkg package.
    /// </summary>
    public class NupkgParser
    {
        /// <summary>
        /// Creates the package object from package path.
        /// </summary>
        /// <param name="packagePath">The path to the package path.</param>
        /// <param name="runtimeGraph">The path to the the runtime graph.</param>
        /// <param name="assemblyName">The name of the assembly to be used for API comparisons.</param>
        /// <returns>The package object.</returns>
        public static Package CreatePackage(string packagePath, RuntimeGraph runtimeGraph, string assemblyName)
        {
            using (PackageArchiveReader packageReader = new PackageArchiveReader(packagePath))
            {
                Package package = CreatePackage(packageReader, runtimeGraph, assemblyName);
                package.PackagePath = packagePath;
                return package;
            }
        }

        private static Package CreatePackage(PackageArchiveReader packageReader, RuntimeGraph runtimeGraph, string assemblyName)
        {
            NuspecReader nuspecReader = packageReader.NuspecReader;
            string packageId = nuspecReader.GetId();
            string version = nuspecReader.GetVersion().ToString();
            IEnumerable<PackageDependencyGroup> dependencyGroups = nuspecReader.GetDependencyGroups();
            
            Dictionary<NuGetFramework, IEnumerable<PackageDependency>> packageDependencies = new Dictionary<NuGetFramework, IEnumerable<PackageDependency>>();
            foreach (PackageDependencyGroup item in dependencyGroups)
            {
                packageDependencies.Add(item.TargetFramework, item.Packages);
            }

            return new Package(packageId, version, packageReader.GetFiles()?.Where(t => t.EndsWith((assemblyName ?? packageId) + ".dll")), packageDependencies, runtimeGraph);
        }
    }
}
