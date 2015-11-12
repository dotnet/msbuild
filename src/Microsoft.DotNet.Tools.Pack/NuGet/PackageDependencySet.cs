// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.Packaging.Core;

namespace NuGet
{
    public class PackageDependencySet
    {
        public PackageDependencySet(IEnumerable<PackageDependency> dependencies)
            : this((NuGetFramework)null, dependencies)
        {
        }

        public PackageDependencySet(string targetFramework, IEnumerable<PackageDependency> dependencies)
            : this(targetFramework != null ? NuGetFramework.Parse(targetFramework) : null, dependencies)
        {
        }

        public PackageDependencySet(NuGetFramework targetFramework, IEnumerable<PackageDependency> dependencies)
        {
            if (dependencies == null)
            {
                throw new ArgumentNullException(nameof(dependencies));
            }

            TargetFramework = targetFramework;
            Dependencies = dependencies.ToArray();
        }

        public NuGetFramework TargetFramework { get; }

        public IReadOnlyList<PackageDependency> Dependencies { get; }
    }
}