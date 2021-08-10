// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

#if BUILD_ENGINE
namespace Microsoft.Build.FileSystem
#else
namespace Microsoft.Build.Shared.FileSystem
#endif
{
    /// <summary>
    /// A predicate taking file name.
    /// </summary>
    /// <param name="fileName">The file name to check.</param>
#if BUILD_ENGINE
    public
#endif
    delegate bool FindPredicate(ref ReadOnlySpan<char> fileName);

    /// <summary>
    /// A function taking file name and returning an arbitrary result.
    /// </summary>
    /// <typeparam name="TResult">The type of the result to return</typeparam>
    /// <param name="fileName">The file name to transform.</param>
#if BUILD_ENGINE
    public
#endif
    delegate TResult FindTransform<TResult>(ref ReadOnlySpan<char> fileName);

    /// <summary>
    /// Allows the implementor to intercept file existence checks and file enumerations performed during project evaluation.
    /// </summary>
#if BUILD_ENGINE
    public
#endif
    interface IDirectoryCache
    {
        /// <summary>
        /// Returns <code>true</code> if the given path points to an existing file on disk.
        /// </summary>
        /// <param name="path">A normalized path.</param>
        bool FileExists(string path);

        /// <summary>
        /// Returns <code>true</code> if the given path points to an existing directory on disk.
        /// </summary>
        /// <param name="path">A normalized path.</param>
        bool DirectoryExists(string path);

        /// <summary>
        /// Enumerates files in the given directory only (non-recursively).
        /// </summary>
        /// <typeparam name="TResult">The desired return type.</typeparam>
        /// <param name="path">The directory to enumerate.</param>
        /// <param name="predicate">A predicate to test whether a file should be included.</param>
        /// <param name="transform">A transform from <code>ReadOnlySpan&lt;char&gt;</code> to <typeparamref name="TResult"/>.</param>
        IEnumerable<TResult> EnumerateFiles<TResult>(string path, FindPredicate predicate, FindTransform<TResult> transform);

        /// <summary>
        /// Enumerates subdirectories in the given directory only (non-recursively).
        /// </summary>
        /// <typeparam name="TResult">The desired return type.</typeparam>
        /// <param name="path">The directory to enumerate.</param>
        /// <param name="predicate">A predicate to test whether a directory should be included.</param>
        /// <param name="transform">A transform from <code>ReadOnlySpan&lt;char&gt;</code> to <typeparamref name="TResult"/>.</param>
        IEnumerable<TResult> EnumerateDirectories<TResult>(string path, FindPredicate predicate, FindTransform<TResult> transform);
    }
}
