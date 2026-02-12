// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !TASKHOST
using System.Threading;
#endif

#if NETFRAMEWORK && !TASKHOST
using Path = Microsoft.IO.Path;
#else
using System.IO;
#endif

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
        private const char UnixDirectorySeparator = '/';
        private const char WindowsDirectorySeparator = '\\';

        internal static readonly char[] Slashes = [UnixDirectorySeparator, WindowsDirectorySeparator];

#if !TASKHOST
        /// <summary>
        /// AsyncLocal working directory for use during property/item expansion in multithreaded mode.
        /// Set by MultiThreadedTaskEnvironmentDriver when building projects. null in multi-process mode.
        /// Using AsyncLocal ensures the value flows to child threads/tasks spawned during execution of tasks.
        /// </summary>
        private static readonly AsyncLocal<string?> s_currentThreadWorkingDirectory = new();

        internal static string? CurrentThreadWorkingDirectory
        {
            get => s_currentThreadWorkingDirectory.Value;
            set => s_currentThreadWorkingDirectory.Value = value;
        }
#endif

        /// <summary>
        /// Indicates if the given character is a slash in current OS.
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

        /// <summary>
        /// Fixes backslashes to forward slashes on Unix. This allows to recognise windows style paths on Unix. 
        /// However, this leads to incorrect path on Linux if backslash was part of the file/directory name.
        /// </summary>  
        internal static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == WindowsDirectorySeparator ? path : path.Replace(WindowsDirectorySeparator, UnixDirectorySeparator);
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
            if (path.Length > 0 && IsSlash(path[path.Length - 1]))
            {
                path = path.Substring(0, path.Length - 1);
            }

            return path;
        }

#if !TASKHOST
        /// <summary>
        /// Checks if the path contains backslashes on Unix.
        /// </summary>
        private static bool HasWindowsDirectorySeparatorOnUnix(string path)
            => NativeMethods.IsUnixLike && path.IndexOf(WindowsDirectorySeparator) >= 0;

        /// <summary>
        /// Checks if the path contains forward slashes on Windows.
        /// </summary>
        private static bool HasUnixDirectorySeparatorOnWindows(string path)
            => NativeMethods.IsWindows && path.IndexOf(UnixDirectorySeparator) >= 0;

        /// <summary>
        /// Quickly checks if the path may contain relative segments like "." or "..".
        /// This is a non-precise detection that may have false positives but no false negatives.
        /// </summary>
        /// <remarks>
        /// Check for relative path segments "." and ".."
        /// In absolute path those segments can not appear in the beginning of the path, only after a path separator.
        /// This is not a precise full detection of relative segments. There are no false negatives as this might affect correctness, but it may have false positives:
        /// like when there is a hidden file or directory starting with a dot, or on linux the backslash and dot can be part of the file name.
        /// </remarks>
        private static bool MayHaveRelativeSegment(string path)
            => path.Contains("/.") || path.Contains("\\.");

        /// <summary>
        /// If the given path doesn't have a trailing slash then add one.
        /// </summary>
        /// <param name="path">The absolute path to check.</param>
        /// <returns>An absolute path with a trailing slash.</returns>
        /// <remarks>
        /// If the path does not require modification, returns the current instance to avoid unnecessary allocations.
        /// Preserves the OriginalValue of the current instance.
        /// </remarks>
        internal static AbsolutePath EnsureTrailingSlash(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                return path;
            }

            // Check if the path already has a trailing slash and no separator fixing is needed on Unix.
            // EnsureTrailingSlash should also fix the path separators on Unix.
            if (IsSlash(path.Value[path.Value.Length - 1]) && !HasWindowsDirectorySeparatorOnUnix(path.Value))
            {
                return path;
            }

            return new AbsolutePath(EnsureTrailingSlash(path.Value),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Ensures the absolute path does not have a trailing slash.
        /// </summary>
        /// <param name="path">The absolute path to check.</param>
        /// <returns>An absolute path without a trailing slash.</returns>
        /// <remarks>
        /// If the path does not require modification, returns the current instance to avoid unnecessary allocations.
        /// Preserves the OriginalValue of the current instance.
        /// </remarks>
        internal static AbsolutePath EnsureNoTrailingSlash(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                return path;
            }

            // Check if already has no trailing slash and no separator fixing needed on unix 
            // (EnsureNoTrailingSlash also should fix the paths on unix). 
            if (!IsSlash(path.Value[path.Value.Length - 1]) && !HasWindowsDirectorySeparatorOnUnix(path.Value))
            {
                return path;
            }

            return new AbsolutePath(EnsureNoTrailingSlash(path.Value),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Gets the canonicalized full path of the provided path.
        /// Resolves relative segments like "." and "..". Fixes directory separators.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        /// <remarks>
        /// If the path does not require modification, returns the current instance to avoid unnecessary allocations.
        /// Preserves the OriginalValue of the current instance.
        /// </remarks>
        internal static AbsolutePath NormalizePath(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                return path;
            }

            if (!MayHaveRelativeSegment(path.Value) &&
                !HasWindowsDirectorySeparatorOnUnix(path.Value) &&
                !HasUnixDirectorySeparatorOnWindows(path.Value))
            {
                return path;
            }

            return new AbsolutePath(FixFilePath(Path.GetFullPath(path.Value)),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Resolves relative segments like "." and "..". Fixes directory separators on Windows like Path.GetFullPath does.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        internal static AbsolutePath RemoveRelativeSegments(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value))
            {
                return path;
            }

            if (!MayHaveRelativeSegment(path.Value) && !HasUnixDirectorySeparatorOnWindows(path.Value))
            {
                return path;
            }

            return new AbsolutePath(Path.GetFullPath(path.Value),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }

        /// <summary>
        /// Fixes file path separators for the current platform.
        /// </summary>
        internal static AbsolutePath FixFilePath(AbsolutePath path)
        {
            if (string.IsNullOrEmpty(path.Value) || !HasWindowsDirectorySeparatorOnUnix(path.Value))
            {
                return path;
            }

            return new AbsolutePath(FixFilePath(path.Value),
                original: path.OriginalValue,
                ignoreRootedCheck: true);
        }
#endif
    }
}
