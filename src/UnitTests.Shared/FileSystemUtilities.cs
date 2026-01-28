// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

#nullable disable

namespace Microsoft.Build.UnitTests.Shared
{
    /// <summary>
    /// File system utilities for unit tests.
    /// </summary>
    public static class FileSystemUtilities
    {
        /// <summary>
        /// Recursively copies all files and directories from source to target path.
        /// </summary>
        /// <param name="sourcePath">Source directory path</param>
        /// <param name="targetPath">Target directory path</param>
        public static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            // First Create all directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            // Then copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }
    }
}