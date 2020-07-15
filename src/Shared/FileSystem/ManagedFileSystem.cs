// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Implementation of file system operations directly over the dot net managed layer
    /// </summary>
    internal class ManagedFileSystem : IFileSystem
    {
        private static readonly ManagedFileSystem Instance = new ManagedFileSystem();

        public static ManagedFileSystem Singleton() => ManagedFileSystem.Instance;

        protected ManagedFileSystem() { }

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

        public virtual IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
        }

        public virtual IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateDirectories(path, searchPattern, searchOption);
        }

        public virtual IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

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

        public virtual bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public virtual bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public virtual bool DirectoryEntryExists(string path)
        {
            return FileExists(path) || DirectoryExists(path);
        }

        public void ClearCaches() { }

        public void WriteStatistics(TextWriter writer) { }
    }
}
