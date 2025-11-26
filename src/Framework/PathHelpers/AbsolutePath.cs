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
    /// This struct ensures that paths are always in absolute form and properly formatted.
    /// </remarks>
    public readonly struct AbsolutePath
    {
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
        /// Returns the string representation of this path.
        /// </summary>
        /// <returns>The path as a string.</returns>
        public override string ToString() => Value;
    }
}
