// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

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

        public override bool Equals(object obj) =>
            obj is MetadataInformation information && Equals(information);

        public bool Equals(MetadataInformation other) =>
            string.Equals(AssemblyName, other.AssemblyName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(TargetFramework, other.TargetFramework, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(AssemblyId, other.AssemblyId, StringComparison.Ordinal);

        public override int GetHashCode()
        {
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssemblyName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(TargetFramework);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssemblyId);
            return hashCode;
        }
    }
}
