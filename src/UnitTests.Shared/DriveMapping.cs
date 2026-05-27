// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace Microsoft.Build.UnitTests.Shared;

internal static class DriveMapping
{
    private const int ERROR_FILE_NOT_FOUND = 2;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const DEFINE_DOS_DEVICE_FLAGS DDD_REMOVE_DEFINITION = (DEFINE_DOS_DEVICE_FLAGS)2;
    private const DEFINE_DOS_DEVICE_FLAGS DDD_NO_FLAG = 0;
    // extra space for '\??\'. Not counting for long paths support in tests.
    private const int MAX_PATH = 259;

    /// <summary>
    /// Windows specific. Maps path to a requested drive.
    /// </summary>
    /// <param name="letter">Drive letter</param>
    /// <param name="path">Path to be mapped</param>
    [SupportedOSPlatform("windows5.1.2600")]
    public static void MapDrive(char letter, string path)
    {
        if (!PInvoke.DefineDosDevice(DDD_NO_FLAG, ToDeviceName(letter), path))
        {
            NativeMethodsShared.ThrowExceptionForErrorCode(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Windows specific. Unmaps drive mapping.
    /// </summary>
    /// <param name="letter">Drive letter.</param>
    [SupportedOSPlatform("windows5.1.2600")]
    public static void UnmapDrive(char letter)
    {
        if (!PInvoke.DefineDosDevice(DDD_REMOVE_DEFINITION, ToDeviceName(letter), null))
        {
            NativeMethodsShared.ThrowExceptionForErrorCode(Marshal.GetLastWin32Error());
        }
    }

    /// <summary>
    /// Windows specific. Fetches path mapped under specific drive letter.
    /// </summary>
    /// <param name="letter">Drive letter.</param>
    /// <returns>Path mapped under specified letter. Empty string if mapping not found.</returns>
    [SupportedOSPlatform("windows5.1.2600")]
    public static unsafe string GetDriveMapping(char letter)
    {
        // since this is just for test purposes - let's not overcomplicate with long paths support
        char[] buffer = new char[MAX_PATH];
        string deviceName = ToDeviceName(letter);
        uint length;

        while (true)
        {
            fixed (char* pBuf = buffer)
            {
                fixed (char* pDevice = deviceName)
                {
                    length = PInvoke.QueryDosDevice(new PCWSTR(pDevice), new PWSTR(pBuf), (uint)buffer.Length);
                }
            }
            if (length != 0)
            {
                break;
            }

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

        // Translate from the native path semantic - starting with '\??\'. `length` is the
        // number of characters QueryDosDevice copied INCLUDING the trailing NUL; trim the
        // prefix (4) and the terminator (1) to get the managed string content.
        return new string(buffer, 4, (int)length - 5);
    }

    private static string ToDeviceName(char letter)
    {
        return $"{char.ToUpper(letter)}:";
    }
}
