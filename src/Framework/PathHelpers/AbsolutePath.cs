// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

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
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new instance of AbsolutePath.
        /// </summary>
        /// <param name="path">The absolute path string.</param>
        /// <param name="ignoreRootedCheck">If true, skips checking whether the path is rooted.</param>
        internal AbsolutePath(string path, bool ignoreRootedCheck)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a new absolute path by combining a absolute path with a relative path.
        /// </summary>
        /// <param name="path">The path to combine with the base path.</param>
        /// <param name="basePath">The base path to combine with.</param>
        public AbsolutePath(string path, AbsolutePath basePath)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Implicitly converts an AbsolutePath to a string.
        /// </summary>
        /// <param name="path">The path to convert.</param>
        public static implicit operator string(AbsolutePath path) => throw new NotImplementedException();

        /// <summary>
        /// Returns the string representation of this path.
        /// </summary>
        /// <returns>The path as a string.</returns>
        public override string ToString() => throw new NotImplementedException();
    }
}
