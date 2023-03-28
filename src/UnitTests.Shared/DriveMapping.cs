// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace Microsoft.Build.UnitTests.Shared;

internal static class DriveMapping
{
    private const int ERROR_FILE_NOT_FOUND = 2;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const int DDD_REMOVE_DEFINITION = 2;
    private const int DDD_NO_FLAG = 0;
    // extra space for '\??\'. Not counting for long paths support in tests.
    private const int MAX_PATH = 259;

    /// <summary>
    /// Windows specific. Maps path to a requested drive.
    /// </summary>
    /// <param name="letter">Drive letter</param>
    /// <param name="path">Path to be mapped</param>
    [SupportedOSPlatform("windows")]
    public static void MapDrive(char letter, string path)
    {
        if (!DefineDosDevice(DDD_NO_FLAG, ToDeviceName(letter), path))
        {
            NativeMethodsShared.ThrowExceptionForErrorCode(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Windows specific. Unmaps drive mapping.
    /// </summary>
    /// <param name="letter">Drive letter.</param>
    [SupportedOSPlatform("windows")]
    public static void UnmapDrive(char letter)
    {
        if (!DefineDosDevice(DDD_REMOVE_DEFINITION, ToDeviceName(letter), null))
        {
            NativeMethodsShared.ThrowExceptionForErrorCode(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Windows specific. Fetches path mapped under specific drive letter.
    /// </summary>
    /// <param name="letter">Drive letter.</param>
    /// <returns>Path mapped under specified letter. Empty string if mapping not found.</returns>
    [SupportedOSPlatform("windows")]
    public static string GetDriveMapping(char letter)
    {
        // since this is just for test purposes - let's not overcomplicate with long paths support
        char[] buffer = new char[MAX_PATH];

        while (QueryDosDevice(ToDeviceName(letter), buffer, buffer.Length) == 0)
        {
            // Return empty string if the drive is not mapped
            int err = Marshal.GetLastWin32Error();
            if (err == ERROR_FILE_NOT_FOUND)
            {
                return string.Empty;
            }

            if (err != ERROR_INSUFFICIENT_BUFFER)
            {
                NativeMethodsShared.ThrowExceptionForErrorCode(err);
            }

            buffer = new char[buffer.Length * 4];
        }

        // Translate from the native path semantic - starting with '\??\'
        return new string(buffer, 4, buffer.Length - 4);
    }

    private static string ToDeviceName(char letter)
    {
        return $"{char.ToUpper(letter)}:";
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private static extern bool DefineDosDevice([In] int flags, [In] string deviceName, [In] string? path);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [SupportedOSPlatform("windows")]
    private static extern int QueryDosDevice([In] string deviceName, [Out] char[] buffer, [In] int bufSize);
}
