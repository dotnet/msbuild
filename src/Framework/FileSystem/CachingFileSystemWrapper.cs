// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using Microsoft.Build.Framework;


namespace Microsoft.Build.Shared.FileSystem
{
    internal sealed class CachingFileSystemWrapper : IFileSystem, IDirectFileSystemEnumeration
    {
        private readonly IFileSystem _fileSystem;
        private readonly ConcurrentDictionary<string, bool> _directoryExistenceCache = new();
        private readonly ConcurrentDictionary<string, bool> _fileExistenceCache = new();
        private readonly ConcurrentDictionary<string, bool> _fileOrDirectoryExistenceCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _lastWriteTimeCache = new();
        private readonly Func<string, bool> _readDirectoryExists;
        private readonly Func<string, bool> _readFileExists;
        private readonly Func<string, bool> _readFileOrDirectoryExists;
        private readonly Func<string, DateTime> _readLastWriteTime;
        private ConcurrentDictionary<(string Directory, string File), string>? _upwardSearchCache;

        public CachingFileSystemWrapper(IFileSystem fileSystem) : this(fileSystem, null)
        {
        }

        internal CachingFileSystemWrapper(IFileSystem fileSystem, DirectoryListingCache? directoryListingCache)
        {
            _fileSystem = fileSystem;
            DirectoryListingCache = directoryListingCache is not null
                && ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_13)
                && FileSystems.SupportsDirectoryCache(fileSystem)
                    ? directoryListingCache.ForEvaluation() : null;
            _readDirectoryExists = ReadDirectoryExists;
            _readFileExists = ReadFileExists;
            _readFileOrDirectoryExists = ReadFileOrDirectoryExists;
            _readLastWriteTime = fileSystem.GetLastWriteTimeUtc;
        }

        internal DirectoryListingCache? DirectoryListingCache { get; }

        public bool SupportsDirectEnumeration =>
            _fileSystem is IDirectFileSystemEnumeration { SupportsDirectEnumeration: true };

        internal IFileSystem UnderlyingFileSystem => _fileSystem;

        public bool FileOrDirectoryExists(string path)
        {
            // A positive result from either specific cache implies existence, so we can avoid a redundant filesystem stat.
            if ((_fileExistenceCache.TryGetValue(path, out bool fileExists) && fileExists) ||
                (_directoryExistenceCache.TryGetValue(path, out bool directoryExists) && directoryExists))
            {
                return true;
            }

            return _fileOrDirectoryExistenceCache.GetOrAdd(path, _readFileOrDirectoryExists);
        }

        public FileAttributes GetAttributes(string path)
        {
            return _fileSystem.GetAttributes(path);
        }

        public DateTime GetLastWriteTimeUtc(string path)
        {
            return _lastWriteTimeCache.GetOrAdd(path, _readLastWriteTime);
        }

        public bool DirectoryExists(string path)
        {
            return _directoryExistenceCache.GetOrAdd(path, _readDirectoryExists);
        }

        public bool FileExists(string path)
        {
            return _fileExistenceCache.GetOrAdd(path, _readFileExists);
        }

        private bool ReadDirectoryExists(string path)
        {
            if (DirectoryListingCache?.ValidateMissingPath(path) == DirectorySnapshotStatus.Valid) { return false; }
            bool exists = _fileSystem.DirectoryExists(path);
            if (!exists) { DirectoryListingCache?.TryRecordMissingPath(path, _fileSystem); }
            return exists;
        }

        private bool ReadFileExists(string path)
        {
            if (DirectoryListingCache?.ValidateMissingPath(path) == DirectorySnapshotStatus.Valid) { return false; }
            bool exists = _fileSystem.FileExists(path);
            if (!exists) { DirectoryListingCache?.TryRecordMissingPath(path, _fileSystem); }
            return exists;
        }

        private bool ReadFileOrDirectoryExists(string path)
        {
            if (DirectoryListingCache?.ValidateMissingPath(path) == DirectorySnapshotStatus.Valid) { return false; }
            bool exists = _fileSystem.FileOrDirectoryExists(path);
            if (!exists) { DirectoryListingCache?.TryRecordMissingPath(path, _fileSystem); }
            return exists;
        }

        internal bool TryGetUpwardSearch(string directory, string file, [NotNullWhen(true)] out string? result)
        {
            result = null;
            return _upwardSearchCache is not null && _upwardSearchCache.TryGetValue((directory, file), out result);
        }

        internal string CacheUpwardSearch(string directory, string file, string result)
            => LazyInitializer.EnsureInitialized(ref _upwardSearchCache)!.GetOrAdd((directory, file), result);

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
