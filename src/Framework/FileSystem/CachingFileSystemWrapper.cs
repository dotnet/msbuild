// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;


namespace Microsoft.Build.Shared.FileSystem
{
    internal sealed class CachingFileSystemWrapper : IFileSystem, IDirectFileSystemEnumeration
    {
        private readonly IFileSystem _fileSystem;
        private readonly ConcurrentDictionary<string, bool> _directoryExistenceCache = new();
        private readonly ConcurrentDictionary<string, bool> _fileExistenceCache = new();
        private readonly ConcurrentDictionary<string, bool> _fileOrDirectoryExistenceCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastWriteTimeCache = new();
        private readonly Func<string, bool> _directoryExists;
        private readonly Func<string, bool> _fileExists;
        private readonly Func<string, bool> _fileOrDirectoryExists;
        private readonly Func<string, DateTime> _getLastWriteTimeUtc;

        public CachingFileSystemWrapper(IFileSystem fileSystem)
        {
            _fileSystem = fileSystem;
            _directoryExists = fileSystem.DirectoryExists;
            _fileExists = fileSystem.FileExists;
            _fileOrDirectoryExists = fileSystem.FileOrDirectoryExists;
            _getLastWriteTimeUtc = fileSystem.GetLastWriteTimeUtc;
        }

        public bool SupportsDirectEnumeration =>
            _fileSystem is IDirectFileSystemEnumeration { SupportsDirectEnumeration: true };

        public bool FileOrDirectoryExists(string path)
        {
            // A positive result from either specific cache implies existence, so we can avoid a redundant filesystem stat.
            if ((_fileExistenceCache.TryGetValue(path, out bool fileExists) && fileExists) ||
                (_directoryExistenceCache.TryGetValue(path, out bool directoryExists) && directoryExists))
            {
                return true;
            }

            return _fileOrDirectoryExistenceCache.GetOrAdd(path, _fileOrDirectoryExists);
        }

        public FileAttributes GetAttributes(string path)
        {
            return _fileSystem.GetAttributes(path);
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            return _lastWriteTimeCache.GetOrAdd(path, _getLastWriteTimeUtc);
        }

        public bool DirectoryExists(string path)
        {
            return _directoryExistenceCache.GetOrAdd(path, _directoryExists);
        }

        public bool FileExists(string path)
        {
            return _fileExistenceCache.GetOrAdd(path, _fileExists);
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
