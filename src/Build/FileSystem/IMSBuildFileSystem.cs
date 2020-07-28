// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
    public interface IMSBuildFileSystem
    {
        /// <summary>
        /// Use this for var sr = new StreamReader(path)
        /// </summary>
        TextReader ReadFile(string path);

        /// <summary>
        /// Use this for new FileStream(path, mode, access, share)
        /// </summary>
        Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share);

        /// <summary>
        /// Use this for File.ReadAllText(path)
        /// </summary>
        string ReadFileAllText(string path);

        /// <summary>
        /// Use this for File.ReadAllBytes(path)
        /// </summary>
        byte[] ReadFileAllBytes(string path);

        /// <summary>
        /// Use this for Directory.EnumerateFiles(path, pattern, option)
        /// </summary>
        IEnumerable<string> EnumerateFiles(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Use this for Directory.EnumerateFolders(path, pattern, option)
        /// </summary>
        IEnumerable<string> EnumerateDirectories(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Use this for Directory.EnumerateFileSystemEntries(path, pattern, option)
        /// </summary>
        IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);

        /// <summary>
        /// Gets file or directory attributes for a path.
        /// NOTE: The semantics here are different from File.GetAttributes() -
        /// the attribute value will be -1 if cast to an integer when the file or directory
        /// is missing, instead of throwing FileNotFoundException or DirectoryNotFoundException.
        /// </summary>
        FileAttributes GetAttributesNoMissingException(string path);

        /// <summary>
        /// Use this for Directory.Exists(path)
        /// </summary>
        bool DirectoryExists(string path);

        /// <summary>
        /// Use this for File.Exists(path)
        /// </summary>
        bool FileExists(string path);

        /// <summary>
        /// Use this for File.Exists(path) || Directory.Exists(path)
        /// </summary>
        bool DirectoryEntryExists(string path);

        /// <summary>
        /// Clears cached information to reduce memory usage.
        /// </summary>
        void ClearCaches();

        /// <summary>
        /// Write usage statistics.
        /// </summary>
        void WriteStatistics(TextWriter writer);
    }
}
