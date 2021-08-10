// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if BUILD_ENGINE
using Microsoft.Build.FileSystem;
#endif
using System;
using System.Collections.Generic;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Implements <see cref="IDirectoryCache"/> on top of <see cref="IFileSystem"/>.
    /// </summary>
    internal sealed class DirectoryCacheOverFileSystem : IDirectoryCache
    {
        private IFileSystem _fileSystem;

        public DirectoryCacheOverFileSystem(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public bool FileExists(string path)
        {
            return _fileSystem.FileExists(path);
        }

        public bool DirectoryExists(string path)
        {
            return _fileSystem.DirectoryExists(path);
        }

        public IEnumerable<TResult> EnumerateDirectories<TResult>(string path, FindPredicate predicate, FindTransform<TResult> transform)
        {
            return EnumerateAndTransformFullPaths(_fileSystem.EnumerateDirectories(path), predicate, transform);
        }

        public IEnumerable<TResult> EnumerateFiles<TResult>(string path, FindPredicate predicate, FindTransform<TResult> transform)
        {
            return EnumerateAndTransformFullPaths(_fileSystem.EnumerateFiles(path), predicate, transform);
        }

        private IEnumerable<TResult> EnumerateAndTransformFullPaths<TResult>(IEnumerable<string> fullPaths, FindPredicate predicate, FindTransform<TResult> transform)
        {
            foreach (string fullPath in fullPaths)
            {
                // TODO: Call Path.GetFileName() from Microsoft.IO.
                int lastSlashPos = fullPath.LastIndexOfAny(FileUtilities.Slashes);
                ReadOnlySpan<char> fileName = fullPath.AsSpan(lastSlashPos + 1, fullPath.Length - lastSlashPos - 1);

                if (predicate(ref fileName))
                {
                    yield return transform(ref fileName);
                }
            }
        }
    }
}
