// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.CompilerServices;
using Microsoft.Build.Shared.FileSystem;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// It is in a separate file so that it can be selectively included into an assembly.
    /// </summary>
    internal static partial class FileUtilities
    {
        // For the current user, these correspond to read, write, and execute permissions.
        // Lower order bits correspond to the same for "group" or "other" users.
        private const int userRWX = 0x100 | 0x80 | 0x40;
        private static string tempFileDirectory = null;
        private const string msbuildTempFolderPrefix = "MSBuildTemp";

        internal static string TempFileDirectory
        {
            get
            {
                return tempFileDirectory ??= CreateFolderUnderTemp();
            }
        }

        internal static void ClearTempFileDirectory()
        {
            tempFileDirectory = null;
        }

        // For all native calls, directly check their return values to prevent bad actors from getting in between checking if a directory exists and returning it.
        private static string CreateFolderUnderTemp()
        {
            // On windows Username with Unicode chars can give issues, so we dont append username to the temp folder name.
            string msbuildTempFolder = NativeMethodsShared.IsWindows ?
                msbuildTempFolderPrefix :
                msbuildTempFolderPrefix + Environment.UserName;

            string basePath = Path.Combine(Path.GetTempPath(), msbuildTempFolder);

            if (NativeMethodsShared.IsLinux && NativeMethodsShared.mkdir(basePath, userRWX) != 0)
            {
                if (NativeMethodsShared.chmod(basePath, userRWX) == 0)
                {
                    // Current user owns this file; we can read and write to it. It is reasonable here to assume it was created properly by MSBuild and can be used
                    // for temporary files.
                }
                else
                {
                    // Another user created a folder pretending to be us! Find a folder we can actually use.
                    int extraBits = 0;
                    string pathToCheck = basePath + extraBits;
                    while (NativeMethodsShared.mkdir(pathToCheck, userRWX) != 0 && NativeMethodsShared.chmod(pathToCheck, userRWX) != 0)
                    {
                        extraBits++;
                        pathToCheck = basePath + extraBits;
                    }

                    basePath = pathToCheck;
                }
            }
            else
            {
                Directory.CreateDirectory(basePath);
            }

            return FileUtilities.EnsureTrailingSlash(basePath);
        }

        /// <summary>
        /// Generates a unique directory name in the temporary folder.
        /// Caller must delete when finished.
        /// </summary>
        /// <param name="createDirectory"></param>
        /// <param name="subfolder"></param>
        internal static string GetTemporaryDirectory(bool createDirectory = true, string subfolder = null)
        {
            string temporaryDirectory = Path.Combine(TempFileDirectory, $"Temporary{Guid.NewGuid():N}", subfolder ?? string.Empty);

            if (createDirectory)
            {
                Directory.CreateDirectory(temporaryDirectory);
            }

            return temporaryDirectory;
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// File will NOT be created.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFileName()
        {
            return GetTemporaryFileName(".tmp");
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// File will NOT be created.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFileName(string extension)
        {
            return GetTemporaryFile(null, null, extension, false);
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// If no extension is provided, uses ".tmp".
        /// File is guaranteed to be unique.
        /// Caller must delete it when finished.
        /// </summary>
        internal static string GetTemporaryFile()
        {
            return GetTemporaryFile(".tmp");
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// File is guaranteed to be unique.
        /// Caller must delete it when finished.
        /// </summary>
        internal static string GetTemporaryFile(string fileName, string extension, bool createFile)
        {
            return GetTemporaryFile(null, fileName, extension, createFile);
        }

        /// <summary>
        /// Generates a unique temporary file name with a given extension in the temporary folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// Caller must delete it when finished.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFile(string extension)
        {
            return GetTemporaryFile(null, null, extension);
        }

        /// <summary>
        /// Creates a file with unique temporary file name with a given extension in the specified folder.
        /// File is guaranteed to be unique.
        /// Extension may have an initial period.
        /// If folder is null, the temporary folder will be used.
        /// Caller must delete it when finished.
        /// May throw IOException.
        /// </summary>
        internal static string GetTemporaryFile(string directory, string fileName, string extension, bool createFile = true)
        {
            ErrorUtilities.VerifyThrowArgumentLengthIfNotNull(directory, nameof(directory));

            try
            {
                directory ??= TempFileDirectory;

                // If the extension needs a dot prepended, do so.
                if (extension is null)
                {
                    extension = string.Empty;
                }
                else if (extension.Length > 0 && extension[0] != '.')
                {
                    extension = '.' + extension;
                }

                // If the fileName is null, use tmp{Guid}; otherwise use fileName.
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = $"tmp{Guid.NewGuid():N}";
                }

                Directory.CreateDirectory(directory);

                string file = Path.Combine(directory, $"{fileName}{extension}");

                ErrorUtilities.VerifyThrow(!FileSystems.Default.FileExists(file), "Guid should be unique");

                if (createFile)
                {
                    File.WriteAllText(file, string.Empty);
                }

                return file;
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                throw new IOException(ResourceUtilities.FormatResourceStringStripCodeAndKeyword("Shared.FailedCreatingTempFile", ex.Message), ex);
            }
        }

        internal static void CopyDirectory(string source, string dest)
        {
            Directory.CreateDirectory(dest);

            DirectoryInfo sourceInfo = new DirectoryInfo(source);
            foreach (var fileInfo in sourceInfo.GetFiles())
            {
                string destFile = Path.Combine(dest, fileInfo.Name);
                fileInfo.CopyTo(destFile);
            }
            foreach (var subdirInfo in sourceInfo.GetDirectories())
            {
                string destDir = Path.Combine(dest, subdirInfo.Name);
                CopyDirectory(subdirInfo.FullName, destDir);
            }
        }

        public sealed class TempWorkingDirectory : IDisposable
        {
            public string Path { get; }

            public TempWorkingDirectory(string sourcePath,
#if !CLR2COMPATIBILITY
                [CallerMemberName]
#endif
            string name = null)
            {
                Path = name == null
                    ? GetTemporaryDirectory()
                    : System.IO.Path.Combine(TempFileDirectory, name);

                if (FileSystems.Default.DirectoryExists(Path))
                {
                    Directory.Delete(Path, true);
                }

                CopyDirectory(sourcePath, Path);
            }

            public void Dispose()
            {
                Directory.Delete(Path, true);
            }
        }
    }
}
