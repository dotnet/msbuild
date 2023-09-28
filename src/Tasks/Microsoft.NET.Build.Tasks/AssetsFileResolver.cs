// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Packaging.Core;
using NuGet.ProjectModel;

namespace Microsoft.NET.Build.Tasks
{
    internal class AssetsFileResolver
    {
        private readonly IPackageResolver _packageResolver;
        private IEnumerable<string> _excludedPackageIds;
        private bool _preserveStoreLayout;

        public AssetsFileResolver(IPackageResolver packageResolver)
        {
            _packageResolver = packageResolver;
        }

        public AssetsFileResolver WithExcludedPackages(IEnumerable<string> excludedPackageIds)
        {
            _excludedPackageIds = excludedPackageIds;
            return this;
        }
        public AssetsFileResolver WithPreserveStoreLayout(bool preserveStoreLayout)
        {
            _preserveStoreLayout = preserveStoreLayout;
            return this;
        }
        public IEnumerable<ResolvedFile> Resolve(ProjectContext projectContext, bool resolveRuntimeTargets = true)
        {
            List<ResolvedFile> results = new();

            foreach (LockFileTargetLibrary targetLibrary in projectContext.GetRuntimeLibraries(_excludedPackageIds))
            {
                if (!targetLibrary.IsPackage())
                {
                    continue;
                }
                var targetLibraryPackage = new PackageIdentity(targetLibrary.Name, targetLibrary.Version);

                string pkgRoot;
                string libraryPath = _packageResolver.GetPackageDirectory(targetLibrary.Name, targetLibrary.Version, out pkgRoot);

                results.AddRange(GetResolvedFiles(targetLibrary.RuntimeAssemblies, targetLibraryPackage, libraryPath, pkgRoot, AssetType.Runtime));
                results.AddRange(GetResolvedFiles(targetLibrary.NativeLibraries, targetLibraryPackage, libraryPath, pkgRoot, AssetType.Native));

                if (resolveRuntimeTargets)
                {
                    foreach (LockFileRuntimeTarget runtimeTarget in targetLibrary.RuntimeTargets.FilterPlaceholderFiles())
                    {
                        if (string.Equals(runtimeTarget.AssetType, "native", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(runtimeTarget.AssetType, "runtime", StringComparison.OrdinalIgnoreCase))
                        {
                            string sourcePath = Path.Combine(libraryPath, runtimeTarget.Path);
                            AssetType _assetType = AssetType.None;
                            Enum.TryParse<AssetType>(runtimeTarget.AssetType, true, out _assetType);

                            results.Add(
                                new ResolvedFile(
                                    sourcePath: sourcePath,
                                    destinationSubDirectory: GetDestinationSubDirectory(sourcePath,
                                                                                        pkgRoot,
                                                                                        GetRuntimeTargetDestinationSubDirectory(runtimeTarget)),
                                    package: targetLibraryPackage,
                                    assetType: _assetType));
                        }
                    }
                }

                foreach (LockFileItem resourceAssembly in targetLibrary.ResourceAssemblies.FilterPlaceholderFiles())
                {
                    string locale;
                    string sourcePath = Path.Combine(libraryPath, resourceAssembly.Path);
                    if (!resourceAssembly.Properties.TryGetValue("locale", out locale))
                    {
                        locale = null;
                    }

                    results.Add(
                        new ResolvedFile(
                            sourcePath: sourcePath,
                            destinationSubDirectory: GetDestinationSubDirectory(sourcePath, pkgRoot, locale),
                            package: targetLibraryPackage,
                            assetType: AssetType.Resources));
                }
            }

            return results;
        }

        private IEnumerable<ResolvedFile> GetResolvedFiles(IEnumerable<LockFileItem> items, PackageIdentity package, string libraryPath, string pkgRoot, AssetType assetType)
        {
            foreach (LockFileItem item in items.FilterPlaceholderFiles())
            {
                string srcpath = Path.Combine(libraryPath, item.Path);

                yield return new ResolvedFile(
                    sourcePath: srcpath,
                    destinationSubDirectory: GetDestinationSubDirectory(srcpath, pkgRoot),
                    package: package,
                    assetType: assetType);
            }
        }

        private static string GetRuntimeTargetDestinationSubDirectory(LockFileRuntimeTarget runtimeTarget)
        {
            if (!string.IsNullOrEmpty(runtimeTarget.Runtime))
            {
                return Path.GetDirectoryName(runtimeTarget.Path) + Path.DirectorySeparatorChar;
            }

            return null;
        }

        private string GetDestinationSubDirectory(string libraryPath, string pkgRoot, string destPath = null)
        {
            if (_preserveStoreLayout && pkgRoot != null)
            {
                if (!libraryPath.StartsWith(pkgRoot))
                {
                    throw new BuildErrorException(Strings.IncorrectPackageRoot, pkgRoot, libraryPath);
                }

                destPath = Path.GetDirectoryName(libraryPath.Substring(pkgRoot.Length));
            }

            if (destPath != null && destPath[destPath.Length - 1] != Path.DirectorySeparatorChar)
            {
                destPath += Path.DirectorySeparatorChar;
            }

            return destPath;
        }
    }
}
