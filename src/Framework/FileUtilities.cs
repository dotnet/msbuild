// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

namespace Microsoft.Build.Framework
{
    // TODO: this should be unified with Shared\FileUtilities, but it is hard to untangle everything in one go.
    // Moved some of the methods here for now.

    /// <summary>
    /// This class contains utility methods for file IO.
    /// Functions from FileUtilities are transferred here as part of the effort to remove Shared files.
    /// </summary>
    internal static class FrameworkFileUtilities
    {
        internal static readonly char[] Slashes = ['/', '\\'];

        /// <summary>
        /// Indicates if the given character is a slash.
        /// </summary>
        /// <param name="c"></param>
        /// <returns>true, if slash</returns>
        internal static bool IsSlash(char c)
        {
            return (c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Indicates if the given file-spec ends with a slash.
        /// </summary>
        /// <param name="fileSpec">The file spec.</param>
        /// <returns>true, if file-spec has trailing slash</returns>
        internal static bool EndsWithSlash(string fileSpec)
        {
            return (fileSpec.Length > 0)
                ? IsSlash(fileSpec[fileSpec.Length - 1])
                : false;
        }

        internal static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');
        }

        /// <summary>
        /// If the given path doesn't have a trailing slash then add one.
        /// If the path is an empty string, does not modify it.
        /// </summary>
        /// <param name="fileSpec">The path to check.</param>
        /// <returns>A path with a slash.</returns>
        internal static string EnsureTrailingSlash(string fileSpec)
        {
            fileSpec = FixFilePath(fileSpec);
            if (fileSpec.Length > 0 && !IsSlash(fileSpec[fileSpec.Length - 1]))
            {
                fileSpec += Path.DirectorySeparatorChar;
            }

            return fileSpec;
        }

        /// <summary>
        /// Ensures the path does not have a trailing slash.
        /// </summary>
        internal static string EnsureNoTrailingSlash(string path)
        {
            path = FixFilePath(path);
            if (EndsWithSlash(path))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }

#if !TASKHOST
        /// <summary>
        /// If the given path doesn't have a trailing slash then add one.
        /// </summary>
        /// <param name="path">The absolute path to check.</param>
        /// <returns>An absolute path with a trailing slash.</returns>
        internal static AbsolutePath EnsureTrailingSlash(AbsolutePath path)
        {
            return new AbsolutePath(EnsureTrailingSlash(path.Value), 
                original: path.OriginalValue, 
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Ensures the absolute path does not have a trailing slash.
        /// </summary>
        /// <param name="path">The absolute path to check.</param>
        /// <returns>An absolute path without a trailing slash.</returns>
        internal static AbsolutePath EnsureNoTrailingSlash(AbsolutePath path)
        {
            return new AbsolutePath(EnsureNoTrailingSlash(path.Value),
                original: path.OriginalValue, 
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Gets the canonicalized full path of the provided path.
        /// Resolves relative segments like "." and "..". Fixes directory separators.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        internal static AbsolutePath NormalizePath(AbsolutePath path)
        {
            return new AbsolutePath(FixFilePath(Path.GetFullPath(path.Value)),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Resolves relative segments like "." and "..".
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        internal static AbsolutePath RemoveRelativeSegments(AbsolutePath path)
        {
            return new AbsolutePath(Path.GetFullPath(path.Value), 
                original: path.OriginalValue, 
                ignoreRootedCheck: true);
        }

        internal static AbsolutePath FixFilePath(AbsolutePath path)
        {
            return new AbsolutePath(FixFilePath(path.Value),
                original: path.OriginalValue, 
                ignoreRootedCheck: true);
        }
#endif
    }
}