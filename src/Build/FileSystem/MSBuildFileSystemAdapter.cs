// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Shared.FileSystem;

namespace Microsoft.Build.FileSystem
{
     public class MSBuildFileSystemAdapter : IFileSystem
    {
        private readonly IMSBuildFileSystem _publicFileSystemInterface;
        public MSBuildFileSystemAdapter(IMSBuildFileSystem publicFileSystemInterface)
        {
            _publicFileSystemInterface = publicFileSystemInterface;
        }
        public TextReader ReadFile(string path) => _publicFileSystemInterface.ReadFile(path);

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share) => _publicFileSystemInterface.GetFileStream(path, mode, access, share);

        public string ReadFileAllText(string path) => _publicFileSystemInterface.ReadFileAllText(path);

        public byte[] ReadFileAllBytes(string path) => _publicFileSystemInterface.ReadFileAllBytes(path);

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _publicFileSystemInterface.EnumerateFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _publicFileSystemInterface.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateFileSystemEntries(
            string path,
            string searchPattern = "*",
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return _publicFileSystemInterface.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        public FileAttributes GetAttributesNoMissingException(string path) => _publicFileSystemInterface.GetAttributesNoMissingException(path);

        public bool DirectoryExists(string path) => _publicFileSystemInterface.DirectoryExists(path);

        public bool FileExists(string path) => _publicFileSystemInterface.FileExists(path);

        public bool DirectoryEntryExists(string path) => _publicFileSystemInterface.DirectoryEntryExists(path);

        public void ClearCaches() => _publicFileSystemInterface.ClearCaches();

        public void WriteStatistics(TextWriter writer)
        {
            _publicFileSystemInterface.WriteStatistics(writer);
        }
    }
}
