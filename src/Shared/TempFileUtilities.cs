// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// It is in a separate file so that it can be selectively included into an assembly.
    /// </summary>
    internal static partial class FileUtilities
    {
        /// <inheritdoc cref="FrameworkFileUtilities.TempFileDirectory"/>
        internal static string TempFileDirectory
            => FrameworkFileUtilities.TempFileDirectory;

        /// <inheritdoc cref="FrameworkFileUtilities.ClearTempFileDirectory"/>
        internal static void ClearTempFileDirectory()
            => FrameworkFileUtilities.ClearTempFileDirectory();

        /// <inheritdoc cref="FrameworkFileUtilities.GetTemporaryDirectory(bool, string)"/>
        internal static string GetTemporaryDirectory(bool createDirectory = true, string subfolder = null)
            => FrameworkFileUtilities.GetTemporaryDirectory(createDirectory, subfolder);

        /// <inheritdoc cref="FrameworkFileUtilities.GetTemporaryFileName()"/>
        internal static string GetTemporaryFileName()
            => FrameworkFileUtilities.GetTemporaryFileName();

        /// <inheritdoc cref="FrameworkFileUtilities.GetTemporaryFileName(string)"/>
        internal static string GetTemporaryFileName(string extension)
            => FrameworkFileUtilities.GetTemporaryFileName(extension);

        /// <inheritdoc cref="FrameworkFileUtilities.GetTemporaryFile()"/>
        internal static string GetTemporaryFile()
            => FrameworkFileUtilities.GetTemporaryFile();

        /// <inheritdoc cref="FrameworkFileUtilities.GetTemporaryFile(string, string, bool)"/>
        internal static string GetTemporaryFile(string fileName, string extension, bool createFile)
            => FrameworkFileUtilities.GetTemporaryFile(fileName, extension, createFile);

        /// <inheritdoc cref="FrameworkFileUtilities.GetTemporaryFile(string)"/>
        internal static string GetTemporaryFile(string extension)
            => FrameworkFileUtilities.GetTemporaryFile(extension);

        /// <inheritdoc cref="FrameworkFileUtilities.GetTemporaryFile(string, string, string, bool)"/>
        internal static string GetTemporaryFile(string directory, string fileName, string extension, bool createFile = true)
            => FrameworkFileUtilities.GetTemporaryFile(directory, fileName, extension, createFile);

        /// <inheritdoc cref="FrameworkFileUtilities.CopyDirectory(string, string)"/>
        internal static void CopyDirectory(string source, string dest)
            => FrameworkFileUtilities.CopyDirectory(source, dest);
    }
}
