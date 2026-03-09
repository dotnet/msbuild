// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NET
using System.Buffers;
#endif
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared.FileSystem;

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
        /// <inheritdoc cref="FrameworkFileUtilities.ClearCacheDirectoryPath"/>
        internal static void ClearCacheDirectoryPath()
            => FrameworkFileUtilities.ClearCacheDirectoryPath();

        /// <inheritdoc cref="FrameworkFileUtilities.PathComparison"/>
        internal static StringComparison PathComparison
            => FrameworkFileUtilities.PathComparison;

        /// <inheritdoc cref="FrameworkFileUtilities.PathComparer"/>
        internal static StringComparer PathComparer
            => FrameworkFileUtilities.PathComparer;

        /// <inheritdoc cref="FrameworkFileUtilities.GetIsFileSystemCaseSensitive"/>
        public static bool GetIsFileSystemCaseSensitive()
            => FrameworkFileUtilities.GetIsFileSystemCaseSensitive();

        /// <inheritdoc cref="FrameworkFileUtilities.InvalidPathChars"/>
#if NET
        internal static SearchValues<char> InvalidPathChars
#else
        internal static char[] InvalidPathChars
#endif
            => FrameworkFileUtilities.InvalidPathChars;

        /// <inheritdoc cref="FrameworkFileUtilities.InvalidFileNameCharsArray"/>
        internal static char[] InvalidFileNameCharsArray
            => FrameworkFileUtilities.InvalidFileNameCharsArray;

        /// <inheritdoc cref="FrameworkFileUtilities.InvalidFileNameChars"/>
#if NET
        internal static SearchValues<char> InvalidFileNameChars
#else
        internal static char[] InvalidFileNameChars
