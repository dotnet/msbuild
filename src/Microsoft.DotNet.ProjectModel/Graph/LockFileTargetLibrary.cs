// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Frameworks;
using NuGet.Packaging.Core;
using NuGet.Versioning;

namespace Microsoft.Extensions.ProjectModel.Graph
{
    public class LockFileTargetLibrary
    {
        public string Name { get; set; }

        public string Type { get; set; }

        public NuGetFramework TargetFramework { get; set; }

        public NuGetVersion Version { get; set; }

        public IList<PackageDependency> Dependencies { get; set; } = new List<PackageDependency>();

        public ISet<string> FrameworkAssemblies { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public IList<LockFileItem> RuntimeAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> CompileTimeAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> ResourceAssemblies { get; set; } = new List<LockFileItem>();

        public IList<LockFileItem> NativeLibraries { get; set; } = new List<LockFileItem>();
    }
}
