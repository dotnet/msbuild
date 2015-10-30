// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;
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
            LibraryIdentity identity,
            string hash,
            string path,
            IEnumerable<LibraryRange> dependencies,
            NuGetFramework framework,
            bool resolved,
            bool compatible)
        {
            Path = path;
            Identity = identity;
            Hash = hash;
            Dependencies = dependencies ?? Enumerable.Empty<LibraryRange>();
            Framework = framework;
            Resolved = resolved;
            Compatible = compatible;
        }

        public LibraryIdentity Identity { get; }
        public string Hash { get; }
        public HashSet<LibraryRange> RequestedRanges { get; } = new HashSet<LibraryRange>(new LibraryRangeEqualityComparer());
        public List<LibraryDescription> Parents { get; } = new List<LibraryDescription>();
        public string Path { get; }
        public IEnumerable<LibraryRange> Dependencies { get; }
        public bool Compatible { get; }

        public NuGetFramework Framework { get; set; }
        public bool Resolved { get; set; }

        public override string ToString()
        {
            return $"{Identity} = {Path}";
        }

        // For diagnostics, we don't want to duplicate requested dependencies so we 
        // dedupe dependencies defined in project.json
        private class LibraryRangeEqualityComparer : IEqualityComparer<LibraryRange>
        {
            public bool Equals(LibraryRange x, LibraryRange y)
            {
                return x.Equals(y) &&
                    x.SourceColumn == y.SourceColumn &&
                    x.SourceLine == y.SourceLine &&
                    string.Equals(x.SourceFilePath, y.SourceFilePath, StringComparison.Ordinal);
            }

            public int GetHashCode(LibraryRange obj)
            {
                var combiner = HashCodeCombiner.Start();
                combiner.Add(obj);
                combiner.Add(obj.SourceFilePath);
                combiner.Add(obj.SourceLine);
                combiner.Add(obj.SourceColumn);

                return combiner.CombinedHash;
            }
        }
    }
}
