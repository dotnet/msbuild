// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.ProjectModel.Resolution
{
    public class PackageDependencyProvider
    {
        private readonly FallbackPackagePathResolver _packagePathResolver;
        private readonly VersionFolderPathResolver _versionFolderPathResolver;
        private readonly FrameworkReferenceResolver _frameworkReferenceResolver;

        public PackageDependencyProvider(INuGetPathContext nugetPathContext, FrameworkReferenceResolver frameworkReferenceResolver)
        {
            if (nugetPathContext != null)
            {
                _packagePathResolver = new FallbackPackagePathResolver(nugetPathContext);

                // This resolver is only used for building file names, so that base path is not required.
                _versionFolderPathResolver = new VersionFolderPathResolver(path: null);
            }

            _frameworkReferenceResolver = frameworkReferenceResolver;
        }

        public PackageDescription GetDescription(
            NuGetFramework targetFramework,
            LockFileLibrary package,
            LockFileTargetLibrary targetLibrary)
        {
            // If a NuGet dependency is supposed to provide assemblies but there is no assembly compatible with
            // current target framework, we should mark this dependency as unresolved
            var containsAssembly = package.Files
                .Select(f => f.Replace('/', Path.DirectorySeparatorChar))
                .Any(x => x.StartsWith($"ref{Path.DirectorySeparatorChar}") ||
                    x.StartsWith($"lib{Path.DirectorySeparatorChar}"));

            var compatible = targetLibrary.FrameworkAssemblies.Any() ||
                targetLibrary.CompileTimeAssemblies.Any() ||
                targetLibrary.RuntimeAssemblies.Any() ||
                !containsAssembly;

            var dependencies = 
                new List<ProjectLibraryDependency>(targetLibrary.Dependencies.Count + targetLibrary.FrameworkAssemblies.Count);
            PopulateDependencies(dependencies, targetLibrary, targetFramework);

            var path = _packagePathResolver?.GetPackageDirectory(package.Name, package.Version);
            bool exists = path != null;

            string hashPath = null;
            if (_versionFolderPathResolver != null)
            {
                hashPath = _versionFolderPathResolver.GetHashFileName(package.Name, package.Version);
            }

            if (exists)
            {
                // If the package's compile time assemblies is for a portable profile then, read the assembly metadata
                // and turn System.* references into reference assembly dependencies
                PopulateLegacyPortableDependencies(targetFramework, dependencies, path, targetLibrary);
            }

            var packageDescription = new PackageDescription(
                path,
                hashPath,
                package,
                targetLibrary,
                dependencies,
                compatible,
                resolved: compatible && exists);

            return packageDescription;
        }

        private void PopulateLegacyPortableDependencies(
            NuGetFramework targetFramework,
            List<ProjectLibraryDependency> dependencies,
            string packagePath,
            LockFileTargetLibrary targetLibrary)
        {
            var seen = new HashSet<string>();

            foreach (var assembly in targetLibrary.CompileTimeAssemblies)
            {
                if (IsPlaceholderFile(assembly.Path))
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
                            dependencies.Add(new ProjectLibraryDependency
                            {
                                LibraryRange = new LibraryRange(dependency, LibraryDependencyTarget.Reference)
                            });
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
            List<ProjectLibraryDependency> dependencies,
            LockFileTargetLibrary targetLibrary,
            NuGetFramework targetFramework)
        {
            foreach (var dependency in targetLibrary.Dependencies)
            {
                dependencies.Add(new ProjectLibraryDependency{
                    LibraryRange = new LibraryRange(dependency.Id, dependency.VersionRange, LibraryDependencyTarget.All)
                });
            }

            if (!targetFramework.IsPackageBased)
            {
                // Only add framework assemblies for non-package based frameworks.
                foreach (var frameworkAssembly in targetLibrary.FrameworkAssemblies)
                {
                    dependencies.Add(new ProjectLibraryDependency
                    {
                        LibraryRange = new LibraryRange(frameworkAssembly, LibraryDependencyTarget.Reference)
                    });
                }
            }
        }

        public static bool IsPlaceholderFile(string path)
        {
            return string.Equals(Path.GetFileName(path), "_._", StringComparison.Ordinal);
        }
    }
}
