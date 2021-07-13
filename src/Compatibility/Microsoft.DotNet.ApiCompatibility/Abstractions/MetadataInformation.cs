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
        public readonly string AssemblyId;
        public readonly string DisplayString;

        public MetadataInformation(string name, string targetFramework, string assemblyId, string displayString = null)
        {
            AssemblyName = name ?? string.Empty;
            TargetFramework = targetFramework ?? string.Empty;
            AssemblyId = assemblyId ?? string.Empty;
            DisplayString = displayString ?? assemblyId;
        }

        public bool Equals(MetadataInformation other) =>
            string.Equals(AssemblyName, other.AssemblyName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(TargetFramework, other.TargetFramework, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(AssemblyId, other.AssemblyId, StringComparison.Ordinal);

        public override int GetHashCode() => HashCode.Combine(AssemblyName, TargetFramework, AssemblyId);
    }
}
