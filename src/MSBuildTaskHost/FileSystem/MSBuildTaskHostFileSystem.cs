// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Legacy implementation for MSBuildTaskHost which is stuck on net20 APIs
    /// </summary>
    internal class MSBuildTaskHostFileSystem : IFileSystem
    {
        private static readonly MSBuildTaskHostFileSystem Instance = new MSBuildTaskHostFileSystem();

        public static MSBuildTaskHostFileSystem Singleton() => Instance;

        public bool DirectoryEntryExists(string path)
        {
            return NativeMethodsShared.FileOrDirectoryExists(path);
        }

        public void ClearCaches() { }
        public void WriteStatistics(TextWriter writer) { }

        public FileAttributes GetAttributesNoMissingException(string path)
        {
            try
            {
                return File.GetAttributes(path);
            }
            catch
            {
                // ignored
            }

            // Convert any exceptions into a -1 attribute value result.
            // If we got an ArgumentException on invalid path this will match
            // the semantics of File.Exists() or Directory.Exists().
            return (FileAttributes)(-1);
        }

        public bool DirectoryExists(string path)
        {
            return NativeMethodsShared.DirectoryExists(path);
        }

        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return Directory.GetDirectories(path, searchPattern, searchOption);
        }

        public TextReader ReadFile(string path)
        {
            return new StreamReader(path);
        }

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return new FileStream(path, mode, access, share);
        }

        public string ReadFileAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public byte[] ReadFileAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        public IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            return Directory.GetFiles(path, searchPattern, searchOption);
        }

        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            ErrorUtilities.VerifyThrow(searchOption == SearchOption.TopDirectoryOnly, $"In net20 {nameof(Directory.GetFileSystemEntries)} does not take a {nameof(SearchOption)} parameter");

            return Directory.GetFileSystemEntries(path, searchPattern);
        }

        public bool FileExists(string path)
        {
            return NativeMethodsShared.FileExists(path);
        }
    }
}
