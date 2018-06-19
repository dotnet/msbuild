// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Abstracts away some file system operations
    /// </summary>
    /// TODO: This interface has only enumeration and existence related methods. Consider extending it to include other file system operations.
    internal interface IFileSystemAbstraction
    {
        /// <summary>
        /// Returns an enumerable collection of file names that match a search pattern in a specified path, and optionally searches subdirectories.
        /// </summary>
        IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns an enumerable collection of directory names that match a search pattern in a specified path, and optionally searches subdirectories.
        /// </summary>
        IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Returns an enumerable collection of file names and directory names that match a search pattern in a specified path, and optionally searches subdirectories.
        /// </summary>
        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Determines whether the given path refers to an existing directory on disk.
        /// </summary>
        bool DirectoryExists(string path);

        /// <summary>
        /// Determines whether the given path refers to an existing file on disk.
        /// </summary>
        bool FileExists(string path);

        /// <summary>
        /// Determines whether the given path refers to an existing entry in the directory service.
        /// </summary>
        bool DirectoryEntryExists(string path);

        void ClearCaches();
    }
}
