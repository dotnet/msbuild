using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.ProjectModel.Graph;
using Microsoft.Extensions.ProjectModel.Utilities;
using NuGet.Packaging;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Resolution
{
    public class PackageDependencyProvider
    {
        private readonly string _packagesPath;

        private readonly IEnumerable<VersionFolderPathResolver> _cacheResolvers;
        private readonly VersionFolderPathResolver _packagePathResolver;

        public PackageDependencyProvider(string packagesPath)
        {
            _packagesPath = packagesPath;
            _cacheResolvers = GetCacheResolvers();
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

            var resolved = compatible;
            var dependencies = new List<LibraryRange>(targetLibrary.Dependencies.Count + targetLibrary.FrameworkAssemblies.Count);
            PopulateDependencies(dependencies, targetLibrary);

            var path = ResolvePackagePath(package);

            var packageDescription = new PackageDescription(
                new LibraryRange(package.Name, new VersionRange(package.Version), LibraryType.Package, LibraryDependencyType.Default),
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

        private string ResolvePackagePath(LockFilePackageLibrary package)
        {
            string expectedHash = package.Sha512;

            foreach (var resolver in _cacheResolvers)
            {
                var cacheHashFile = resolver.GetHashPath(package.Name, package.Version);

                // REVIEW: More efficient compare?
                if (File.Exists(cacheHashFile) &&
                    File.ReadAllText(cacheHashFile) == expectedHash)
                {
                    return resolver.GetInstallPath(package.Name, package.Version);
                }
            }

            return _packagePathResolver.GetInstallPath(package.Name, package.Version);
        }

        public static string ResolveRepositoryPath(string rootDirectory, GlobalSettings settings)
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

        private static IEnumerable<VersionFolderPathResolver> GetCacheResolvers()
        {
            var packageCachePathValue = Environment.GetEnvironmentVariable(EnvironmentNames.PackagesCache);

            if (string.IsNullOrEmpty(packageCachePathValue))
            {
                return Enumerable.Empty<VersionFolderPathResolver>();
            }

            return packageCachePathValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(path => new VersionFolderPathResolver(path));
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public static IEqualityComparer<AssemblyName> OrdinalIgnoreCase = new AssemblyNameComparer();

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                return
                    string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.CultureName ?? "", y.CultureName ?? "", StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(AssemblyName obj)
            {
                var hashCode = 0;
                if (obj.Name != null)
                {
                    hashCode ^= obj.Name.ToUpperInvariant().GetHashCode();
                }

                hashCode ^= (obj.CultureName?.ToUpperInvariant() ?? "").GetHashCode();
                return hashCode;
            }
        }
    }
}
