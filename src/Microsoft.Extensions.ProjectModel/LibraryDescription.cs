// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.ProjectModel.Graph;
using NuGet.Frameworks;

namespace Microsoft.Extensions.ProjectModel
{
    /// <summary>
    /// Represents the result of resolving the library
    /// </summary>
    public class LibraryDescription
    {
        public LibraryDescription(
            LibraryRange requestedRange,
            LibraryIdentity identity,
            string path,
            IEnumerable<LibraryRange> dependencies,
            NuGetFramework framework,
            bool resolved,
            bool compatible)
        {
            Path = path;
            RequestedRange = requestedRange;
            Identity = identity;
            Dependencies = dependencies ?? Enumerable.Empty<LibraryRange>();
            Framework = framework;
            Resolved = resolved;
            Compatible = compatible;
        }

        public LibraryRange RequestedRange { get; }
        public LibraryIdentity Identity { get; }
        public LibraryDescription Parent { get; set; }
        public string Path { get; }
        public IEnumerable<LibraryRange> Dependencies { get; }
        public bool Compatible { get; }

        public NuGetFramework Framework { get; set; }
        public bool Resolved { get; set; }

        public override string ToString()
        {
            return $"{Identity} = {Path}";
        }
    }
}
