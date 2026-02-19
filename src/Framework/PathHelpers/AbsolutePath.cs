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
    /// and represents an issue in path handling. Two default instances are considered equal.
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
        /// The original string used to create this path.
        /// </summary>
        public string OriginalValue { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct.
        /// </summary>
        /// <param name="path">The absolute path string.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is null, empty, or not a rooted path.</exception>
        public AbsolutePath(string path)
        {
            ValidatePath(path);
            Value = path;
            OriginalValue = path;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct.
        /// </summary>
        /// <param name="path">The absolute path string.</param>
        /// <param name="ignoreRootedCheck">If true, skips checking whether the path is rooted.</param>
        /// <remarks>For internal and testing use, when we want to force bypassing the rooted check.</remarks>
        internal AbsolutePath(string path, bool ignoreRootedCheck)
            : this(path, path, ignoreRootedCheck)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct.
        /// </summary>
        /// <param name="path">The absolute path string.</param>
        /// <param name="original">The original string used to create this path.</param>
        /// <param name="ignoreRootedCheck">If true, skips checking whether the path is rooted.</param>
        internal AbsolutePath(string path, string original, bool ignoreRootedCheck)
        {
            if (!ignoreRootedCheck)
            {
                ValidatePath(path);
            }
            Value = path;
            OriginalValue = original;
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
                throw new ArgumentException(FrameworkResources.GetString("PathMustNotBeNullOrEmpty"), nameof(path));
            }

            // Path.IsPathFullyQualified is not available in .NET Standard 2.0
            // in .NET Framework it's provided by package and in .NET it's built-in
#if NETFRAMEWORK || NET
            if (!Path.IsPathFullyQualified(path))
            {
                throw new ArgumentException(FrameworkResources.GetString("PathMustBeRooted"), nameof(path));
            }
#endif
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AbsolutePath"/> struct by combining an absolute path with a relative path.
        /// </summary>
        /// <param name="path">The path to combine with the base path.</param>
        /// <param name="basePath">The base path to combine with.</param>
        /// <exception cref="ArgumentException">Thrown if <paramref name="path"/> is null or empty.</exception>
        public AbsolutePath(string path, AbsolutePath basePath)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException(FrameworkResources.GetString("PathMustNotBeNullOrEmpty"), nameof(path));
            }

            // This function should not throw when path has illegal characters.
            // For .NET Framework, Microsoft.IO.Path.Combine should be used instead of System.IO.Path.Combine to achieve it.
            // For .NET Core, System.IO.Path.Combine already does not throw in this case.
            Value = Path.Combine(basePath.Value, path);
            OriginalValue = path;
        }

        /// <summary>
        /// Implicitly converts an AbsolutePath to a string.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        public static implicit operator string(AbsolutePath path) => path.Value;

        /// <summary>
        /// Returns the canonical form of this path.
        /// </summary>
        /// <returns>
        /// An <see cref="AbsolutePath"/> representing the canonical form of the path.
        /// </returns>
        /// <remarks>
        /// <para>
        /// The canonical form of a path is exactly what <see cref="Path.GetFullPath(string)"/> would produce,
        /// with the following properties:
        /// <list type="bullet">
        ///   <item>All relative path segments ("." and "..") are resolved.</item>
        ///   <item>Directory separators are normalized to the platform convention (backslash on Windows).</item>
        /// </list>
        /// </para>
        /// <para>
        /// If the path is already in canonical form, returns the current instance to avoid unnecessary allocations.
        /// Preserves the OriginalValue of the current instance.
        /// </para>
        /// </remarks>
        internal AbsolutePath GetCanonicalForm()
        {
            if (string.IsNullOrEmpty(Value))
            {
                return this;
            }


            // Note: this is a quick check to avoid calling Path.GetFullPath when it's not necessary, since it can be expensive. 
            // It should cover the most common cases and avoid the overhead of Path.GetFullPath in those cases.

            // Check for relative path segments "." and ".."
            // In absolute path those segments can not appear in the beginning of the path, only after a path separator.
            // This is not a precise full detection of relative segments. There is no false negatives as this might affect correctenes, but it may have false positives:
            // like when there is a hidden file or directory starting with a dot, or on linux the backslash and dot can be part of the file name.
            // In case of false positives we would call Path.GetFullPath and the result would still be correct.

            bool hasRelativeSegment = Value.Contains("/.") || Value.Contains("\\.");

            // Check if directory separator normalization is required (only on Windows: "/" to "\")
            // On unix "\" is not a valid path separator, but is a part of the file/directory name, so no normalization is needed. 
            bool needsSeparatorNormalization = NativeMethods.IsWindows && Value.IndexOf(Path.AltDirectorySeparatorChar) >= 0;

            if (!hasRelativeSegment && !needsSeparatorNormalization)
            {
                return this;
            }

            // Use Path.GetFullPath to resolve relative segments and normalize separators.
            // Skip validation since Path.GetFullPath already ensures the result is absolute.
            return new AbsolutePath(Path.GetFullPath(Value), OriginalValue, ignoreRootedCheck: true);
        }

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
