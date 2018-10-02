// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Implementation of file system operations directly over the dot net managed layer
    /// </summary>
    internal sealed class MSBuildOnWindowsFileSystem : IFileSystem
    {
        private static readonly MSBuildOnWindowsFileSystem Instance = new MSBuildOnWindowsFileSystem();

        /// <nodoc/>
        public static MSBuildOnWindowsFileSystem Singleton() => Instance;

        private MSBuildOnWindowsFileSystem()
        { }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return ManagedFileSystem.Singleton().EnumerateFiles(path, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return ManagedFileSystem.Singleton().EnumerateDirectories(path, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            return ManagedFileSystem.Singleton().EnumerateFileSystemEntries(path, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public bool DirectoryExists(string path)
        {
            return WindowsFileSystem.Singleton().DirectoryExists(path);
        }

        /// <inheritdoc/>
        public bool FileExists(string path)
        {
            return WindowsFileSystem.Singleton().FileExists(path);
        }

        /// <inheritdoc/>
        public bool DirectoryEntryExists(string path)
        {
            return WindowsFileSystem.Singleton().DirectoryEntryExists(path);
        }
    }
}
