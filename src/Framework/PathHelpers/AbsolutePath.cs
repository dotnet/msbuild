// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NETFRAMEWORK
using IOPath = Microsoft.IO.Path;
#else
using IOPath = System.IO.Path;
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
        /// Gets the string representation of this path.
        /// </summary>
        public string Path { get; }

        /// <summary>
        /// Creates a new instance of AbsolutePath.
        /// </summary>
        /// <param name="path">The absolute path string.</param>
        public AbsolutePath(string path)
        {
            ValidatePath(path);
            Path = path;
        }

        /// <summary>
        /// Creates a new instance of AbsolutePath.
        /// </summary>
        /// <param name="path">The absolute path string.</param>
        /// <param name="ignoreRootedCheck">If true, skips checking whether the path is rooted.</param>
        internal AbsolutePath(string path, bool ignoreRootedCheck)
        {
            if (!ignoreRootedCheck) 
            {
                ValidatePath(path);
            }
            Path = path;
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
            if (!IOPath.IsPathFullyQualified(path))
            {
                throw new ArgumentException("Path must be rooted.", nameof(path));
            }
#endif
        }

        /// <summary>
        /// Creates a new absolute path by combining a absolute path with a relative path.
        /// </summary>
        /// <param name="path">The path to combine with the base path.</param>
        /// <param name="basePath">The base path to combine with.</param>
        public AbsolutePath(string path, AbsolutePath basePath)
        {
            Path = System.IO.Path.Combine(basePath.Path, path);
        }

        /// <summary>
        /// Implicitly converts an AbsolutePath to a string.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        public static implicit operator string(AbsolutePath path) => path.Path;

        /// <summary>
        /// Returns the string representation of this path.
        /// </summary>
        /// <returns>The path as a string.</returns>
        public override string ToString() => Path;
    }
}
