// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using NuGet.ProjectModel;

namespace Microsoft.NETCore.Build.Tasks
{
    internal class ProjectContext
    {
        public bool IsPortable { get; }

        public IEnumerable<LockFileTargetLibrary> RuntimeLibraries { get; }

        public ProjectContext(bool isPortable, IEnumerable<LockFileTargetLibrary> runtimeLibraries)
        {
            IsPortable = isPortable;
            RuntimeLibraries = runtimeLibraries;
        }
    }
}
