// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// Struct containing the assembly's relevant information, used to distinguish different tuple comparisons
    /// and different list of <see cref="CompatDifference"/>.
    /// </summary>
    public readonly struct MetadataInformation : IEquatable<MetadataInformation>
    {
        private const string DEFAULT_LEFT_NAME = "left";
        private const string DEFAULT_RIGHT_NAME = "right";

        /// <summary>
        /// A default metadata information for left hand side comparison elements.
        /// </summary>
        public static readonly MetadataInformation DefaultLeft = new(DEFAULT_LEFT_NAME, DEFAULT_LEFT_NAME);

        /// <summary>
        /// A default metadata information for right hand side comparison elements.
        /// </summary>
        public static readonly MetadataInformation DefaultRight = new(DEFAULT_RIGHT_NAME, DEFAULT_RIGHT_NAME);

        /// <summary>
        /// The name of the assembly.
        /// </summary>
        public readonly string AssemblyName;

        /// <summary>
        /// The unique assembly id.
        /// </summary>
        public readonly string AssemblyId;

        /// <summary>
        /// Returns the assembly's full path or if it's part of an archive, the path to the archive.
        /// </summary>
        public readonly string FullPath;

        /// <summary>
        /// The assembly references.
        /// </summary>
        public readonly IEnumerable<string>? References;

        /// <summary>
        /// The assembly's display string.
        /// </summary>
        public readonly string DisplayString;

        public MetadataInformation(string assemblyName, string assemblyId, string? fullPath = null, IEnumerable<string>? references = null, string? displayString = null)
        {
            AssemblyName = assemblyName;
            AssemblyId = assemblyId;
            FullPath = fullPath ?? assemblyId;
            References = references;
            DisplayString = displayString ?? assemblyId;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) =>
            obj is MetadataInformation information && Equals(information);

        /// <inheritdoc />
        public bool Equals(MetadataInformation other) =>
            string.Equals(AssemblyName, other.AssemblyName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(AssemblyId, other.AssemblyId, StringComparison.Ordinal) &&
            string.Equals(FullPath, other.FullPath, StringComparison.OrdinalIgnoreCase);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            int hashCode = 1447485498;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssemblyName);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(AssemblyId);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(FullPath);
            return hashCode;
        }

        /// <inheritdoc />
        public override string ToString() => DisplayString;

        /// <inheritdoc />
        public static bool operator ==(MetadataInformation left, MetadataInformation right) => left.Equals(right);

        /// <inheritdoc />
        public static bool operator !=(MetadataInformation left, MetadataInformation right) => !(left == right);
    }
}
