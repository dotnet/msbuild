// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.DotNet.PlatformAbstractions;

namespace Microsoft.DotNet.ProjectModel.Files
{
    public class IncludeEntry : IEquatable<IncludeEntry>
    {
        public string TargetPath { get; }

        public string SourcePath { get; }

        public bool IsCustomTarget { get; set; }

        public IncludeEntry(string target, string source)
        {
            TargetPath = target;
            SourcePath = source;
        }

        public override bool Equals(object obj)
        {
            return Equals((IncludeEntry)obj);
        }

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();
            combiner.Add(TargetPath);
            combiner.Add(SourcePath);

            return combiner.CombinedHash;
        }

        public bool Equals(IncludeEntry other)
        {
            return other != null &&
                string.Equals(TargetPath, other.TargetPath, StringComparison.Ordinal) &&
                string.Equals(SourcePath, other.SourcePath, StringComparison.Ordinal) &&
                IsCustomTarget == other.IsCustomTarget;
        }
    }
}
