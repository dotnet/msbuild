// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.DotNet.ApiCompatibility.Abstractions
{
    /// <summary>
    /// Struct containing the assembly's relevant information, used to distinguish different tuple comparisons
    /// and different list of <see cref="CompatDifference"/>.
    /// </summary>
    public readonly struct MetadataInformation : IEquatable<MetadataInformation>
    {
        public readonly string AssemblyName;
        public readonly string TargetFramework;
        public readonly string AssemblyType;

        public MetadataInformation(string name, string targetFramework, string assemblyType)
        {
            AssemblyName = name ?? string.Empty;
            TargetFramework = targetFramework ?? string.Empty;
            AssemblyType = assemblyType ?? string.Empty;
        }

        public bool Equals(MetadataInformation other) =>
            string.Equals(AssemblyName, other.AssemblyName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(TargetFramework, other.TargetFramework, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(AssemblyType, other.AssemblyType, StringComparison.Ordinal);

        public override int GetHashCode() => HashCode.Combine(AssemblyName, TargetFramework, AssemblyType);
    }
}
