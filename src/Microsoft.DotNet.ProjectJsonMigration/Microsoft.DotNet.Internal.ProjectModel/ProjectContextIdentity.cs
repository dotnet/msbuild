// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.PlatformAbstractions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.Internal.ProjectModel
{
    public struct ProjectContextIdentity
    {
        public ProjectContextIdentity(string path, NuGetFramework targetFramework)
        {
            Path = path;
            TargetFramework = targetFramework;
        }

        public string Path { get; }
        public NuGetFramework TargetFramework { get; }

        public bool Equals(ProjectContextIdentity other)
        {
            return string.Equals(Path, other.Path) && Equals(TargetFramework, other.TargetFramework);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ProjectContextIdentity && Equals((ProjectContextIdentity) obj);
        }

        public override int GetHashCode()
        {
            var combiner = HashCodeCombiner.Start();
            combiner.Add(Path);
            combiner.Add(TargetFramework);
            return combiner.CombinedHash;
        }
    }
}