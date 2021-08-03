// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
#if NETCOREAPP
using System.IO.Enumeration;
#else
using Microsoft.IO.Enumeration;
#endif

namespace Microsoft.Build.FileSystem
{
    public interface IDirectoryCacheFactory
    {
        IDirectoryCache GetDirectoryCacheForProject(string projectPath);
    }

    public interface IDirectoryCache
    {
        bool FileExists(string path);

        bool DirectoryExists(string path);

        /// <summary>
        /// Enumerates files in the given directory only (non-recursively).
        /// </summary>
        /// <typeparam name="TResult">The desired return type.</typeparam>
        /// <param name="path">The directory to enumerate.</param>
        /// <param name="predicate">A predicate to test whether a file should be included.</param>
        /// <param name="transform">A transform from <see cref="FileSystemEntry"/> to <typeparamref name="TResult"/>.</param>
        /// <returns></returns>
        IEnumerable<TResult> EnumerateFiles<TResult>(string path, FileSystemEnumerable<TResult>.FindPredicate predicate, FileSystemEnumerable<TResult>.FindTransform transform);

        /// <summary>
        /// Enumerates subdirectories in the given directory only (non-recursively).
        /// </summary>
        /// <typeparam name="TResult">The desired return type.</typeparam>
        /// <param name="path">The directory to enumerate.</param>
        /// <param name="predicate">A predicate to test whether a directory should be included.</param>
        /// <param name="transform">A transform from <see cref="FileSystemEntry"/> to <typeparamref name="TResult"/>.</param>
        /// <returns></returns>
        IEnumerable<TResult> EnumerateDirectories<TResult>(string path, FileSystemEnumerable<TResult>.FindPredicate predicate, FileSystemEnumerable<TResult>.FindTransform transform);
    }
}
