// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Frameworks;
using NuGet.ProjectModel;

namespace Microsoft.NETCore.Build.Tasks
{
    public class PublishAssembliesResolver
    {
        private readonly LockFile _lockFile;
        private readonly IPackageResolver _packageResolver;
        private IEnumerable<string> _privateAssetPackageIds;

        public PublishAssembliesResolver(LockFile lockFile, IPackageResolver packageResolver)
        {
            _lockFile = lockFile;
            _packageResolver = packageResolver;
        }

        public PublishAssembliesResolver WithPrivateAssets(IEnumerable<string> privateAssetPackageIds)
        {
            _privateAssetPackageIds = privateAssetPackageIds;
            return this;
        }

        public IEnumerable<ResolvedFile> Resolve(NuGetFramework framework, string runtime)
        {
            ProjectContext projectContext = _lockFile.CreateProjectContext(framework, runtime);

            List<ResolvedFile> results = new List<ResolvedFile>();

            foreach (LockFileTargetLibrary targetLibrary in projectContext.GetRuntimeLibraries(_privateAssetPackageIds))
            {
                string libraryPath = _packageResolver.GetPackageDirectory(targetLibrary.Name, targetLibrary.Version);

                results.AddRange(GetResolvedFiles(targetLibrary.RuntimeAssemblies, libraryPath));
                results.AddRange(GetResolvedFiles(targetLibrary.NativeLibraries, libraryPath));

                foreach (LockFileRuntimeTarget runtimeTarget in targetLibrary.RuntimeTargets.FilterPlaceHolderFiles())
                {
                    if (string.Equals(runtimeTarget.AssetType, "native", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(runtimeTarget.AssetType, "runtime", StringComparison.OrdinalIgnoreCase))
                    {
                        results.Add(
                            new ResolvedFile(
                                sourcePath: Path.Combine(libraryPath, runtimeTarget.Path),
                                destinationSubDirectory: GetRuntimeTargetDestinationSubDirectory(runtimeTarget)));
                    }
                }

                foreach (LockFileItem resourceAssembly in targetLibrary.ResourceAssemblies.FilterPlaceHolderFiles())
                {
                    string locale;
                    if (!resourceAssembly.Properties.TryGetValue("locale", out locale))
                    {
                        locale = null;
                    }

                    results.Add(
                        new ResolvedFile(
                            sourcePath: Path.Combine(libraryPath, resourceAssembly.Path),
                            destinationSubDirectory: locale));
                }
            }

            return results;
        }

        private static IEnumerable<ResolvedFile> GetResolvedFiles(IEnumerable<LockFileItem> items, string libraryPath)
        {
            foreach (LockFileItem item in items.FilterPlaceHolderFiles())
            {
                yield return new ResolvedFile(
                    sourcePath: Path.Combine(libraryPath, item.Path),
                    destinationSubDirectory: null);
            }
        }

        private static string GetRuntimeTargetDestinationSubDirectory(LockFileRuntimeTarget runtimeTarget)
        {
            if (!string.IsNullOrEmpty(runtimeTarget.Runtime))
            {
                return Path.GetDirectoryName(runtimeTarget.Path);
            }

            return null;
        }
    }
}
