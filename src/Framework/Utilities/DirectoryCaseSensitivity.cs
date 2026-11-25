// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security;
#if NET
using System.Runtime.InteropServices;
#endif
#if FEATURE_WINDOWSINTEROP
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
#endif

namespace Microsoft.Build.Framework;

/// <summary>
/// Determines whether name lookups inside a directory are case-sensitive by asking the file system
/// </summary>
internal static partial class DirectoryCaseSensitivity
{
#if NET
    /// <summary>
    /// <c>_PC_CASE_SENSITIVE</c> from macOS <c>sys/unistd.h</c>.
    /// </summary>
    private const int PC_CASE_SENSITIVE = 11;
#endif

    private static readonly ConcurrentDictionary<string, bool> s_cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Returns <see langword="true"/> if lookups of entries inside <paramref name="directory"/> are case-sensitive.
    /// </summary>
    /// <param name="directory">Full path of the directory.</param>
    internal static bool IsCaseSensitive(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);

        if (s_cache.TryGetValue(directory, out bool cached))
        {
            return cached;
        }

        if (Detect(directory) is bool detected)
        {
            s_cache[directory] = detected;
            return detected;
        }

        return FileUtilities.IsFileSystemCaseSensitive;
    }

    /// <summary>
    /// Detects the case sensitivity of <paramref name="directory"/> without caching.
    /// Returns <see langword="null"/> if it cannot be determined.
    /// </summary>
    internal static bool? Detect(string directory) => QueryFileSystem(directory) ?? ProbeByLookup(directory);

    /// <summary>
    /// Asks the operating system for the case sensitivity of <paramref name="directory"/>:
    /// the per-directory flag on Windows and the per-volume setting on macOS.
    /// Returns <see langword="null"/> if there is no such API or the query fails.
    /// </summary>
    internal static bool? QueryFileSystem(string directory)
    {
#if FEATURE_WINDOWSINTEROP
        if (NativeMethods.IsWindows)
        {
            return QueryWindowsDirectoryFlag(directory);
        }
#endif
#if NET
        if (NativeMethods.IsOSX)
        {
            return QueryMacVolume(directory);
        }
#endif
        return null;
    }

    /// <summary>
    /// Finds an entry in <paramref name="directory"/> that has an ASCII letter, then looks up the same name with
    /// the case of its letters flipped. This works on every platform and file system.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if the flipped name is not found or is a different entry,
    /// <see langword="false"/> if the flipped name resolves to the same entry,
    /// <see langword="null"/> if the directory cannot be read or has no usable entry.
    /// </returns>
    internal static bool? ProbeByLookup(string directory)
    {
        try
        {
            DirectoryInfo directoryInfo = new(directory);
            string? flippedName = null;

            foreach (FileSystemInfo entry in directoryInfo.EnumerateFileSystemInfos())
            {
                // A symbolic link can point to a missing target, so the existence check below would fail
                // even on a case-insensitive file system.
                if ((entry.Attributes & FileAttributes.ReparsePoint) == 0 && TryFlipAsciiCase(entry.Name, out flippedName))
                {
                    break;
                }
            }

            if (flippedName is null)
            {
                return null;
            }

            string flippedPath = Path.Combine(directory, flippedName);
            if (!File.Exists(flippedPath) && !Directory.Exists(flippedPath))
            {
                return true;
            }

            // The flipped name resolves. It is the same entry unless the directory also lists it with this exact
            // case, which can happen only on a case-sensitive file system (for example, "Makefile" and "makefile").
            foreach (string entryPath in Directory.EnumerateFileSystemEntries(directory))
            {
                if (string.Equals(Path.GetFileName(entryPath), flippedName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    private static bool TryFlipAsciiCase(string name, out string? flipped)
    {
        char[] chars = name.ToCharArray();
        bool hasLetter = false;

        for (int i = 0; i < chars.Length; i++)
        {
            char c = chars[i];
            if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'))
            {
                // ASCII upper and lower case letters differ only in this bit.
                chars[i] = (char)(c ^ 0x20);
                hasLetter = true;
            }
        }

        flipped = hasLetter ? new string(chars) : null;
        return hasLetter;
    }

#if FEATURE_WINDOWSINTEROP
    /// <summary>
    /// Reads the per-directory case sensitivity flag (Windows 10 version 1803 and later).
    /// The query fails on older Windows versions and on file systems that do not support the flag, such as FAT.
    /// </summary>
    [SupportedOSPlatform("windows6.0.6000")]
    private static unsafe bool? QueryWindowsDirectoryFlag(string directory)
    {
        HANDLE handle = PInvoke.CreateFile(
            directory,
            (uint)FILE_ACCESS_RIGHTS.FILE_READ_ATTRIBUTES,
            FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
            null,
            FILE_CREATION_DISPOSITION.OPEN_EXISTING,
            FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS, // Required to open a directory.
            HANDLE.Null);

        if (handle == HANDLE.INVALID_HANDLE_VALUE)
        {
            return null;
        }

        try
        {
            FILE_CASE_SENSITIVE_INFO info = default;
            if (!PInvoke.GetFileInformationByHandleEx(handle, FILE_INFO_BY_HANDLE_CLASS.FileCaseSensitiveInfo, &info, (uint)sizeof(FILE_CASE_SENSITIVE_INFO)))
            {
                return null;
            }

            return (info.Flags & PInvoke.FILE_CS_FLAG_CASE_SENSITIVE_DIR) != 0;
        }
        finally
        {
            PInvoke.CloseHandle(handle);
        }
    }
#endif

#if NET
    /// <summary>
    /// Reads the per-volume case sensitivity with <c>pathconf(_PC_CASE_SENSITIVE)</c>.
    /// </summary>
    private static bool? QueryMacVolume(string directory)
        => pathconf(directory, PC_CASE_SENSITIVE) switch
        {
            0 => false,
            > 0 => true,

            // -1 means an error or that the file system does not report it.
            _ => null,
        };

    [LibraryImport("libc", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial nint pathconf(string path, int name);
#endif
}
