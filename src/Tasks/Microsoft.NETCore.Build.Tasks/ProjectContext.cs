// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.ProjectModel;

namespace Microsoft.NETCore.Build.Tasks
{
    internal class ProjectContext
    {
        private readonly LockFile _lockFile;
        private readonly LockFileTarget _lockFileTarget;

        public bool IsPortable { get; }

        public LockFile LockFile => _lockFile;
        public LockFileTarget LockFileTarget => _lockFileTarget;

        public ProjectContext(LockFile lockFile, LockFileTarget lockFileTarget)
        {
            _lockFile = lockFile;
            _lockFileTarget = lockFileTarget;

            IsPortable = _lockFileTarget.IsPortable();
        }

        public IEnumerable<LockFileTargetLibrary> GetRuntimeLibraries(IEnumerable<string> privateAssetPackageIds)
        {
            IEnumerable<LockFileTargetLibrary> runtimeLibraries = _lockFileTarget.Libraries;
            Dictionary<string, LockFileTargetLibrary> libraryLookup =
                runtimeLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> allExclusionList = new HashSet<string>();

            if (IsPortable)
            {
                allExclusionList.UnionWith(_lockFileTarget.GetPlatformExclusionList(libraryLookup));
            }

            if (privateAssetPackageIds?.Any() == true)
            {
                HashSet<string> privateAssetsExclusionList =
                    LockFileExtensions.GetPrivateAssetsExclusionList(
                        _lockFile,
                        _lockFileTarget,
                        privateAssetPackageIds,
                        libraryLookup);

                allExclusionList.UnionWith(privateAssetsExclusionList);
            }

            return runtimeLibraries.Filter(allExclusionList).ToArray();
        }

        public IEnumerable<LockFileTargetLibrary> GetCompileLibraries(IEnumerable<string> compilePrivateAssetPackageIds)
        {
            IEnumerable<LockFileTargetLibrary> compileLibraries = _lockFileTarget.Libraries;

            if (compilePrivateAssetPackageIds?.Any() == true)
            {
                Dictionary<string, LockFileTargetLibrary> libraryLookup =
                    compileLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

                HashSet<string> privateAssetsExclusionList =
                    LockFileExtensions.GetPrivateAssetsExclusionList(
                        _lockFile,
                        _lockFileTarget,
                        compilePrivateAssetPackageIds,
                        libraryLookup);

                compileLibraries = compileLibraries.Filter(privateAssetsExclusionList);
            }

            return compileLibraries.ToArray();
        }

        public IEnumerable<string> GetTopLevelDependencies()
        {
            return LockFileExtensions.GetTopLevelDependencies(LockFile, LockFileTarget);
        }
    }
}