#endif
            => FrameworkFileUtilities.InvalidFileNameChars;

        /// <inheritdoc cref="FrameworkFileUtilities.DirectorySeparatorString"/>
        internal static string DirectorySeparatorString
            => FrameworkFileUtilities.DirectorySeparatorString;

        /// <inheritdoc cref="FrameworkFileUtilities.GetCacheDirectory"/>
        internal static string GetCacheDirectory()
            => FrameworkFileUtilities.GetCacheDirectory();

        /// <inheritdoc cref="FrameworkFileUtilities.GetHexHash(string)"/>
        internal static string GetHexHash(string stringToHash)
            => FrameworkFileUtilities.GetHexHash(stringToHash);

        /// <inheritdoc cref="FrameworkFileUtilities.GetPathsHash(IEnumerable{string})"/>
        internal static int GetPathsHash(IEnumerable<string> assemblyPaths)
            => FrameworkFileUtilities.GetPathsHash(assemblyPaths);

        /// <inheritdoc cref="FrameworkFileUtilities.CanWriteToDirectory(string)"/>
        internal static bool CanWriteToDirectory(string directory)
            => FrameworkFileUtilities.CanWriteToDirectory(directory);

        /// <inheritdoc cref="FrameworkFileUtilities.ClearCacheDirectory"/>
        internal static void ClearCacheDirectory()
            => FrameworkFileUtilities.ClearCacheDirectory();

        /// <inheritdoc cref="FrameworkFileUtilities.EnsureNoLeadingOrTrailingSlash(string, int)"/>
        internal static string EnsureNoLeadingOrTrailingSlash(string path, int start)
            => FrameworkFileUtilities.EnsureNoLeadingOrTrailingSlash(path, start);

        /// <inheritdoc cref="FrameworkFileUtilities.EnsureTrailingNoLeadingSlash(string, int)"/>
        internal static string EnsureTrailingNoLeadingSlash(string path, int start)
            => FrameworkFileUtilities.EnsureTrailingNoLeadingSlash(path, start);

        /// <inheritdoc cref="FrameworkFileUtilities.EnsureSingleQuotes(string)"/>
        internal static string EnsureSingleQuotes(string path)
            => FrameworkFileUtilities.EnsureSingleQuotes(path);

        /// <inheritdoc cref="FrameworkFileUtilities.EnsureDoubleQuotes(string)"/>
        internal static string EnsureDoubleQuotes(string path)
            => FrameworkFileUtilities.EnsureDoubleQuotes(path);

        /// <inheritdoc cref="FrameworkFileUtilities.EnsureQuotes(string, bool)"/>
        internal static string EnsureQuotes(string path, bool isSingleQuote = true)
            => FrameworkFileUtilities.EnsureQuotes(path, isSingleQuote);

        /// <inheritdoc cref="FrameworkFileUtilities.TrimAndStripAnyQuotes(string)"/>
        internal static string TrimAndStripAnyQuotes(string path)
            => FrameworkFileUtilities.TrimAndStripAnyQuotes(path);

        /// <inheritdoc cref="FrameworkFileUtilities.GetDirectoryNameOfFullPath(string)"/>
        internal static string GetDirectoryNameOfFullPath(string fullPath)
            => FrameworkFileUtilities.GetDirectoryNameOfFullPath(fullPath);

        /// <inheritdoc cref="FrameworkFileUtilities.TruncatePathToTrailingSegments(string, int)"/>
        internal static string TruncatePathToTrailingSegments(string path, int trailingSegmentsToKeep)
            => FrameworkFileUtilities.TruncatePathToTrailingSegments(path, trailingSegmentsToKeep);

        /// <inheritdoc cref="FrameworkFileUtilities.ContainsRelativePathSegments(string)"/>
        internal static bool ContainsRelativePathSegments(string path)
            => FrameworkFileUtilities.ContainsRelativePathSegments(path);

        /// <inheritdoc cref="FrameworkFileUtilities.NormalizePath(string)"/>
        internal static string NormalizePath(string path)
            => FrameworkFileUtilities.NormalizePath(path);

        /// <inheritdoc cref="FrameworkFileUtilities.NormalizePath(string, string)"/>
        internal static string NormalizePath(string directory, string file)
            => FrameworkFileUtilities.NormalizePath(directory, file);

        /// <inheritdoc cref="FrameworkFileUtilities.NormalizePath(string[])"/>
        internal static string NormalizePath(params string[] paths)
            => FrameworkFileUtilities.NormalizePath(paths);

        /// <inheritdoc cref="FrameworkFileUtilities.NormalizePathSeparatorsToForwardSlash(string)"/>
        internal static string NormalizePathSeparatorsToForwardSlash(string path)
            => FrameworkFileUtilities.NormalizePathSeparatorsToForwardSlash(path);

        /// <inheritdoc cref="FrameworkFileUtilities.MaybeAdjustFilePath(string, string)"/>
        internal static string MaybeAdjustFilePath(string value, string baseDirectory = "")
            => FrameworkFileUtilities.MaybeAdjustFilePath(value, baseDirectory);

        /// <inheritdoc cref="FrameworkFileUtilities.MaybeAdjustFilePath(ReadOnlyMemory{char}, string)"/>
        internal static ReadOnlyMemory<char> MaybeAdjustFilePath(ReadOnlyMemory<char> value, string baseDirectory = "")
            => FrameworkFileUtilities.MaybeAdjustFilePath(value, baseDirectory);

        /// <inheritdoc cref="FrameworkFileUtilities.IsAnySlash"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsAnySlash(char c)
            => FrameworkFileUtilities.IsAnySlash(c);

        /// <inheritdoc cref="FrameworkFileUtilities.LooksLikeUnixFilePath(string, string)"/>
        internal static bool LooksLikeUnixFilePath(string value, string baseDirectory = "")
            => FrameworkFileUtilities.LooksLikeUnixFilePath(value, baseDirectory);

        /// <inheritdoc cref="FrameworkFileUtilities.LooksLikeUnixFilePath(ReadOnlySpan{char}, string)"/>
        internal static bool LooksLikeUnixFilePath(ReadOnlySpan<char> value, string baseDirectory = "")
            => FrameworkFileUtilities.LooksLikeUnixFilePath(value, baseDirectory);

        /// <inheritdoc cref="FrameworkFileUtilities.GetDirectory(string)"/>
        internal static string GetDirectory(string fileSpec)
            => FrameworkFileUtilities.GetDirectory(fileSpec);

        /// <inheritdoc cref="FrameworkFileUtilities.DeleteSubdirectoriesNoThrow(string)"/>
        internal static void DeleteSubdirectoriesNoThrow(string directory)
            => FrameworkFileUtilities.DeleteSubdirectoriesNoThrow(directory);

        /// <inheritdoc cref="FrameworkFileUtilities.HasExtension(string, string[])"/>
        internal static bool HasExtension(string fileName, string[] allowedExtensions)
            => FrameworkFileUtilities.HasExtension(fileName, allowedExtensions);

        /// <inheritdoc cref="FrameworkFileUtilities.FileTimeFormat"/>
        internal static string FileTimeFormat
            => FrameworkFileUtilities.FileTimeFormat;

        /// <summary>
        /// Get the currently executing assembly path.
        /// </summary>
        internal static string ExecutingAssemblyPath
            => Path.GetFullPath(AssemblyUtilities.GetAssemblyLocation(typeof(FileUtilities).GetTypeInfo().Assembly));

        /// <inheritdoc cref="FrameworkFileUtilities.GetFullPath(string, string, bool)"/>
        internal static string GetFullPath(string fileSpec, string currentDirectory, bool escape = true)
            => FrameworkFileUtilities.GetFullPath(fileSpec, currentDirectory, escape);

        /// <inheritdoc cref="FrameworkFileUtilities.GetFullPathNoThrow(string)"/>
        internal static string GetFullPathNoThrow(string path)
            => FrameworkFileUtilities.GetFullPathNoThrow(path);

        /// <inheritdoc cref="FrameworkFileUtilities.ComparePathsNoThrow(string, string, string, bool)"/>
        internal static bool ComparePathsNoThrow(string first, string second, string currentDirectory, bool alwaysIgnoreCase = false)
            => FrameworkFileUtilities.ComparePathsNoThrow(first, second, currentDirectory, alwaysIgnoreCase);

        /// <inheritdoc cref="FrameworkFileUtilities.NormalizePathForComparisonNoThrow(string, string)"/>
        internal static string NormalizePathForComparisonNoThrow(string path, string currentDirectory)
            => FrameworkFileUtilities.NormalizePathForComparisonNoThrow(path, currentDirectory);

        /// <inheritdoc cref="FrameworkFileUtilities.PathIsInvalid(string)"/>
        internal static bool PathIsInvalid(string path)
            => FrameworkFileUtilities.PathIsInvalid(path);

        /// <inheritdoc cref="FrameworkFileUtilities.DeleteNoThrow(string)"/>
        internal static void DeleteNoThrow(string path)
            => FrameworkFileUtilities.DeleteNoThrow(path);

        /// <inheritdoc cref="FrameworkFileUtilities.DeleteDirectoryNoThrow(string, bool, int, int)"/>
        internal static void DeleteDirectoryNoThrow(string path, bool recursive, int retryCount = 0, int retryTimeOut = 0)
            => FrameworkFileUtilities.DeleteDirectoryNoThrow(path, recursive, retryCount, retryTimeOut);

        /// <inheritdoc cref="FrameworkFileUtilities.DeleteWithoutTrailingBackslash(string, bool)"/>
        internal static void DeleteWithoutTrailingBackslash(string path, bool recursive = false)
            => FrameworkFileUtilities.DeleteWithoutTrailingBackslash(path, recursive);

        /// <inheritdoc cref="FrameworkFileUtilities.GetFileInfoNoThrow(string)"/>
        internal static FileInfo GetFileInfoNoThrow(string filePath)
            => FrameworkFileUtilities.GetFileInfoNoThrow(filePath);

        /// <inheritdoc cref="FrameworkFileUtilities.DirectoryExistsNoThrow(string, IFileSystem)"/>
        internal static bool DirectoryExistsNoThrow(string fullPath, IFileSystem fileSystem = null)
            => FrameworkFileUtilities.DirectoryExistsNoThrow(fullPath, fileSystem);

        /// <inheritdoc cref="FrameworkFileUtilities.FileExistsNoThrow(string, IFileSystem)"/>
        internal static bool FileExistsNoThrow(string fullPath, IFileSystem fileSystem = null)
            => FrameworkFileUtilities.FileExistsNoThrow(fullPath, fileSystem);

        /// <inheritdoc cref="FrameworkFileUtilities.FileOrDirectoryExistsNoThrow(string, IFileSystem)"/>
        internal static bool FileOrDirectoryExistsNoThrow(string fullPath, IFileSystem fileSystem = null)
            => FrameworkFileUtilities.FileOrDirectoryExistsNoThrow(fullPath, fileSystem);

        /// <inheritdoc cref="FrameworkFileUtilities.IsSolutionFilename(string)"/>
        internal static bool IsSolutionFilename(string filename)
            => FrameworkFileUtilities.IsSolutionFilename(filename);

        /// <inheritdoc cref="FrameworkFileUtilities.IsSolutionFilterFilename(string)"/>
        internal static bool IsSolutionFilterFilename(string filename)
            => FrameworkFileUtilities.IsSolutionFilterFilename(filename);

        /// <inheritdoc cref="FrameworkFileUtilities.IsSolutionXFilename(string)"/>
        internal static bool IsSolutionXFilename(string filename)
            => FrameworkFileUtilities.IsSolutionXFilename(filename);

        /// <inheritdoc cref="FrameworkFileUtilities.IsVCProjFilename(string)"/>
        internal static bool IsVCProjFilename(string filename)
            => FrameworkFileUtilities.IsVCProjFilename(filename);

        /// <inheritdoc cref="FrameworkFileUtilities.IsDspFilename(string)"/>
        internal static bool IsDspFilename(string filename)
            => FrameworkFileUtilities.IsDspFilename(filename);

        /// <inheritdoc cref="FrameworkFileUtilities.IsMetaprojectFilename(string)"/>
        internal static bool IsMetaprojectFilename(string filename)
            => FrameworkFileUtilities.IsMetaprojectFilename(filename);

        /// <inheritdoc cref="FrameworkFileUtilities.IsBinaryLogFilename(string)"/>
        internal static bool IsBinaryLogFilename(string filename)
            => FrameworkFileUtilities.IsBinaryLogFilename(filename);

        /// <inheritdoc cref="FrameworkFileUtilities.MakeRelative(string, string)"/>
        internal static string MakeRelative(string basePath, string path)
            => FrameworkFileUtilities.MakeRelative(basePath, path);

        /// <inheritdoc cref="FrameworkFileUtilities.AttemptToShortenPath(string)"/>
        internal static string AttemptToShortenPath(string path)
            => FrameworkFileUtilities.AttemptToShortenPath(path);

        /// <inheritdoc cref="FrameworkFileUtilities.GetFolderAbove(string, int)"/>
        internal static string GetFolderAbove(string path, int count = 1)
            => FrameworkFileUtilities.GetFolderAbove(path, count);

        /// <inheritdoc cref="FrameworkFileUtilities.CombinePaths(string, string[])"/>
        internal static string CombinePaths(string root, params string[] paths)
            => FrameworkFileUtilities.CombinePaths(root, paths);

        /// <inheritdoc cref="FrameworkFileUtilities.TrimTrailingSlashes(string)"/>
        internal static string TrimTrailingSlashes(this string s)
            => FrameworkFileUtilities.TrimTrailingSlashes(s);

        /// <inheritdoc cref="FrameworkFileUtilities.ToSlash(string)"/>
        internal static string ToSlash(this string s)
            => FrameworkFileUtilities.ToSlash(s);

        /// <inheritdoc cref="FrameworkFileUtilities.ToBackslash(string)"/>
        internal static string ToBackslash(this string s)
            => FrameworkFileUtilities.ToBackslash(s);

        /// <inheritdoc cref="FrameworkFileUtilities.ToPlatformSlash(string)"/>
        internal static string ToPlatformSlash(this string s)
            => FrameworkFileUtilities.ToPlatformSlash(s);

        /// <inheritdoc cref="FrameworkFileUtilities.WithTrailingSlash(string)"/>
        internal static string WithTrailingSlash(this string s)
            => FrameworkFileUtilities.WithTrailingSlash(s);

        /// <inheritdoc cref="FrameworkFileUtilities.NormalizeForPathComparison(string)"/>
        internal static string NormalizeForPathComparison(this string s)
            => FrameworkFileUtilities.NormalizeForPathComparison(s);

        /// <inheritdoc cref="FrameworkFileUtilities.PathsEqual(string, string)"/>
        internal static bool PathsEqual(string path1, string path2)
            => FrameworkFileUtilities.PathsEqual(path1, path2);

        /// <inheritdoc cref="FrameworkFileUtilities.OpenWrite(string, bool, Encoding?)"/>
        internal static StreamWriter OpenWrite(string path, bool append, Encoding encoding = null)
            => FrameworkFileUtilities.OpenWrite(path, append, encoding);

        /// <inheritdoc cref="FrameworkFileUtilities.OpenRead(string, Encoding?, bool)"/>
        internal static StreamReader OpenRead(string path, Encoding encoding = null, bool detectEncodingFromByteOrderMarks = true)
            => FrameworkFileUtilities.OpenRead(path, encoding, detectEncodingFromByteOrderMarks);

        /// <inheritdoc cref="FrameworkFileUtilities.GetDirectoryNameOfFileAbove(string, string, IFileSystem)"/>
        internal static string GetDirectoryNameOfFileAbove(string startingDirectory, string fileName, IFileSystem fileSystem = null)
            => FrameworkFileUtilities.GetDirectoryNameOfFileAbove(startingDirectory, fileName, fileSystem);

        /// <inheritdoc cref="FrameworkFileUtilities.GetPathOfFileAbove(string, string, IFileSystem)"/>
        internal static string GetPathOfFileAbove(string file, string startingDirectory, IFileSystem fileSystem = null)
            => FrameworkFileUtilities.GetPathOfFileAbove(file, startingDirectory, fileSystem);

        /// <inheritdoc cref="FrameworkFileUtilities.EnsureDirectoryExists(string)"/>
        internal static void EnsureDirectoryExists(string directoryPath)
            => FrameworkFileUtilities.EnsureDirectoryExists(directoryPath);

        /// <inheritdoc cref="FrameworkFileUtilities.ClearFileExistenceCache"/>
        internal static void ClearFileExistenceCache()
            => FrameworkFileUtilities.ClearFileExistenceCache();

        /// <inheritdoc cref="FrameworkFileUtilities.ReadFromStream(Stream, byte[], int, int)"/>
        internal static void ReadFromStream(this Stream stream, byte[] content, int startIndex, int length)
            => FrameworkFileUtilities.ReadFromStream(stream, content, startIndex, length);
    }
}
