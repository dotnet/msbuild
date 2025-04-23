// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Shared;
using Microsoft.Build.Shared.FileSystem;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if FEATURE_MSIOREDIST
using Path = Microsoft.IO.Path;
#endif

#nullable disable

namespace Microsoft.Build.FileSystem
{
    internal class DirectoryCacheFileSystemWrapper : IFileSystem
    {
        /// <summary>
        /// The base <see cref="IFileSystem"/> to fall back to for functionality not provided by <see cref="_directoryCache"/>.
        /// </summary>
        private readonly IFileSystem _fileSystem;

        /// <summary>
        /// A host-provided cache used for file existence and directory enumeration.
        /// </summary>
        private readonly IDirectoryCache _directoryCache;

        public DirectoryCacheFileSystemWrapper(IFileSystem fileSystem, IDirectoryCache directoryCache)
        {
            _fileSystem = fileSystem;
            _directoryCache = directoryCache;
        }

        #region IFileSystem implementation based on IDirectoryCache

        public bool FileOrDirectoryExists(string path)
        {
            return _directoryCache.FileExists(path) || _directoryCache.DirectoryExists(path);
        }

        public bool DirectoryExists(string path)
        {
            return _directoryCache.DirectoryExists(path);
        }

        public bool FileExists(string path)
        {
            return _directoryCache.FileExists(path);
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (searchOption != SearchOption.TopDirectoryOnly)
            {
                // Recursive enumeration is not used during evaluation, pass it through.
                return _fileSystem.EnumerateDirectories(path, searchPattern, searchOption);
            }
            return EnumerateFullFileSystemPaths(path, searchPattern, includeFiles: false, includeDirectories: true);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (searchOption != SearchOption.TopDirectoryOnly)
            {
                // Recursive enumeration is not used during evaluation, pass it through.
                return _fileSystem.EnumerateFiles(path, searchPattern, searchOption);
            }
            return EnumerateFullFileSystemPaths(path, searchPattern, includeFiles: true, includeDirectories: false);
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            if (searchOption != SearchOption.TopDirectoryOnly)
            {
                // Recursive enumeration is not used during evaluation, pass it through.
                return _fileSystem.EnumerateFileSystemEntries(path, searchPattern, searchOption);
            }
            return EnumerateFullFileSystemPaths(path, searchPattern, includeFiles: true, includeDirectories: true);
        }

        private IEnumerable<string> EnumerateFullFileSystemPaths(string path, string searchPattern, bool includeFiles, bool includeDirectories)
        {
            FindPredicate predicate = (ref ReadOnlySpan<char> fileName) =>
            {
                return FileMatcher.IsAllFilesWildcard(searchPattern) || FileMatcher.IsMatch(fileName, searchPattern);
            };

#if !FEATURE_MSIOREDIST && NETFRAMEWORK
            FindTransform<string> transform = (ref ReadOnlySpan<char> fileName) => Path.Combine(path, fileName.ToString());
#else
            FindTransform<string> transform = (ref ReadOnlySpan<char> fileName) => Path.Join(path.AsSpan(), fileName);
#endif
            IEnumerable<string> directories = includeDirectories
                ? _directoryCache.EnumerateDirectories(path, searchPattern, predicate, transform)
                : Enumerable.Empty<string>();
            IEnumerable<string> files = includeFiles
                ? _directoryCache.EnumerateFiles(path, searchPattern, predicate, transform)
                : Enumerable.Empty<string>();

            return Enumerable.Concat(directories, files);
        }

        #endregion

        #region IFileSystem pass-through implementation

        public FileAttributes GetAttributes(string path) => _fileSystem.GetAttributes(path);

        public DateTime GetLastWriteTimeUtc(string path) => _fileSystem.GetLastWriteTimeUtc(path);

        public TextReader ReadFile(string path) => _fileSystem.ReadFile(path);

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share) => _fileSystem.GetFileStream(path, mode, access, share);

        public string ReadFileAllText(string path) => _fileSystem.ReadFileAllText(path);

        public byte[] ReadFileAllBytes(string path) => _fileSystem.ReadFileAllBytes(path);

        #endregion
    }
}
