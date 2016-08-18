// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using NuGet.Common;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.ProjectModel;

namespace Microsoft.DotNet.Core.Build.Tasks
{
    public class PublishAssembliesResolver
    {
        private readonly LockFile _lockFile;
        private readonly FallbackPackagePathResolver _packagePathResolver;

        public PublishAssembliesResolver(LockFile lockFile, INuGetPathContext nugetPathContext)
        {
            _lockFile = lockFile;
            _packagePathResolver = new FallbackPackagePathResolver(nugetPathContext);
        }

        public IEnumerable<ResolvedFile> Resolve(NuGetFramework framework, string runtime)
        {
            LockFileTarget lockFileTarget = _lockFile.GetTarget(framework, runtime);

            bool isPortable;
            IEnumerable<LockFileTargetLibrary> runtimeLibraries = lockFileTarget.GetRuntimeLibraries(out isPortable);

            List<ResolvedFile> results = new List<ResolvedFile>();

            foreach (LockFileTargetLibrary targetLibrary in runtimeLibraries)
            {
                string libraryPath = _packagePathResolver.GetPackageDirectory(targetLibrary.Name, targetLibrary.Version);

                results.AddRange(GetResolvedFiles(targetLibrary.RuntimeAssemblies, libraryPath));
                results.AddRange(GetResolvedFiles(targetLibrary.NativeLibraries, libraryPath));

                foreach (LockFileRuntimeTarget runtimeTarget in targetLibrary.RuntimeTargets)
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

                foreach (LockFileItem resourceAssembly in targetLibrary.ResourceAssemblies)
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
            foreach (LockFileItem item in items)
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
