// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.DotNet.ProjectModel.Graph;
using NuGet.Frameworks;
using NuGet.Packaging;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public class PackageDependencyProvider
    {
        private readonly VersionFolderPathResolver _packagePathResolver;
        private readonly FrameworkReferenceResolver _frameworkReferenceResolver;

        public PackageDependencyProvider(string packagesPath, FrameworkReferenceResolver frameworkReferenceResolver)
        {
            _packagePathResolver = new VersionFolderPathResolver(packagesPath);
            _frameworkReferenceResolver = frameworkReferenceResolver;
        }

        public PackageDescription GetDescription(NuGetFramework targetFramework, LockFilePackageLibrary package, LockFileTargetLibrary targetLibrary)
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
            PopulateDependencies(dependencies, targetLibrary, targetFramework);

            var path = _packagePathResolver.GetInstallPath(package.Name, package.Version);
            var exists = Directory.Exists(path);

            if (exists)
            {
                // If the package's compile time assemblies is for a portable profile then, read the assembly metadata
                // and turn System.* references into reference assembly dependencies
                PopulateLegacyPortableDependencies(targetFramework, dependencies, path, targetLibrary);
            }

            var packageDescription = new PackageDescription(
                path,
                package,
                targetLibrary,
                dependencies,
                compatible,
                resolved: compatible && exists);

            return packageDescription;
        }

        private void PopulateLegacyPortableDependencies(NuGetFramework targetFramework, List<LibraryRange> dependencies, string packagePath, LockFileTargetLibrary targetLibrary)
        {
            var seen = new HashSet<string>();

            foreach (var assembly in targetLibrary.CompileTimeAssemblies)
            {
                if (IsPlaceholderFile(assembly))
                {
                    continue;
                }

                // (ref/lib)/{tfm}/{assembly}
                var pathParts = assembly.Path.Split(Path.DirectorySeparatorChar);

                if (pathParts.Length != 3)
                {
                    continue;
                }

                var assemblyTargetFramework = NuGetFramework.Parse(pathParts[1]);

                if (!assemblyTargetFramework.IsPCL)
                {
                    continue;
                }

                var assemblyPath = Path.Combine(packagePath, assembly.Path);

                foreach (var dependency in GetDependencies(assemblyPath))
                {
                    if (seen.Add(dependency))
                    {
                        string path;
                        Version version;

                        // If there exists a reference assembly on the requested framework with the same name then turn this into a
                        // framework assembly dependency
                        if (_frameworkReferenceResolver.TryGetAssembly(dependency, targetFramework, out path, out version))
                        {
                            dependencies.Add(new LibraryRange(dependency,
                                LibraryType.ReferenceAssembly,
                                LibraryDependencyType.Build));
                        }
                    }
                }
            }
        }

        private static IEnumerable<string> GetDependencies(string path)
        {
            using (var peReader = new PEReader(File.OpenRead(path)))
            {
                var metadataReader = peReader.GetMetadataReader();

                foreach (var assemblyReferenceHandle in metadataReader.AssemblyReferences)
                {
                    var assemblyReference = metadataReader.GetAssemblyReference(assemblyReferenceHandle);

                    yield return metadataReader.GetString(assemblyReference.Name);
                }
            }
        }

        private void PopulateDependencies(
            List<LibraryRange> dependencies,
            LockFileTargetLibrary targetLibrary,
            NuGetFramework targetFramework)
        {
            foreach (var dependency in targetLibrary.Dependencies)
            {
                dependencies.Add(new LibraryRange(
                    dependency.Id,
                    dependency.VersionRange,
                    LibraryType.Unspecified,
                    LibraryDependencyType.Default));
            }

            if (!targetFramework.IsPackageBased())
            {
                // Only add framework assemblies for non-package based frameworks.
                foreach (var frameworkAssembly in targetLibrary.FrameworkAssemblies)
                {
                    dependencies.Add(new LibraryRange(
                        frameworkAssembly,
                        LibraryType.ReferenceAssembly,
                        LibraryDependencyType.Default));
                }
            }
        }

        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), "_._", StringComparison.Ordinal);
        }

        public static string ResolvePackagesPath(string rootDirectory, GlobalSettings settings)
        {
            // Order
            // 1. global.json { "packages": "..." }
            // 2. EnvironmentNames.PackagesStore environment variable
            // 3. NuGet.config repositoryPath (maybe)?
            // 4. {DefaultLocalRuntimeHomeDir}\packages

            if (!string.IsNullOrEmpty(settings?.PackagesPath))
            {
                return Path.Combine(rootDirectory, settings.PackagesPath);
            }

            var runtimePackages = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesStore);

            if (!string.IsNullOrEmpty(runtimePackages))
            {
                return runtimePackages;
            }

            var profileDirectory = Environment.GetEnvironmentVariable("USERPROFILE");

            if (string.IsNullOrEmpty(profileDirectory))
            {
                profileDirectory = Environment.GetEnvironmentVariable("HOME");
            }

            return Path.Combine(profileDirectory, ".nuget", "packages");
        }
    }
}
