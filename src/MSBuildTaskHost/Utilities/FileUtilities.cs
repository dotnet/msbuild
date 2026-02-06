// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.TaskHost.Resources;

namespace Microsoft.Build.TaskHost.Utilities;

/// <summary>
/// This class contains utility methods for file IO.
/// PERF\COVERAGE NOTE: Try to keep classes in 'shared' as granular as possible. All the methods in
/// each class get pulled into the resulting assembly.
/// </summary>
internal static partial class FileUtilities
{
    private const char BackSlash = '\\';
    private const char ForwardSlash = '/';

    // ISO 8601 Universal time with sortable format
    private const string FileTimeFormat = "yyyy'-'MM'-'dd HH':'mm':'ss'.'fffffff";

    public static string TempFileDirectory => Path.GetTempPath();

    public static string MSBuildTaskHostDirectory
        => field ??= Path.GetDirectoryName(Path.GetFullPath(typeof(FileUtilities).Assembly.Location));

    /// <summary>
    /// Indicates if the given character is a slash.
    /// </summary>
    /// <param name="c"></param>
    /// <returns>true, if slash</returns>
    private static bool IsAnySlash(char c)
        => c is BackSlash or ForwardSlash;

    /// <summary>
    /// Indicates if the given file-spec ends with a slash.
    /// </summary>
    /// <param name="fileSpec">The file spec.</param>
    /// <returns>true, if file-spec has trailing slash</returns>
    private static bool EndsWithSlash(string fileSpec)
        => fileSpec.Length > 0 && IsAnySlash(fileSpec[fileSpec.Length - 1]);

    /// <summary>
    /// Indicates whether the specified string follows the pattern drive pattern (for example "C:", "D:").
    /// </summary>
    /// <param name="pattern">Input to check for drive pattern.</param>
    /// <returns>true if follows the drive pattern, false otherwise.</returns>
    private static bool IsDrivePattern(string pattern)
        => pattern.Length == 2 && StartsWithDrivePattern(pattern); // Format must be two characters long: "<drive letter>:"

    /// <summary>
    /// Indicates whether the specified string follows the pattern drive pattern (for example "C:/" or "C:\").
    /// </summary>
    /// <param name="pattern">Input to check for drive pattern with slash.</param>
    /// <returns>true if follows the drive pattern with slash, false otherwise.</returns>
    private static bool IsDrivePatternWithSlash(string pattern)
        => pattern.Length == 3 && StartsWithDrivePatternWithSlash(pattern);

