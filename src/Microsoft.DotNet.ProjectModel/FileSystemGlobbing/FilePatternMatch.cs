// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.ProjectModel.FileSystemGlobbing
{
    public struct FilePatternMatch : IEquatable<FilePatternMatch>
    {
        public string Path { get; }
        public string Stem { get; }

        public FilePatternMatch(string path, string stem)
        {
            Path = path;
            Stem = stem;
        }

        public bool Equals(FilePatternMatch other)
        {
            return string.Equals(other.Path, Path, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(other.Stem, Stem, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj)
        {
            return Equals((FilePatternMatch)obj);
        }

        public override int GetHashCode()
        {
            var hashCodeCombiner = HashCodeCombiner.Start();
            hashCodeCombiner.Add(Path, StringComparer.OrdinalIgnoreCase);
            hashCodeCombiner.Add(Stem, StringComparer.OrdinalIgnoreCase);

            return hashCodeCombiner.CombinedHash;
        }
    }
}