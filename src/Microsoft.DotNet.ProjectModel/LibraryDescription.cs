// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using NuGet.Frameworks;
using NuGet.LibraryModel;

namespace Microsoft.DotNet.ProjectModel
{
    /// <summary>
    /// Represents the result of resolving the library
    /// </summary>
    public class LibraryDescription
    {
        public LibraryDescription(
            LibraryIdentity identity,
            string hash,
            string path,
            IEnumerable<ProjectLibraryDependency> dependencies,
            NuGetFramework framework,
            bool resolved,
            bool compatible)
        {
            Path = path;
            Identity = identity;
            Hash = hash;
            Dependencies = dependencies ?? Enumerable.Empty<ProjectLibraryDependency>();
            Framework = framework;
            Resolved = resolved;
            Compatible = compatible;
        }

        public LibraryIdentity Identity { get; }
        public string Hash { get; }
        public HashSet<ProjectLibraryDependency> RequestedRanges { get; } = new HashSet<ProjectLibraryDependency>();
        public List<LibraryDescription> Parents { get; } = new List<LibraryDescription>();
        public string Path { get; }
        public IEnumerable<ProjectLibraryDependency> Dependencies { get; }
        public bool Compatible { get; }

        public NuGetFramework Framework { get; set; }
        public bool Resolved { get; set; }

        public override string ToString()
        {
            return $"{Identity} ({Identity.Type}) = {Path}";
        }
    }
}
