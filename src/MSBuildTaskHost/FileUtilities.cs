// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;

#nullable disable

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// PERF\COVERAGE NOTE: Try to keep classes in 'shared' as granular as possible. All the methods in
    /// each class get pulled into the resulting assembly.
    /// </summary>
    internal static partial class FileUtilities
    {
        internal static string TempFileDirectory => Path.GetTempPath();

        /// <summary>
        /// Indicates if the given character is a slash.
        /// </summary>
        /// <param name="c"></param>
        /// <returns>true, if slash</returns>
        private static bool IsSlash(char c)
        {
            return (c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar);
        }

        /// <summary>
        /// Indicates if the given file-spec ends with a slash.
        /// </summary>
        /// <param name="fileSpec">The file spec.</param>
        /// <returns>true, if file-spec has trailing slash</returns>
        private static bool EndsWithSlash(string fileSpec)
        {
            return (fileSpec.Length > 0)
                ? IsSlash(fileSpec[fileSpec.Length - 1])
                : false;
        }

        private static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');
        }

        /// <summary>
        /// Gets the canonicalized full path of the provided path.
        /// Guidance for use: call this on all paths accepted through public entry
        /// points that need normalization. After that point, only verify the path
        /// is rooted, using ErrorUtilities.VerifyThrowPathRooted.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        private static string NormalizePath(string path)
        {
            ErrorUtilities.VerifyThrowArgumentLength(path);
            string fullPath = GetFullPath(path);
            return FixFilePath(fullPath);
        }

        private static string GetFullPath(string path)
        {
            string uncheckedFullPath = NativeMethodsShared.GetFullPath(path);

            if (IsPathTooLong(uncheckedFullPath))
            {
                string message = ResourceUtilities.FormatString(AssemblyResources.GetString("Shared.PathTooLong"), path, NativeMethodsShared.MaxPath);
                throw new PathTooLongException(message);
            }

            // We really don't care about extensions here, but Path.HasExtension provides a great way to
            // invoke the CLR's invalid path checks (these are independent of path length)
            _ = Path.HasExtension(uncheckedFullPath);

            // If we detect we are a UNC path then we need to use the regular get full path in order to do the correct checks for UNC formatting
            // and security checks for strings like \\?\GlobalRoot
            return IsUNCPath(uncheckedFullPath) ? Path.GetFullPath(uncheckedFullPath) : uncheckedFullPath;
        }

        private static bool IsUNCPath(string path)
        {
            if (!path.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return false;
            }

            bool isUNC = true;
            for (int i = 2; i < path.Length - 1; i++)
            {
                if (path[i] == '\\')
                {
                    isUNC = false;
                    break;
                }
            }

            /*
              From Path.cs in the CLR

              Throw an ArgumentException for paths like \\, \\server, \\server\
              This check can only be properly done after normalizing, so
              \\foo\.. will be properly rejected.  Also, reject \\?\GLOBALROOT\
              (an internal kernel path) because it provides aliases for drives.

              throw new ArgumentException(Environment.GetResourceString("Arg_PathIllegalUNC"));

               // Check for \\?\Globalroot, an internal mechanism to the kernel
               // that provides aliases for drives and other undocumented stuff.
               // The kernel team won't even describe the full set of what
               // is available here - we don't want managed apps mucking
               // with this for security reasons.
            */
            return isUNC || path.IndexOf(@"\\?\globalroot", StringComparison.OrdinalIgnoreCase) != -1;
        }

        /// <summary>
        /// Extracts the directory from the given file-spec.
        /// </summary>
        /// <param name="fileSpec">The filespec.</param>
        /// <returns>directory path</returns>
        private static string GetDirectory(string fileSpec)
        {
            string directory = Path.GetDirectoryName(FixFilePath(fileSpec));

            // if file-spec is a root directory e.g. c:, c:\, \, \\server\share
            // NOTE: Path.GetDirectoryName also treats invalid UNC file-specs as root directories e.g. \\, \\server
            if (directory == null)
            {
                // just use the file-spec as-is
                directory = fileSpec;
            }
            else if ((directory.Length > 0) && !EndsWithSlash(directory))
            {
                // restore trailing slash if Path.GetDirectoryName has removed it (this happens with non-root directories)
                directory += Path.DirectorySeparatorChar;
            }

            return directory;
        }

        // ISO 8601 Universal time with sortable format
        internal const string FileTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

        /// <summary>
        /// Get the currently executing assembly path
        /// </summary>
        internal static string ExecutingAssemblyPath => Path.GetFullPath(typeof(FileUtilities).Assembly.Location);

        /// <summary>
        /// Determines the full path for the given file-spec.
        /// ASSUMES INPUT IS STILL ESCAPED
        /// </summary>
        /// <param name="fileSpec">The file spec to get the full path of.</param>
        /// <param name="currentDirectory"></param>
        /// <param name="escape">Whether to escape the path after getting the full path.</param>
        /// <returns>Full path to the file, escaped if not specified otherwise.</returns>
        private static string GetFullPath(string fileSpec, string currentDirectory, bool escape = true)
        {
            // Sending data out of the engine into the filesystem, so time to unescape.
            fileSpec = FixFilePath(EscapingUtilities.UnescapeAll(fileSpec));

            string fullPath = NormalizePath(Path.Combine(currentDirectory, fileSpec));
            // In some cases we might want to NOT escape in order to preserve symbols like @, %, $ etc.
            if (escape)
            {
                // Data coming back from the filesystem into the engine, so time to escape it back.
                fullPath = EscapingUtilities.Escape(fullPath);
            }

            if (!EndsWithSlash(fullPath))
            {
                if (FileUtilitiesRegex.IsDrivePattern(fileSpec) ||
                    FileUtilitiesRegex.IsUncPattern(fullPath))
                {
                    // append trailing slash if Path.GetFullPath failed to (this happens with drive-specs and UNC shares)
                    fullPath += Path.DirectorySeparatorChar;
                }
            }

            return fullPath;
        }

        /// <summary>
        /// A variation of Path.GetFullPath that will return the input value
        /// instead of throwing any IO exception.
        /// Useful to get a better path for an error message, without the risk of throwing
        /// if the error message was itself caused by the path being invalid!
        /// </summary>
        private static string GetFullPathNoThrow(string path)
        {
            try
            {
                path = NormalizePath(path);
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
            }

            return path;
        }

        /// <summary>
        /// Gets a file info object for the specified file path. If the file path
        /// is invalid, or is a directory, or cannot be accessed, or does not exist,
        /// it returns null rather than throwing or returning a FileInfo around a non-existent file.
        /// This allows it to be called where File.Exists() (which never throws, and returns false
        /// for directories) was called - but with the advantage that a FileInfo object is returned
        /// that can be queried (e.g., for LastWriteTime) without hitting the disk again.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>FileInfo around path if it is an existing /file/, else null</returns>
        private static FileInfo GetFileInfoNoThrow(string filePath)
        {
            filePath = AttemptToShortenPath(filePath);

            FileInfo fileInfo;

            try
            {
                fileInfo = new FileInfo(filePath);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Invalid or inaccessible path: treat as if nonexistent file, just as File.Exists does
                return null;
            }

            if (fileInfo.Exists)
            {
                // It's an existing file
                return fileInfo;
            }
            else
            {
                // Nonexistent, or existing but a directory, just as File.Exists behaves
                return null;
            }
        }

        /// <summary>
        /// Normalizes the path if and only if it is longer than max path,
        /// or would be if rooted by the current directory.
        /// This may make it shorter by removing ".."'s.
        /// </summary>
        private static string AttemptToShortenPath(string path)
        {
            if (IsPathTooLong(path) || IsPathTooLongIfRooted(path))
            {
                // Attempt to make it shorter -- perhaps there are some \..\ elements
                path = GetFullPathNoThrow(path);
            }
            return FixFilePath(path);
        }

        private static bool IsPathTooLong(string path)
        {
            // >= not > because MAX_PATH assumes a trailing null
            return path.Length >= NativeMethodsShared.MaxPath;
        }

        private static bool IsPathTooLongIfRooted(string path)
        {
            bool hasMaxPath = NativeMethodsShared.HasMaxPath;
            int maxPath = NativeMethodsShared.MaxPath;
            // >= not > because MAX_PATH assumes a trailing null
            return hasMaxPath && !IsRootedNoThrow(path) && NativeMethodsShared.GetCurrentDirectory().Length + path.Length + 1 /* slash */ >= maxPath;
        }

        /// <summary>
        /// A variation of Path.IsRooted that not throw any IO exception.
        /// </summary>
        private static bool IsRootedNoThrow(string path)
        {
            try
            {
                return Path.IsPathRooted(FixFilePath(path));
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                return false;
            }
        }

        /// <summary>
        /// Get the folder N levels above the given. Will stop and return current path when rooted.
        /// </summary>
        /// <param name="path">Path to get the folder above.</param>
        /// <param name="count">Number of levels up to walk.</param>
        /// <returns>Full path to the folder N levels above the path.</returns>
        internal static string GetFolderAbove(string path, int count = 1)
        {
            if (count < 1)
            {
                return path;
            }

            var parent = Directory.GetParent(path);

            while (count > 1 && parent?.Parent != null)
            {
                parent = parent.Parent;
                count--;
            }

            return parent?.FullName ?? path;
        }

        /// <summary>
        /// Combine multiple paths. Should only be used when compiling against .NET 2.0.
        /// <remarks>
        /// Only use in .NET 2.0. Otherwise, use System.IO.Path.Combine(...)
        /// </remarks>
        /// </summary>
        /// <param name="root">Root path.</param>
        /// <param name="paths">Paths to concatenate.</param>
        /// <returns>Combined path.</returns>
        internal static string CombinePaths(string root, params string[] paths)
        {
            ErrorUtilities.VerifyThrowArgumentNull(root);
            ErrorUtilities.VerifyThrowArgumentNull(paths);

            return paths.Aggregate(root, Path.Combine);
        }

        internal static StreamWriter OpenWrite(string path, bool append, Encoding encoding = null)
        {
            const int DefaultFileStreamBufferSize = 4096;
            FileMode mode = append ? FileMode.Append : FileMode.Create;
            Stream fileStream = new FileStream(path, mode, FileAccess.Write, FileShare.Read, DefaultFileStreamBufferSize, FileOptions.SequentialScan);
            if (encoding == null)
            {
                return new StreamWriter(fileStream);
            }
            else
            {
                return new StreamWriter(fileStream, encoding);
            }
        }
    }
}
