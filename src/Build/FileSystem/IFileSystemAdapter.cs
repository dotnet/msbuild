// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.FileSystem
{
    internal class IFileSystemAdapter : MSBuildFileSystemBase
    {
        private readonly IFileSystem _wrappedFileSystem;

        public IFileSystemAdapter(IFileSystem wrappedFileSystem)
        {
            _wrappedFileSystem = wrappedFileSystem;
        }

        public override TextReader ReadFile(string path)
        {
            return _wrappedFileSystem.ReadFile(path);
        }

        public override Stream GetFileStream(
            string path,
            FileMode mode,
            FileAccess access,
            FileShare share)
        {
            return _wrappedFileSystem.GetFileStream(
                path,
                mode,
                access,
                share);
        }

        public override string ReadFileAllText(string path)
        {
            return _wrappedFileSystem.ReadFileAllText(path);
        }

        public override byte[] ReadFileAllBytes(string path)
        {
            return _wrappedFileSystem.ReadFileAllBytes(path);
        }

        public override IEnumerable<string> EnumerateFiles(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _wrappedFileSystem.EnumerateFiles(path, searchPattern, searchOption);
        }

        public override IEnumerable<string> EnumerateDirectories(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _wrappedFileSystem.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public override IEnumerable<string> EnumerateFileSystemEntries(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _wrappedFileSystem.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        public override FileAttributes GetAttributes(string path)
        {
            return _wrappedFileSystem.GetAttributes(path);
        }

        public override DateTime GetLastWriteTimeUtc(string path)
        {
            return _wrappedFileSystem.GetLastWriteTimeUtc(path);
        }

        public override bool DirectoryExists(string path)
        {
            return _wrappedFileSystem.DirectoryExists(path);
        }

        public override bool FileExists(string path)
        {
            return _wrappedFileSystem.FileExists(path);
        }

        public override bool FileOrDirectoryExists(string path)
        {
            return _wrappedFileSystem.DirectoryEntryExists(path);
        }
    }
}
