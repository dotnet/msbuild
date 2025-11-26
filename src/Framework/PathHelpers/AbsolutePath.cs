// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NETFRAMEWORK
using Microsoft.IO;
#else
using System.IO;
#endif

namespace Microsoft.Build.Framework
{
    /// <summary>
    /// Represents an absolute file system path.
    /// </summary>
    /// <remarks>
    /// This struct wraps a string representing an absolute file system path.
    /// Path equality comparisons are case-sensitive or case-insensitive depending on the operating system's
    /// file system conventions (case-sensitive on Linux, case-insensitive on Windows and macOS).
    /// Does not perform any normalization beyond validating the path is fully qualified.
    /// A default instance (created via <c>default(AbsolutePath)</c>) has a null Value 
    /// and should not be used. Two default instances are considered equal.
    /// </remarks>
    public readonly struct AbsolutePath : IEquatable<AbsolutePath>
    {
        /// <summary>
        /// The string comparer to use for path comparisons, based on OS file system case sensitivity.
        /// </summary>
        private static readonly StringComparer s_pathComparer = NativeMethods.IsFileSystemCaseSensitive ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

        /// <summary>
        /// The normalized string representation of this path.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct.
        /// </summary>
        /// <param name="path">The absolute path string.</param>
        public AbsolutePath(string path)
        {
            ValidatePath(path);
            Value = path;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct.
        /// </summary>
        /// <param name="path">The absolute path string.</param>
        /// <param name="ignoreRootedCheck">If true, skips checking whether the path is rooted.</param>
        /// <remarks>For internal and testing use, when we want to force bypassing the rooted check.</remarks>
        internal AbsolutePath(string path, bool ignoreRootedCheck)
        {
            if (!ignoreRootedCheck) 
            {
                ValidatePath(path);
            }
            Value = path;
        }

        /// <summary>
        /// Validates that the specified file system path is non-empty and rooted.
        /// </summary>
        /// <param name="path">The file system path to validate. Must not be null, empty, or a relative path.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is null, empty, or not a rooted path.</exception>
        private static void ValidatePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must not be null or empty.", nameof(path));
            }

            // Path.IsPathFullyQualified is not available in .NET Standard 2.0
            // in .NET Framework it's provided by package and in .NET it's built-in
#if NETFRAMEWORK || NET
            if (!Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException("Path must be rooted.", nameof(path));
            }
#endif
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct by combining an absolute path with a relative path.
        /// </summary>
        /// <param name="path">The path to combine with the base path.</param>
        /// <param name="basePath">The base path to combine with.</param>
        public AbsolutePath(string path, AbsolutePath basePath) => Value = Path.Combine(basePath.Value, path);

        /// <summary>
        /// Implicitly converts an AbsolutePath to a string.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        public static implicit operator string(AbsolutePath path) => path.Value;

        /// <summary>
        /// Determines whether two <see cref="AbsolutePath"/> instances are equal.
        /// </summary>
        /// <param name="left">The first path to compare.</param>
        /// <param name="right">The second path to compare.</param>
        /// <returns><c>true</c> if the paths are equal; otherwise, <c>false</c>.</returns>
        public static bool operator ==(AbsolutePath left, AbsolutePath right) => left.Equals(right);

        /// <summary>
        /// Determines whether two <see cref="AbsolutePath"/> instances are not equal.
        /// </summary>
        /// <param name="left">The first path to compare.</param>
        /// <param name="right">The second path to compare.</param>
        /// <returns><c>true</c> if the paths are not equal; otherwise, <c>false</c>.</returns>
        public static bool operator !=(AbsolutePath left, AbsolutePath right) => !left.Equals(right);

        /// <summary>
        /// Determines whether the specified object is equal to the current <see cref="AbsolutePath"/>.
        /// </summary>
        /// <param name="obj">The object to compare with the current instance.</param>
        /// <returns><c>true</c> if the specified object is an <see cref="AbsolutePath"/> and is equal to the current instance; otherwise, <c>false</c>.</returns>
        public override bool Equals(object? obj) => obj is AbsolutePath other && Equals(other);

        /// <summary>
        /// Determines whether the specified <see cref="AbsolutePath"/> is equal to the current instance.
        /// </summary>
        /// <param name="other">The <see cref="AbsolutePath"/> to compare with the current instance.</param>
        /// <returns><c>true</c> if the paths are equal according to the operating system's file system case sensitivity rules; otherwise, <c>false</c>.</returns>
        public bool Equals(AbsolutePath other) => s_pathComparer.Equals(Value, other.Value);

        /// <summary>
        /// Returns a hash code for this <see cref="AbsolutePath"/>.
        /// </summary>
        /// <returns>A hash code that is consistent with the equality comparison.</returns>
        public override int GetHashCode() => Value is null ? 0 : s_pathComparer.GetHashCode(Value);

        /// <summary>
        /// Returns the string representation of this path.
        /// </summary>
        /// <returns>The path as a string.</returns>
        public override string ToString() => Value;
    }
}