    /// <summary>
    /// Indicates whether the specified string starts with the drive pattern (for example "C:").
    /// </summary>
    /// <param name="pattern">Input to check for drive pattern.</param>
    /// <returns>true if starts with drive pattern, false otherwise.</returns>
    private static bool StartsWithDrivePattern(string pattern)
        // Format dictates a length of at least 2,
        // first character must be a letter,
        // second character must be a ":"
        => pattern.Length >= 2 &&
          (pattern[0] is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z')) &&
           pattern[1] == ':';

    /// <summary>
    /// Indicates whether the specified string starts with the drive pattern (for example "C:/" or "C:\").
    /// </summary>
    /// <param name="pattern">Input to check for drive pattern.</param>
    /// <returns>true if starts with drive pattern with slash, false otherwise.</returns>
    private static bool StartsWithDrivePatternWithSlash(string pattern)
        // Format dictates a length of at least 3,
        // first character must be a letter,
        // second character must be a ":"
        // third character must be a slash.
        => pattern.Length >= 3 &&
            StartsWithDrivePattern(pattern) &&
            pattern[2] is BackSlash or ForwardSlash;

    /// <summary>
    /// Indicates whether the specified file-spec comprises exactly "\\server\share" (with no trailing characters).
    /// </summary>
    /// <param name="pattern">Input to check for UNC pattern.</param>
    /// <returns>true if comprises UNC pattern.</returns>
    private static bool IsUncPattern(string pattern)
        // Return value == pattern.length means:
        //  meets minimum unc requirements
        //  pattern does not end in a '/' or '\'
        //  if a subfolder were found the value returned would be length up to that subfolder, therefore no subfolder exists
        => StartsWithUncPatternMatchLength(pattern) == pattern.Length;

    /// <summary>
    /// Indicates whether the specified file-spec begins with "\\server\share".
    /// </summary>
    /// <param name="pattern">Input to check for UNC pattern.</param>
    /// <returns>true if starts with UNC pattern.</returns>
    private static bool StartsWithUncPattern(string pattern)
        // Any non -1 value returned means there was a match, therefore is begins with the pattern.
        => StartsWithUncPatternMatchLength(pattern) != -1;

    /// <summary>
    /// Indicates whether the file-spec begins with a UNC pattern and how long the match is.
    /// </summary>
    /// <param name="pattern">Input to check for UNC pattern.</param>
    /// <returns>length of the match, -1 if no match.</returns>
    private static int StartsWithUncPatternMatchLength(string pattern)
    {
        if (!MeetsUncPatternMinimumRequirements(pattern))
        {
            return -1;
        }

        bool prevCharWasSlash = true;
        bool hasShare = false;

        for (int i = 2; i < pattern.Length; i++)
        {
            // Real UNC paths should only contain backslashes. However, the previous
            // regex pattern accepted both so functionality will be retained.
            if (pattern[i] is BackSlash or ForwardSlash)
            {
                if (prevCharWasSlash)
                {
                    // We get here in the case of an extra slash.
                    return -1;
                }
                else if (hasShare)
                {
                    return i;
                }

                hasShare = true;
                prevCharWasSlash = true;
            }
            else
            {
                prevCharWasSlash = false;
            }
        }

        if (!hasShare)
        {
            // no subfolder means no unc pattern. string is something like "\\abc" in this case
            return -1;
        }

        return pattern.Length;
    }

    /// <summary>
    /// Indicates whether or not the file-spec meets the minimum requirements of a UNC pattern.
    /// </summary>
    /// <param name="pattern">Input to check for UNC pattern minimum requirements.</param>
    /// <returns>true if the UNC pattern is a minimum length of 5 and the first two characters are be a slash, false otherwise.</returns>
    private static bool MeetsUncPatternMinimumRequirements(string pattern)
        => pattern.Length >= 5 &&
           pattern[0] is BackSlash or ForwardSlash &&
           pattern[1] is BackSlash or ForwardSlash;

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
        string uncheckedFullPath = NativeMethods.GetFullPath(path);

        if (IsPathTooLong(uncheckedFullPath))
        {
            string message = string.Format(SR.Shared_PathTooLong, path, NativeMethods.MaxPath);
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
        string directory = Path.GetDirectoryName(fileSpec);

        // if file-spec is a root directory e.g. c:, c:\, \, \\server\share
        // NOTE: Path.GetDirectoryName also treats invalid UNC file-specs as root directories e.g. \\, \\server
        if (directory == null)
        {
            // just use the file-spec as-is
            directory = fileSpec;
        }

        if (directory.Length > 0 && !EndsWithSlash(directory))
        {
            // restore trailing slash if Path.GetDirectoryName has removed it (this happens with non-root directories)
            directory += Path.DirectorySeparatorChar;
        }

        return directory;
    }

    /// <summary>
    /// Determines the full path for the given file-spec.
    /// ASSUMES INPUT IS STILL ESCAPED.
    /// </summary>
    /// <param name="fileSpec">The file spec to get the full path of.</param>
    /// <param name="currentDirectory"></param>
    /// <param name="escape">Whether to escape the path after getting the full path.</param>
    /// <returns>Full path to the file, escaped if not specified otherwise.</returns>
    private static string GetFullPath(string fileSpec, string currentDirectory, bool escape = true)
    {
        // Sending data out of the engine into the filesystem, so time to unescape.
        fileSpec = EscapingUtilities.UnescapeAll(fileSpec);

        string fullPath = NormalizePath(Path.Combine(currentDirectory, fileSpec));

        // In some cases we might want to NOT escape in order to preserve symbols like @, %, $ etc.
        if (escape)
        {
            // Data coming back from the filesystem into the engine, so time to escape it back.
            fullPath = EscapingUtilities.Escape(fullPath);
        }

        if (!EndsWithSlash(fullPath))
        {
            if (IsDrivePattern(fileSpec) || IsUncPattern(fullPath))
            {
                // append trailing slash if Path.GetFullPath failed to (this happens with drive-specs and UNC shares)
                fullPath += Path.DirectorySeparatorChar;
            }
        }

        return fullPath;
    }

    private static bool IsPathTooLong(string path)
        => path.Length >= NativeMethods.MaxPath; // >= not > because MAX_PATH assumes a trailing null

    internal static StreamWriter CreateWriterForAppend(string path)
    {
        const int DefaultFileStreamBufferSize = 4096;

        var fileStream = new FileStream(
            path,
            mode: FileMode.Append,
            access: FileAccess.Write,
            share: FileShare.Read,
            bufferSize: DefaultFileStreamBufferSize,
            options: FileOptions.SequentialScan);

        return new StreamWriter(fileStream);
    }
}
