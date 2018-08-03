// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// The type of file artifact to search for
    /// </summary>
    internal enum FileArtifactType : byte
    {
        /// <nodoc/>
        File,
        /// <nodoc/>
        Directory,
        /// <nodoc/>
        FileOrDirectory
    }

    /// <summary>
    /// Windows-specific implementation of file system operations using Windows native invocations
    /// </summary>
    internal class WindowsFileSystem : IFileSystem
    {
        private static readonly WindowsFileSystem Instance = new WindowsFileSystem();

        /// <nodoc/>
        public static WindowsFileSystem Singleton() => WindowsFileSystem.Instance;

        private WindowsFileSystem()
        { }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.File, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.Directory, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
            return EnumerateFileOrDirectories(path, FileArtifactType.FileOrDirectory, searchPattern, searchOption);
        }

        /// <inheritdoc/>
        public bool DirectoryExists(string path)
        {
            return NativeMethodsShared.DirectoryExistsWindows(path);
        }

        /// <inheritdoc/>
        public bool FileExists(string path)
        {
            return NativeMethodsShared.FileExistsWindows(path);
        }

        /// <inheritdoc/>
        public bool DirectoryEntryExists(string path)
        {
            return NativeMethodsShared.FileOrDirectoryExistsWindows(path);
        }

        private static IEnumerable<string> EnumerateFileOrDirectories(
            string directoryPath,
            FileArtifactType fileArtifactType,
            string searchPattern,
            SearchOption searchOption = SearchOption.TopDirectoryOnly)
        {
            var enumeration = new List<string>();

            var result = CustomEnumerateDirectoryEntries(
                directoryPath,
                fileArtifactType,
                searchPattern,
                searchOption,
                enumeration);

            // If the result indicates that the enumeration succeeded or the directory does not exist, then the result is considered success.
            // In particular, if the globed directory does not exist, then we want to return the empty file, and track for the anti-dependency.
            if (
                !(result.Status == WindowsNative.EnumerateDirectoryStatus.Success ||
                  result.Status == WindowsNative.EnumerateDirectoryStatus.SearchDirectoryNotFound))
            {
                throw result.CreateExceptionForError();
            }

            return enumeration;
        }

        private static WindowsNative.EnumerateDirectoryResult CustomEnumerateDirectoryEntries(
            string directoryPath,
            FileArtifactType fileArtifactType,
            string pattern,
            SearchOption searchOption,
            ICollection<string> result)
        {
            var searchDirectoryPath = Path.Combine(directoryPath, "*");

            WindowsNative.Win32FindData findResult;
            using (var findHandle = WindowsNative.FindFirstFileW(searchDirectoryPath, out findResult))
            {
                if (findHandle.IsInvalid)
                {
                    int hr = Marshal.GetLastWin32Error();
                    Debug.Assert(hr != WindowsNative.ErrorFileNotFound);

                    WindowsNative.EnumerateDirectoryStatus findHandleOpenStatus;
                    switch (hr)
                    {
                        case WindowsNative.ErrorFileNotFound:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.SearchDirectoryNotFound;
                            break;
                        case WindowsNative.ErrorPathNotFound:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.SearchDirectoryNotFound;
                            break;
                        case WindowsNative.ErrorDirectory:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.CannotEnumerateFile;
                            break;
                        case WindowsNative.ErrorAccessDenied:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.AccessDenied;
                            break;
                        default:
                            findHandleOpenStatus = WindowsNative.EnumerateDirectoryStatus.UnknownError;
                            break;
                    }

                    return new WindowsNative.EnumerateDirectoryResult(directoryPath, findHandleOpenStatus, hr);
                }

                while (true)
                {
                    var isDirectory = (findResult.DwFileAttributes & FileAttributes.Directory) != 0;

                    // There will be entries for the current and parent directories. Ignore those.
                    if (!isDirectory || (findResult.CFileName != "." && findResult.CFileName != ".."))
                    {
                        // Make sure pattern and directory/file filters are honored
                        // We special case the "*" pattern since it is the default when no pattern is specified
                        // so we avoid calling the matching function
                        if (pattern == "*" ||
                            WindowsNative.PathMatchSpecExW(findResult.CFileName, pattern, WindowsNative.DwFlags.PmsfNormal) ==
                            WindowsNative.ErrorSuccess)
                        {
                            if (fileArtifactType == FileArtifactType.FileOrDirectory ||
                                !(fileArtifactType == FileArtifactType.Directory ^ isDirectory))
                            {
                                result.Add(Path.Combine(directoryPath, findResult.CFileName));
                            }
                        }

                        // Recursively go into subfolders if specified
                        if (searchOption == SearchOption.AllDirectories && isDirectory)
                        {
                            var recurs = CustomEnumerateDirectoryEntries(
                                Path.Combine(directoryPath, findResult.CFileName),
                                fileArtifactType,
                                pattern,
                                searchOption,
                                result);

                            if (!recurs.Succeeded)
                            {
                                return recurs;
                            }
                        }
                    }

                    if (!WindowsNative.FindNextFileW(findHandle, out findResult))
                    {
                        int hr = Marshal.GetLastWin32Error();
                        if (hr == WindowsNative.ErrorNoMoreFiles)
                        {
                            // Graceful completion of enumeration.
                            return new WindowsNative.EnumerateDirectoryResult(
                                directoryPath,
                                WindowsNative.EnumerateDirectoryStatus.Success,
                                hr);
                        }

                        Debug.Assert(hr != WindowsNative.ErrorSuccess);
                        return new WindowsNative.EnumerateDirectoryResult(
                            directoryPath,
                            WindowsNative.EnumerateDirectoryStatus.UnknownError,
                            hr);
                    }
                }
            }
        }
    }
}
