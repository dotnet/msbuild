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
        private readonly LockFileTarget _lockFileTarget;

        public bool IsPortable { get; }

        public ProjectContext(LockFileTarget lockFileTarget)
        {
            _lockFileTarget = lockFileTarget;

            IsPortable = _lockFileTarget.IsPortable();
        }

        public IEnumerable<LockFileTargetLibrary> GetRuntimeLibraries()
        {
            IEnumerable<LockFileTargetLibrary> runtimeLibraries = _lockFileTarget.Libraries;
            Dictionary<string, LockFileTargetLibrary> libraryLookup =
                runtimeLibraries.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);

            HashSet<string> allExclusionList = new HashSet<string>();

            if (IsPortable)
            {
                allExclusionList.UnionWith(_lockFileTarget.GetPlatformExclusionList(libraryLookup));
            }

            // TODO: exclude "type: build" dependencies during publish - https://github.com/dotnet/sdk/issues/42

            return runtimeLibraries.Filter(allExclusionList).ToArray();
        }

        public IEnumerable<LockFileTargetLibrary> GetCompileLibraries()
        {
            // TODO: exclude "type: build" dependencies during publish - https://github.com/dotnet/sdk/issues/42

            return _lockFileTarget.Libraries;
        }
    }
}
