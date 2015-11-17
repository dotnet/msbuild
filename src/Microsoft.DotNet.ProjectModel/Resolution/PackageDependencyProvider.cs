// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.ProjectModel.Graph;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Resolution
{
    public class PackageDependencyProvider
    {
        private readonly VersionFolderPathResolver _packagePathResolver;

        public PackageDependencyProvider(string packagesPath)
        {
            _packagePathResolver = new VersionFolderPathResolver(packagesPath);
        }

        public PackageDescription GetDescription(LockFilePackageLibrary package, LockFileTargetLibrary targetLibrary)
        {
            // If a NuGet dependency is supposed to provide assemblies but there is no assembly compatible with
            // current target framework, we should mark this dependency as unresolved
            var containsAssembly = package.Files
                .Any(x => x.StartsWith($"ref{Path.DirectorySeparatorChar}") ||
                    x.StartsWith($"lib{Path.DirectorySeparatorChar}"));

            var compatible = targetLibrary.FrameworkAssemblies.Any() ||
                targetLibrary.CompileTimeAssemblies.Any() ||
                targetLibrary.RuntimeAssemblies.Any() ||
                !containsAssembly;

            var dependencies = new List<LibraryRange>(targetLibrary.Dependencies.Count + targetLibrary.FrameworkAssemblies.Count);
            PopulateDependencies(dependencies, targetLibrary);

            var path = _packagePathResolver.GetInstallPath(package.Name, package.Version);

            var packageDescription = new PackageDescription(
                path,
                package,
                targetLibrary,
                dependencies,
                compatible);

            return packageDescription;
        }

        private void PopulateDependencies(List<LibraryRange> dependencies, LockFileTargetLibrary targetLibrary)
        {
            foreach (var dependency in targetLibrary.Dependencies)
            {
                dependencies.Add(new LibraryRange(
                    dependency.Id,
                    dependency.VersionRange,
                    LibraryType.Unspecified,
                    LibraryDependencyType.Default));
            }

            foreach (var frameworkAssembly in targetLibrary.FrameworkAssemblies)
            {
                dependencies.Add(new LibraryRange(
                    frameworkAssembly, 
                    LibraryType.ReferenceAssembly, 
                    LibraryDependencyType.Default));
            }
        }

        public static string ResolvePackagesPath(string rootDirectory, GlobalSettings settings)
        {
            // Order
            // 1. global.json { "packages": "..." }
            // 2. EnvironmentNames.Packages environment variable
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. {DefaultLocalRuntimeHomeDir}\packages

            if (!string.IsNullOrEmpty(settings?.PackagesPath))
            {
                return Path.Combine(rootDirectory, settings.PackagesPath);
            }

            var runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesCache);

            if (!string.IsNullOrEmpty(runtimePackages))
            {
                return runtimePackages;
            }

            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            // TODO(anurse): This should migrate to the NuGet packages directory
            return Path.Combine(profileDirectory, ".dnx", "packages");
        }
    }
}
