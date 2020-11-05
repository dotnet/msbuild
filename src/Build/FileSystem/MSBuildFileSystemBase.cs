// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Build.FileSystem
{
    /// <summary>
    /// Abstracts away some file system operations.
    ///
    /// Implementations:
    /// - must be thread safe
    /// - may cache some or all the calls.
    /// </summary>
    public abstract class MSBuildFileSystemBase
    {
        /// <summary>
        /// Use this for var sr = new StreamReader(path)
        /// </summary>
        public abstract TextReader ReadFile(string path);

        /// <summary>
        /// Use this for new FileStream(path, mode, access, share)
        /// </summary>
        public abstract Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share);

        /// <summary>
        /// Use this for File.ReadAllText(path)
        /// </summary>
        public abstract string ReadFileAllText(string path);

        /// <summary>
        /// Use this for File.ReadAllBytes(path)
        /// </summary>
        public abstract byte[] ReadFileAllBytes(string path);

        /// <summary>
        /// Use this for Directory.EnumerateFiles(path, pattern, option)
        /// </summary>
        public abstract IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Use this for Directory.EnumerateFolders(path, pattern, option)
        /// </summary>
        public abstract IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Use this for Directory.EnumerateFileSystemEntries(path, pattern, option)
        /// </summary>
        public abstract IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Use this for File.GetAttributes()
        /// </summary>
        public abstract FileAttributes GetAttributes(string path);

        /// <summary>
        /// Use this for File.GetLastWriteTimeUtc(path)
        /// </summary>
        public abstract DateTime GetLastWriteTimeUtc(string path);

        /// <summary>
        /// Use this for Directory.Exists(path)
        /// </summary>
        public abstract bool DirectoryExists(string path);

        /// <summary>
        /// Use this for File.Exists(path)
        /// </summary>
        public abstract bool FileExists(string path);

        /// <summary>
        /// Use this for File.Exists(path) || Directory.Exists(path)
        /// </summary>
        public abstract bool FileOrDirectoryExists(string path);
    }
}
