// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Shared.FileSystem
{
    internal class CachingFileSystemWrapper : IFileSystem
    {
        private readonly IFileSystem _fileSystem;
        private readonly ConcurrentDictionary<string, bool> _existenceCache = new ConcurrentDictionary<string, bool>();

        public CachingFileSystemWrapper(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public bool DirectoryEntryExists(string path)
        {
            return CachedExistenceCheck(path, p => _fileSystem.DirectoryEntryExists(p));
        }

        public bool DirectoryExists(string path)
        {
            return CachedExistenceCheck(path, p => _fileSystem.DirectoryExists(p));
        }

        public bool FileExists(string path)
        {
            return CachedExistenceCheck(path, p => _fileSystem.FileExists(p));
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _fileSystem.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _fileSystem.EnumerateFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _fileSystem.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        private bool CachedExistenceCheck(string path, Func<string, bool> existenceCheck)
        {
            return _existenceCache.GetOrAdd(path, existenceCheck);
        }
    }
}
