// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Text;
using System.Threading;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// PERF\COVERAGE NOTE: Try to keep classes in 'shared' as granular as possible. All the methods in 
    /// each class get pulled into the resulting assembly.
    /// </summary>
    internal static partial class FileUtilities
    {
        // A list of possible test runners. If the program running has one of these substrings in the name, we assume
        // this is a test harness.


        // This flag, when set, indicates that we are running tests. Initially assume it's true. It also implies that
        // the currentExecutableOverride is set to a path (that is non-null). Assume this is not initialized when we
        // have the impossible combination of runningTests = false and currentExecutableOverride = null.

        // This is the fake current executable we use in case we are running tests.

        // MaxPath accounts for the null-terminating character, for example, the maximum path on the D drive is "D:\<256 chars>\0". 
        // See: ndp\clr\src\BCL\System\IO\Path.cs
        internal const int MaxPath = 260;

        /// <summary>
        /// The directory where MSBuild stores cache information used during the build.
        /// </summary>
        internal static string cacheDirectory = null;

        /// <summary>
        /// FOR UNIT TESTS ONLY
        /// Clear out the static variable used for the cache directory so that tests that 
        /// modify it can validate their modifications. 
        /// </summary>
        internal static void ClearCacheDirectoryPath()
        {
            cacheDirectory = null;
        }

        // TODO: assumption on file system case sensitivity: https://github.com/Microsoft/msbuild/issues/781
        internal static readonly StringComparison PathComparison = StringComparison.OrdinalIgnoreCase;

        /// <summary>
        /// Copied from https://github.com/dotnet/corefx/blob/056715ff70e14712419d82d51c8c50c54b9ea795/src/Common/src/System/IO/PathInternal.Windows.cs#L61
        /// MSBuild should support the union of invalid path chars across the supported OSes, so builds can have the same behaviour crossplatform: https://github.com/Microsoft/msbuild/issues/781#issuecomment-243942514
        /// </summary>

        internal static char[] InvalidPathChars => new char[]
        {
            '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31
        };

        /// <summary>
        /// Copied from https://github.com/dotnet/corefx/blob/387cf98c410bdca8fd195b28cbe53af578698f94/src/System.Runtime.Extensions/src/System/IO/Path.Windows.cs#L18
        /// MSBuild should support the union of invalid path chars across the supported OSes, so builds can have the same behaviour crossplatform: https://github.com/Microsoft/msbuild/issues/781#issuecomment-243942514
        /// </summary>
        internal static char[] InvalidFileNameChars => new char[]
        {
            '\"', '<', '>', '|', '\0',
            (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10,
            (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17, (char)18, (char)19, (char)20,
            (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30,
            (char)31, ':', '*', '?', '\\', '/'
        };

        private static readonly char[] Slashes = { '/', '\\' };

        /// <summary>
        /// Retrieves the MSBuild runtime cache directory
        /// </summary>
        internal static string GetCacheDirectory()
        {
            if (cacheDirectory == null)
            {
                cacheDirectory = Path.Combine(Path.GetTempPath(), String.Format(CultureInfo.CurrentUICulture, "MSBuild{0}", Process.GetCurrentProcess().Id));
            }

            return cacheDirectory;
        }

        /// <summary>
        /// Get the hex hash string for the string
        /// </summary>
        internal static string GetHexHash(string stringToHash)
        {
            return stringToHash.GetHashCode().ToString("X", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Get the hash for the assemblyPaths
        /// </summary>
        internal static int GetPathsHash(IEnumerable<string> assemblyPaths)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string path in assemblyPaths)
            {
                if (path != null)
                {
                    string directoryPath = path.Trim();
                    if (directoryPath.Length > 0)
                    {
                        DateTime lastModifiedTime;
                        if (NativeMethodsShared.GetLastWriteDirectoryUtcTime(directoryPath, out lastModifiedTime))
                        {
                            builder.Append(lastModifiedTime.Ticks);
                            builder.Append('|');
                            builder.Append(directoryPath.ToUpperInvariant());
                            builder.Append('|');
                        }
                    }
                }
            }

            return builder.ToString().GetHashCode();
        }

        /// <summary>
        /// Clears the MSBuild runtime cache
        /// </summary>
        internal static void ClearCacheDirectory()
        {
            string cacheDirectory = GetCacheDirectory();

            if (Directory.Exists(cacheDirectory))
            {
                DeleteDirectoryNoThrow(cacheDirectory, true);
            }
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
            if (fileSpec.Length > 0 && !EndsWithSlash(fileSpec))
            {
                fileSpec += Path.DirectorySeparatorChar;
            }

            return fileSpec;
        }

        /// <summary>
        /// Ensures the path does not have a leading slash.
        /// </summary>
        internal static string EnsureNoLeadingSlash(string path)
        {
            path = FixFilePath(path);
            if (path.Length > 0 && IsSlash(path[0]))
            {
                path = path.Substring(1);
            }

            return path;
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
        /// Indicates if the given character is a slash. 
        /// </summary>
        /// <param name="c"></param>
        /// <returns>true, if slash</returns>
        internal static bool IsSlash(char c)
        {
            return ((c == Path.DirectorySeparatorChar) || (c == Path.AltDirectorySeparatorChar));
        }

        /// <summary>
        /// Trims the string and removes any double quotes around it.
        /// </summary>
        internal static string TrimAndStripAnyQuotes(string path)
        {
            // Trim returns the same string if trimming isn't needed
            path = path.Trim();
            path = path.Trim(new char[] { '"' });

            return path;
        }

        /// <summary>
        /// Get the directory name of a rooted full path
        /// </summary>
        /// <param name="fullPath"></param>
        /// <returns></returns>
        internal static String GetDirectoryNameOfFullPath(String fullPath)
        {
            if (fullPath != null)
            {
                int i = fullPath.Length;
                while (i > 0 && fullPath[--i] != Path.DirectorySeparatorChar && fullPath[i] != Path.AltDirectorySeparatorChar) ;
                return FixFilePath(fullPath.Substring(0, i));
            }
            return null;
        }

        /// <summary>
        /// Compare an unsafe char buffer with a <see cref="System.String"/> to see if their contents are identical.
        /// </summary>
        /// <param name="buffer">The beginning of the char buffer.</param>
        /// <param name="len">The length of the buffer.</param>
        /// <param name="s">The string.</param>
        /// <returns>True only if the contents of <paramref name="s"/> and the first <paramref name="len"/> characters in <paramref name="buffer"/> are identical.</returns>
        private unsafe static bool AreStringsEqual(char* buffer, int len, string s)
        {
            if (len != s.Length)
            {
                return false;
            }

            foreach (char ch in s)
            {
                if (ch != *buffer++)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets the canonicalized full path of the provided path.
        /// Path.GetFullPath The pre .Net 4.6.2 implementation of Path.GetFullPath is slow and creates strings in its work. 
        /// Therefore MSBuild has its own implementation on full framework.
        /// Guidance for use: call this on all paths accepted through public entry
        /// points that need normalization. After that point, only verify the path
        /// is rooted, using ErrorUtilities.VerifyThrowPathRooted.
        /// ASSUMES INPUT IS ALREADY UNESCAPED.
        /// </summary>
        internal static string NormalizePath(string path)
        {
            ErrorUtilities.VerifyThrowArgumentLength(path, "path");

#if FEATURE_LEGACY_GETFULLPATH

            if (NativeMethodsShared.IsWindows)
            {
                int errorCode = 0; // 0 == success in Win32

#if _DEBUG
                // Just to make sure and exercise the code that sets the correct buffer size
                // we'll start out with it deliberately too small
                int lenDir = 1;
#else
                int lenDir = MaxPath;
#endif
                unsafe
                {
                    char* finalBuffer = stackalloc char[lenDir + 1]; // One extra for the null terminator

                    int length = NativeMethodsShared.GetFullPathName(path, lenDir + 1, finalBuffer, IntPtr.Zero);
                    errorCode = Marshal.GetLastWin32Error();

                    // If the length returned from GetFullPathName is greater than the length of the buffer we've
                    // allocated, then reallocate the buffer with the correct size, and repeat the call
                    if (length > lenDir)
                    {
                        lenDir = length;
                        char* tempBuffer = stackalloc char[lenDir];
                        finalBuffer = tempBuffer;
                        length = NativeMethodsShared.GetFullPathName(path, lenDir, finalBuffer, IntPtr.Zero);
                        errorCode = Marshal.GetLastWin32Error();
                        // If we find that the length returned from GetFullPathName is longer than the buffer capacity, then
                        // something very strange is going on!
                        ErrorUtilities.VerifyThrow(
                            length <= lenDir,
                            "Final buffer capacity should be sufficient for full path name and null terminator.");
                    }

                    if (length > 0)
                    {
                        // In order to prevent people from taking advantage of our ability to extend beyond MaxPath
                        // since it is unlikely that the CLR fix will be a complete removal of maxpath madness
                        // we reluctantly have to restrict things here.
                        if (length >= MaxPath)
                        {
                            throw new PathTooLongException();
                        }

                        // Avoid creating new strings unnecessarily
                        string finalFullPath = AreStringsEqual(finalBuffer, length, path)
                            ? path
                            : new string(
                                finalBuffer,
                                startIndex: 0,
                                length: length);

                        // We really don't care about extensions here, but Path.HasExtension provides a great way to
                        // invoke the CLR's invalid path checks (these are independent of path length)
                        Path.HasExtension(finalFullPath);

                        if (finalFullPath.StartsWith(@"\\", StringComparison.Ordinal))
                        {
                            // If we detect we are a UNC path then we need to use the regular get full path in order to do the correct checks for UNC formatting
                            // and security checks for strings like \\?\GlobalRoot
                            int startIndex = 2;
                            while (startIndex < finalFullPath.Length)
                            {
                                if (finalFullPath[startIndex] == '\\')
                                {
                                    startIndex++;
                                    break;
                                }
                                else
                                {
                                    startIndex++;
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
                    if (startIndex == finalFullPath.Length || finalFullPath.IndexOf(@"\\?\globalroot", PathComparison) != -1)
                    {
                        finalFullPath = Path.GetFullPath(finalFullPath);
                    }
                }

                        return finalFullPath;
                    }
                }

                NativeMethodsShared.ThrowExceptionForErrorCode(errorCode);
                return null;
            }
#endif
            return FixFilePath(Path.GetFullPath(path));

        }

        internal static string FixFilePath(string path)
        {
            return string.IsNullOrEmpty(path) || Path.DirectorySeparatorChar == '\\' ? path : path.Replace('\\', '/');//.Replace("//", "/");
        }

        /// <summary>
        /// If on Unix, convert backslashes to slashes for strings that resemble paths.
        /// The heuristic is if something resembles paths (contains slashes) check if the
        /// first segment exists and is a directory.
        /// Use a native shared method to massage file path. If the file is adjusted,
        /// that qualifies is as a path.
        /// </summary>
        internal static string MaybeAdjustFilePath(string value)
        {
            // Don't bother with arrays or properties or network paths, or those that
            // have no slashes.
            if (NativeMethodsShared.IsWindows || string.IsNullOrEmpty(value) ||
                value.StartsWith("$(") || value.StartsWith("@(") || value.StartsWith("\\\\") ||
                value.IndexOfAny(Slashes) == -1)
            {
                return value;
            }

            // For Unix-like systems, we may want to convert backslashes to slashes
            string newValue = Regex.Replace(value, @"[\\/]+", "/");

            string quote = string.Empty;
            // Find the part of the name we want to check, that is remove quotes, if present
            string checkValue = newValue;
            if (newValue.Length > 2)
            {
                if (newValue.StartsWith("'"))
                {
                    if (newValue.EndsWith("'"))
                    {
                        checkValue = newValue.Substring(1, newValue.Length - 2);
                        quote = "'";
                    }
                }
                else if (newValue.StartsWith("\"") && newValue.EndsWith("\""))
                {
                    checkValue = newValue.Substring(1, newValue.Length - 2);
                    quote = "\"";
                }
            }

            return LooksLikeUnixFilePath(checkValue) ? newValue : value;
        }

        /// <summary>
        /// If on Unix, check if the string looks like a file path.
        /// The heuristic is if something resembles paths (contains slashes) check if the
        /// first segment exists and is a directory.
        /// </summary>
        internal static bool LooksLikeUnixFilePath(string value)
        {
            if (!NativeMethodsShared.IsUnixLike)
            {
                return false;
            }

            var firstSlash = value.IndexOf('/');

            // The first slash will either be at the beginning of the string or after the first directory name 
            if (firstSlash == 0)
            {
                firstSlash = value.Substring(1).IndexOf('/') + 1;
            }

            if (firstSlash > 0 && Directory.Exists(value.Substring(0, firstSlash)))
            {
                return true;
            }

            // Check for actual files or directories under / that get missed by the above logic
            if (firstSlash == 0 && value[0] == '/' && (Directory.Exists(value) || File.Exists(value)))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Extracts the directory from the given file-spec.
        /// </summary>
        /// <param name="fileSpec">The filespec.</param>
        /// <returns>directory path</returns>
        internal static string GetDirectory(string fileSpec)
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

        /// <summary>
        /// Determines whether the given assembly file name has one of the listed extensions.
        /// </summary>
        /// <param name="fileName">The name of the file</param>
        /// <param name="allowedExtensions">Array of extensions to consider.</param>
        /// <returns></returns>
        internal static bool HasExtension(string fileName, string[] allowedExtensions)
        {
            string fileExtension = Path.GetExtension(fileName);
            foreach (string extension in allowedExtensions)
            {
                if (String.Compare(fileExtension, extension, StringComparison.CurrentCultureIgnoreCase) == 0)
                {
                    return true;
                }
            }
            return false;
        }

        // ISO 8601 Universal time with sortable format
        internal const string FileTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

        /// <summary>
        /// Get the currently executing assembly path
        /// </summary>
        internal static string ExecutingAssemblyPath => Path.GetFullPath(AssemblyUtilities.GetAssemblyLocation(typeof(FileUtilities).GetTypeInfo().Assembly));


        /// <summary>
        /// Determines the full path for the given file-spec.
        /// ASSUMES INPUT IS STILL ESCAPED
        /// </summary>
        /// <param name="fileSpec">The file spec to get the full path of.</param>
        /// <param name="currentDirectory"></param>
        /// <returns>full path</returns>
        internal static string GetFullPath(string fileSpec, string currentDirectory)
        {
            // Sending data out of the engine into the filesystem, so time to unescape.
            fileSpec = FixFilePath(EscapingUtilities.UnescapeAll(fileSpec));

            // Data coming back from the filesystem into the engine, so time to escape it back.
            string fullPath = EscapingUtilities.Escape(NormalizePath(Path.Combine(currentDirectory, fileSpec)));

            if (NativeMethodsShared.IsWindows && !EndsWithSlash(fullPath))
            {
                Match drive = FileUtilitiesRegex.DrivePattern.Match(fileSpec);
                Match UNCShare = FileUtilitiesRegex.UNCPattern.Match(fullPath);

                if ((drive.Success && (drive.Length == fileSpec.Length)) ||
                    (UNCShare.Success && (UNCShare.Length == fullPath.Length)))
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
        internal static string GetFullPathNoThrow(string path)
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
        /// Compare if two paths, relative to the given currentDirectory are equal.
        /// Does not throw IO exceptions. See <see cref="GetFullPathNoThrow(string)"/>
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <param name="currentDirectory"></param>
        /// <returns></returns>
        internal static bool ComparePathsNoThrow(string first, string second, string currentDirectory)
        {
            // perf: try comparing the bare strings first
            if (string.Equals(first, second, PathComparison))
            {
                return true;
            }

            var firstFullPath = NormalizePathForComparisonNoThrow(first, currentDirectory);
            var secondFullPath = NormalizePathForComparisonNoThrow(second, currentDirectory);

            return string.Equals(firstFullPath, secondFullPath, PathComparison);
        }

        /// <summary>
        /// Normalizes a path for path comparison
        /// Does not throw IO exceptions. See <see cref="GetFullPathNoThrow(string)"/>
        /// 
        /// </summary>
        internal static string NormalizePathForComparisonNoThrow(string path, string currentDirectory)
        {
            // file is invalid, return early to avoid triggering an exception
            if (PathIsInvalid(path))
            {
                return path;
            }

            var normalizedPath = path.NormalizeForPathComparison();
            var fullPath = GetFullPathNoThrow(Path.Combine(currentDirectory, normalizedPath));

            return fullPath;
        }

        internal static bool PathIsInvalid(string path)
        {
            if (path.IndexOfAny(InvalidPathChars) >= 0)
            {
                return true;
            }

            var filename = GetFileName(path);

            return filename.IndexOfAny(InvalidFileNameChars) >= 0;
        }

        // Path.GetFileName does not react well to malformed filenames.
        // For example, Path.GetFileName("a/b/foo:bar") returns bar instead of foo:bar
        // It also throws exceptions on illegal path characters
        private static string GetFileName(string path)
        {
            var lastDirectorySeparator = path.LastIndexOfAny(Slashes);
            return lastDirectorySeparator >= 0 ? path.Substring(lastDirectorySeparator + 1) : path;
        }

        /// <summary>
        /// A variation on File.Delete that will throw ExceptionHandling.NotExpectedException exceptions
        /// </summary>
        internal static void DeleteNoThrow(string path)
        {
            try
            {
                File.Delete(FixFilePath(path));
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
            }
        }

        /// <summary>
        /// A variation on Directory.Delete that will throw ExceptionHandling.NotExpectedException exceptions
        /// </summary>
        [SuppressMessage("Microsoft.Usage", "CA1806:DoNotIgnoreMethodResults", MessageId = "System.Int32.TryParse(System.String,System.Int32@)", Justification = "We expect the out value to be 0 if the parse fails and compensate accordingly")]
        internal static void DeleteDirectoryNoThrow(string path, bool recursive)
        {
            int retryCount;
            int retryTimeOut;

            // Try parse will set the out parameter to 0 if the string passed in is null, or is outside the range of an int.
            if (!int.TryParse(Environment.GetEnvironmentVariable("MSBUILDDIRECTORYDELETERETRYCOUNT"), out retryCount))
            {
                retryCount = 0;
            }

            if (!int.TryParse(Environment.GetEnvironmentVariable("MSBUILDDIRECTORYDELETRETRYTIMEOUT"), out retryTimeOut))
            {
                retryTimeOut = 0;
            }

            retryCount = retryCount < 1 ? 2 : retryCount;
            retryTimeOut = retryTimeOut < 1 ? 500 : retryTimeOut;

            path = FixFilePath(path);

            for (int i = 0; i < retryCount; i++)
            {
                try
                {
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, recursive);
                        break;
                    }
                }
                catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                {
                }

                if (i + 1 < retryCount) // should not wait for the final iteration since we not gonna check anyway
                {
                    Thread.Sleep(retryTimeOut);
                }
            }
        }

        /// <summary>
        /// Deletes a directory, ensuring that Directory.Delete does not get a path ending in a slash.
        /// </summary>
        /// <remarks>
        /// This is a workaround for https://github.com/dotnet/corefx/issues/3780, which clashed with a common
        /// pattern in our tests.
        /// </remarks>
        internal static void DeleteWithoutTrailingBackslash(string path, bool recursive = false)
        {
            Directory.Delete(EnsureNoTrailingSlash(path), recursive);
        }

        /// <summary>
        /// A variation of Path.IsRooted that not throw any IO exception.
        /// </summary>
        internal static bool IsRootedNoThrow(string path)
        {
            bool result;

            try
            {
                result = Path.IsPathRooted(FixFilePath(path));
            }
            catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
            {
                result = false;
            }

            return result;
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
        internal static FileInfo GetFileInfoNoThrow(string filePath)
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
        /// Returns if the directory exists
        /// </summary>
        /// <param name="fullPath">Full path to the directory in the filesystem</param>
        /// <returns></returns>
        internal static bool DirectoryExistsNoThrow(string fullPath)
        {
            fullPath = AttemptToShortenPath(fullPath);
            if (NativeMethodsShared.IsWindows)
            {
                NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA data = new NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA();
                bool success = false;

                success = NativeMethodsShared.GetFileAttributesEx(fullPath, 0, ref data);
                if (success)
                {
                    return ((data.fileAttributes & NativeMethodsShared.FILE_ATTRIBUTE_DIRECTORY) != 0);
                }

                return false;
            }
            else
            {
                try
                {
                    return Directory.Exists(fullPath);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// Returns if the directory exists
        /// </summary>
        /// <param name="fullPath">Full path to the file in the filesystem</param>
        /// <returns></returns>
        internal static bool FileExistsNoThrow(string fullPath)
        {
            fullPath = AttemptToShortenPath(fullPath);
            if (NativeMethodsShared.IsWindows)
            {
                NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA data = new NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA();
                bool success = false;

                success = NativeMethodsShared.GetFileAttributesEx(fullPath, 0, ref data);
                if (success)
                {
                    return ((data.fileAttributes & NativeMethodsShared.FILE_ATTRIBUTE_DIRECTORY) == 0);
                }

                return false;
            }

            try
            {
                return File.Exists(fullPath);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// If there is a directory or file at the specified path, returns true.
        /// Otherwise, returns false.
        /// Does not throw IO exceptions, to match Directory.Exists and File.Exists.
        /// Unlike calling each of those in turn it only accesses the disk once, which is faster.
        /// </summary>
        internal static bool FileOrDirectoryExistsNoThrow(string fullPath)
        {
            fullPath = AttemptToShortenPath(fullPath);
            if (NativeMethodsShared.IsWindows)
            {
                NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA data = new NativeMethodsShared.WIN32_FILE_ATTRIBUTE_DATA();
                bool success = false;

                success = NativeMethodsShared.GetFileAttributesEx(fullPath, 0, ref data);

                return success;
            }
            else
            {
                try
                {
                    return File.Exists(fullPath) || Directory.Exists(fullPath);
                }
                catch
                {
                    return false;
                }
            }
        }

        /// <summary>
        /// This method returns true if the specified filename is a solution file (.sln), otherwise
        /// it returns false.
        /// </summary>
        internal static bool IsSolutionFilename(string filename)
        {
            return (String.Equals(Path.GetExtension(filename), ".sln", PathComparison));
        }

        /// <summary>
        /// Returns true if the specified filename is a VC++ project file, otherwise returns false
        /// </summary>
        internal static bool IsVCProjFilename(string filename)
        {
            return (String.Equals(Path.GetExtension(filename), ".vcproj", PathComparison));
        }

        /// <summary>
        /// Returns true if the specified filename is a metaproject file (.metaproj), otherwise false.
        /// </summary>
        internal static bool IsMetaprojectFilename(string filename)
        {
            return (String.Equals(Path.GetExtension(filename), ".metaproj", PathComparison));
        }

        internal static bool IsBinaryLogFilename(string filename)
        {
            return (String.Equals(Path.GetExtension(filename), ".binlog", PathComparison));
        }

        /// <summary>
        /// Given the absolute location of a file, and a disc location, returns relative file path to that disk location. 
        /// Throws UriFormatException.
        /// </summary>
        /// <param name="basePath">
        /// The base path we want to relativize to. Must be absolute.  
        /// Should <i>not</i> include a filename as the last segment will be interpreted as a directory.
        /// </param>
        /// <param name="path">
        /// The path we need to make relative to basePath.  The path can be either absolute path or a relative path in which case it is relative to the base path.
        /// If the path cannot be made relative to the base path (for example, it is on another drive), it is returned verbatim.
        /// If the basePath is an empty string, returns the path.
        /// </param>
        /// <returns>relative path (can be the full path)</returns>
        internal static string MakeRelative(string basePath, string path)
        {
            ErrorUtilities.VerifyThrowArgumentNull(basePath, "basePath");
            ErrorUtilities.VerifyThrowArgumentLength(path, "path");

            if (basePath.Length == 0)
            {
                return path;
            }

            Uri baseUri = new Uri(EnsureTrailingSlash(basePath), UriKind.Absolute); // May throw UriFormatException

            Uri pathUri = CreateUriFromPath(path);

            if (!pathUri.IsAbsoluteUri)
            {
                // the path is already a relative url, we will just normalize it...
                pathUri = new Uri(baseUri, pathUri);
            }

            Uri relativeUri = baseUri.MakeRelativeUri(pathUri);
            string relativePath = Uri.UnescapeDataString(relativeUri.IsAbsoluteUri ? relativeUri.LocalPath : relativeUri.ToString());

            string result = relativePath.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);

            return result;
        }

        /// <summary>
        /// Helper function to create an Uri object from path.
        /// </summary>
        /// <param name="path">path string</param>
        /// <returns>uri object</returns>
        private static Uri CreateUriFromPath(string path)
        {
            ErrorUtilities.VerifyThrowArgumentLength(path, "path");

            Uri pathUri = null;

            // Try absolute first, then fall back on relative, otherwise it
            // makes some absolute UNC paths like (\\foo\bar) relative ...
            if (!Uri.TryCreate(path, UriKind.Absolute, out pathUri))
            {
                pathUri = new Uri(path, UriKind.Relative);
            }

            return pathUri;
        }

        /// <summary>
        /// Normalizes the path if and only if it is longer than max path,
        /// or would be if rooted by the current directory.
        /// This may make it shorter by removing ".."'s.
        /// </summary>
        internal static string AttemptToShortenPath(string path)
        {
            // >= not > because MAX_PATH assumes a trailing null
            if (path.Length >= NativeMethodsShared.MAX_PATH ||
               (!IsRootedNoThrow(path) && ((Directory.GetCurrentDirectory().Length + path.Length + 1 /* slash */) >= NativeMethodsShared.MAX_PATH)))
            {
                // Attempt to make it shorter -- perhaps there are some \..\ elements
                path = GetFullPathNoThrow(path);
            }

            return FixFilePath(path);
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
                return path;

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
            ErrorUtilities.VerifyThrowArgumentNull(root, nameof(root));
            ErrorUtilities.VerifyThrowArgumentNull(paths, nameof(paths));

            return paths.Aggregate(root, Path.Combine);
        }

        internal static string TrimTrailingSlashes(this string s)
        {
            return s.TrimEnd('/', '\\');
        }

        /// <summary>
        /// Replace all backward slashes to forward slashes
        /// </summary>
        internal static string ToSlash(this string s)
        {
            return s.Replace('\\', '/');
        }

        internal static string WithTrailingSlash(this string s)
        {
            return EnsureTrailingSlash(s);
        }

        internal static string NormalizeForPathComparison(this string s) => s.ToSlash().TrimTrailingSlashes();

        internal static bool PathsEqual(string path1, string path2)
        {
            var trim1 = path1.TrimTrailingSlashes();
            var trim2 = path2.TrimTrailingSlashes();

            if (string.Equals(trim1, trim2, PathComparison))
            {
                return true;
            }

            var slash1 = trim1.ToSlash();
            var slash2 = trim2.ToSlash();

            return string.Equals(slash1, slash2, PathComparison);
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

        internal static StreamReader OpenRead(string path, Encoding encoding = null, bool detectEncodingFromByteOrderMarks = true)
        {
            const int DefaultFileStreamBufferSize = 4096;
            Stream fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, DefaultFileStreamBufferSize, FileOptions.SequentialScan);
            if (encoding == null)
            {
                return new StreamReader(fileStream);
            }
            else
            {
                return new StreamReader(fileStream, encoding, detectEncodingFromByteOrderMarks);
            }
        }
    }
}
