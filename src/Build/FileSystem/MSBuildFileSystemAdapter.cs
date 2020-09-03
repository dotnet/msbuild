// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.FileSystem
{
     internal class MSBuildFileSystemAdapter : IFileSystem
    {
        private readonly MSBuildFileSystemBase _msbuildFileSystem;
        public MSBuildFileSystemAdapter(MSBuildFileSystemBase msbuildFileSystem)
        {
            _msbuildFileSystem = msbuildFileSystem;
        }
        public TextReader ReadFile(string path) => _msbuildFileSystem.ReadFile(path);

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share) => _msbuildFileSystem.GetFileStream(path, mode, access, share);

        public string ReadFileAllText(string path) => _msbuildFileSystem.ReadFileAllText(path);

        public byte[] ReadFileAllBytes(string path) => _msbuildFileSystem.ReadFileAllBytes(path);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _msbuildFileSystem.EnumerateFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _msbuildFileSystem.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateFileSystemEntries(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _msbuildFileSystem.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        public FileAttributes GetAttributes(string path) => _msbuildFileSystem.GetAttributes(path);

        public DateTime GetLastWriteTimeUtc(string path) => _msbuildFileSystem.GetLastWriteTimeUtc(path);

        public bool DirectoryExists(string path) => _msbuildFileSystem.DirectoryExists(path);

        public bool FileExists(string path) => _msbuildFileSystem.FileExists(path);

        public bool DirectoryEntryExists(string path) => _msbuildFileSystem.FileOrDirectoryExists(path);
    }
}
