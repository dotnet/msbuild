// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Implementation of file system operations directly over the dot net managed layer
    /// </summary>
    internal sealed class ManagedFileSystem : IFileSystemAbstraction
    {
        private static readonly ManagedFileSystem Instance = new ManagedFileSystem();

        /// <nodoc/>
        public static ManagedFileSystem Singleton() => ManagedFileSystem.Instance;

        private ManagedFileSystem()
        { }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateDirectories(path, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        /// <inheritdoc/>
        public bool FileExists(string path)
        {
            return File.Exists(path);
        }

        /// <inheritdoc/>
        public bool DirectoryEntryExists(string path)
        {
            return FileExists(path) || DirectoryExists(path);
        }

        public void ClearCaches()
        {
        }
    }
}
