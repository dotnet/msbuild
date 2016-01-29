// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Internal;

namespace Microsoft.DotNet.ProjectModel.Compilation
{
    public struct LibraryAsset
    {
        public string Name { get; }
        public string RelativePath { get; }
        public string ResolvedPath { get; }

        public LibraryAsset(string name, string relativePath, string resolvedPath)
        {
            Name = name;
            RelativePath = relativePath;
            ResolvedPath = resolvedPath;
        }

        public bool Equals(LibraryAsset other)
        {
            return string.Equals(Name, other.Name)
                && string.Equals(RelativePath, other.RelativePath)
                && string.Equals(ResolvedPath, other.ResolvedPath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is LibraryAsset && Equals((LibraryAsset) obj);
        }

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();
            combiner.Add(Name);
            combiner.Add(RelativePath);
            combiner.Add(ResolvedPath);
            return combiner.CombinedHash;
        }
    }
}
