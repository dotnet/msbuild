// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System;
using System.Text;
using Microsoft.Build.Utilities;
#endif
#if FEATURE_WINDOWSINTEROP || NET
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
#endif
#if FEATURE_WINDOWSINTEROP
using System.Runtime.Versioning;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;
#endif

namespace Microsoft.Build.Shared;

internal readonly record struct DirectoryChangeStamp(long Value, uint Nanoseconds = 0);

internal static partial class DirectoryChangeTime
{
    internal static bool TryRead(string path, out DirectoryChangeStamp stamp)
    {
#if FEATURE_WINDOWSINTEROP
        if (NativeMethods.IsWindows)
        {
            return TryReadWindows(path, out stamp);
        }
#endif
#if NET
        if (NativeMethods.IsLinux)
        {
            try
            {
                return TryReadLinux(path, out stamp);
            }
            catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
            {
                DebugTrace.WriteLine($"Directory change-time validation is unavailable: {ex.Message}", category: nameof(DirectoryChangeTime));
            }
        }
#endif
        stamp = default;
        return false;
    }

#if FEATURE_WINDOWSINTEROP
    [SupportedOSPlatform("windows6.1")]
    private static unsafe bool TryReadWindows(string path, out DirectoryChangeStamp stamp)
    {
        stamp = default;
        HANDLE handle;
        fixed (char* pointer = path)
        {
            handle = PInvoke.CreateFile(
                new PCWSTR(pointer),
                (uint)(FILE_ACCESS_RIGHTS.FILE_READ_ATTRIBUTES | FILE_ACCESS_RIGHTS.FILE_LIST_DIRECTORY),
                FILE_SHARE_MODE.FILE_SHARE_READ | FILE_SHARE_MODE.FILE_SHARE_WRITE | FILE_SHARE_MODE.FILE_SHARE_DELETE,
                null,
                FILE_CREATION_DISPOSITION.OPEN_EXISTING,
                FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_BACKUP_SEMANTICS | FILE_FLAGS_AND_ATTRIBUTES.FILE_FLAG_OPEN_REPARSE_POINT,
                HANDLE.Null);
        }
        if (handle == HANDLE.INVALID_HANDLE_VALUE)
        {
            TraceFailure(path, Marshal.GetLastWin32Error());
            return false;
        }
        try
        {
            FILE_BASIC_INFO basic;
            if (!PInvoke.GetFileInformationByHandleEx(handle, FILE_INFO_BY_HANDLE_CLASS.FileBasicInfo, &basic, (uint)sizeof(FILE_BASIC_INFO)))
            {
                TraceFailure(path, Marshal.GetLastWin32Error());
                return false;
            }
            if ((basic.FileAttributes & (uint)(FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY | FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_REPARSE_POINT))
                != (uint)FILE_FLAGS_AND_ATTRIBUTES.FILE_ATTRIBUTE_DIRECTORY
                || basic.ChangeTime == 0)
            {
                return false;
            }
            stamp = new DirectoryChangeStamp(basic.ChangeTime);
            return true;
        }
        finally
        {
            if (!PInvoke.CloseHandle(handle))
            {
                TraceFailure(path, Marshal.GetLastWin32Error());
            }
        }
    }
#endif

#if NET
    [Flags]
    private enum StatxFields : uint
    {
        Type = 0x1,
        ChangeTime = 0x80,
    }

    [Flags]
    private enum StatxFlags
    {
        NoFollow = 0x100,
        ForceSync = 0x2000,
    }

    private enum UnixFileType : ushort
    {
        Directory = 0x4000,
        Mask = 0xF000,
    }

    // statx has a fixed 256-byte ABI; retain the complete buffer, requesting only type and ctime.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct StatxData
    {
        [FieldOffset(0)]
        internal StatxFields Fields;
        [FieldOffset(28)]
        internal UnixFileType FileType;
        [FieldOffset(96)]
        internal long ChangeSeconds;
        [FieldOffset(104)]
        internal uint ChangeNanoseconds;
    }

    [LibraryImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static unsafe partial int Statx(int directoryDescriptor, byte* path, StatxFlags flags, StatxFields fields, StatxData* result);

    private static unsafe bool TryReadLinux(string path, out DirectoryChangeStamp stamp)
    {
        stamp = default;
        const int currentDirectory = -100; // AT_FDCWD
        const StatxFields required = StatxFields.Type | StatxFields.ChangeTime;
        int length = Encoding.UTF8.GetByteCount(path);
        using BufferScope<byte> buffer = new(stackalloc byte[512], checked(length + 1));
        Encoding.UTF8.GetBytes(path.AsSpan(), buffer.AsSpan());
        buffer[length] = 0;
        StatxData data;
        int result;
        fixed (byte* pointer = buffer)
        {
            result = Statx(currentDirectory, pointer, StatxFlags.NoFollow | StatxFlags.ForceSync, required, &data);
        }
        if (result != 0)
        {
            TraceFailure(path, Marshal.GetLastPInvokeError());
            return false;
        }
        if ((data.Fields & required) != required
            || (data.FileType & UnixFileType.Mask) != UnixFileType.Directory
            || data.ChangeNanoseconds >= 1_000_000_000)
        {
            DebugTrace.WriteLine($"Directory change-time metadata is incomplete for '{path}'.", category: nameof(DirectoryChangeTime));
            return false;
        }
        stamp = new DirectoryChangeStamp(data.ChangeSeconds, data.ChangeNanoseconds);
        return true;
    }
#endif

#if FEATURE_WINDOWSINTEROP || NET
    private static void TraceFailure(string path, int error)
        => DebugTrace.WriteLine($"Could not read directory change time for '{path}': native error {error}.", category: nameof(DirectoryChangeTime));
#endif
}
