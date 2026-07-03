// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;


namespace Microsoft.Build.Shared.FileSystem
{
    internal sealed class CachingFileSystemWrapper : IFileSystem
    {
        private readonly IFileSystem _fileSystem;
        private readonly ConcurrentDictionary<string, bool> _directoryExistenceCache = new();
        private readonly ConcurrentDictionary<string, bool> _fileExistenceCache = new();
        private readonly ConcurrentDictionary<string, bool> _fileOrDirectoryExistenceCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastWriteTimeCache = new();

        public CachingFileSystemWrapper(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
        }

        public bool FileOrDirectoryExists(string path)
        {
            // A positive result from either specific cache implies existence, so we can avoid a redundant filesystem stat.
            if ((_fileExistenceCache.TryGetValue(path, out bool fileExists) && fileExists) ||
                (_directoryExistenceCache.TryGetValue(path, out bool directoryExists) && directoryExists))
            {
                return true;
            }

            return _fileOrDirectoryExistenceCache.GetOrAdd(path, p => _fileSystem.FileOrDirectoryExists(p));
        }

        public FileAttributes GetAttributes(string path)
        {
            return _fileSystem.GetAttributes(path);
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            return _lastWriteTimeCache.GetOrAdd(path, p => _fileSystem.GetLastWriteTimeUtc(p));
        }

        public bool DirectoryExists(string path)
        {
            return _directoryExistenceCache.GetOrAdd(path, p => _fileSystem.DirectoryExists(p));
        }

        public bool FileExists(string path)
        {
            return _fileExistenceCache.GetOrAdd(path, p => _fileSystem.FileExists(p));
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _fileSystem.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public TextReader ReadFile(string path)
        {
            return _fileSystem.ReadFile(path);
        }

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return _fileSystem.GetFileStream(path, mode, access, share);
        }

        public string ReadFileAllText(string path)
        {
            return _fileSystem.ReadFileAllText(path);
        }

        public byte[] ReadFileAllBytes(string path)
        {
            return _fileSystem.ReadFileAllBytes(path);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _fileSystem.EnumerateFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _fileSystem.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }
    }
}
