// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NET
using System.Text;
using Microsoft.Build.Utilities;
#endif
#if NET
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;
#endif

namespace Microsoft.Build.Shared;

internal enum DirectoryStampStatus { Unavailable, Available, Missing }
internal enum DirectorySnapshotStatus { Unavailable, Valid, Mismatch }
internal delegate DirectoryStampStatus DirectoryStampReader(string path, out DirectoryChangeStamp stamp);

internal readonly record struct DirectoryChangeStamp(
    ulong VolumeOrDevice,
    ulong IdentityLow,
    ulong IdentityHigh,
    long Value,
    uint Nanoseconds = 0,
    long ModifiedSeconds = 0,
    uint ModifiedNanoseconds = 0,
    long Size = 0,
    uint Mode = 0,
    uint Generation = 0)
{
    internal bool IsOldEnoughToCache(DateTime utcNow)
    {
        if (Nanoseconds >= 1_000_000_000 || ModifiedNanoseconds >= 1_000_000_000) { return false; }
        // Timestamp precision does not establish update granularity, including on multigrain kernels.
        const long minimumAgeTicks = TimeSpan.TicksPerSecond + 50 * TimeSpan.TicksPerMillisecond;
        const long unixEpochTicks = 621355968000000000;
        long cutoff = utcNow.Ticks - unixEpochTicks - minimumAgeTicks;
        long seconds = Math.DivRem(cutoff, TimeSpan.TicksPerSecond, out long ticks);
        if (ticks < 0)
        {
            seconds--;
            ticks += TimeSpan.TicksPerSecond;
        }

        return (Value < seconds || (Value == seconds && Nanoseconds <= ticks * 100))
            && (ModifiedSeconds < seconds || (ModifiedSeconds == seconds && ModifiedNanoseconds <= ticks * 100));
    }
}

internal static partial class DirectoryChangeTime
{
    internal static bool TryRead(string path, out DirectoryChangeStamp stamp)
        => Read(path, out stamp, out _) == DirectoryStampStatus.Available;

    internal static DirectoryStampStatus Read(string path, out DirectoryChangeStamp stamp, out bool unsupportedFileSystem)
    {
        unsupportedFileSystem = false;
#if NET
        try
        {
            if (NativeMethods.IsLinux)
            {
                return ReadLinux(path, out stamp, out unsupportedFileSystem);
            }
            if (NativeMethods.IsOSX)
            {
                return ReadMacOS(path, out stamp, out unsupportedFileSystem);
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            DebugTrace.WriteLine($"Directory change-time validation is unavailable: {ex.Message}", category: nameof(DirectoryChangeTime));
        }
#endif
        stamp = default;
        return DirectoryStampStatus.Unavailable;
    }

#if NET
    [Flags]
    internal enum StatxFields : uint
    {
        Type = 0x1,
        Mode = 0x2,
        ModificationTime = 0x40,
        ChangeTime = 0x80,
        Identity = 0x100,
        Size = 0x200,
        MountId = 0x1000,
    }

    [Flags]
    private enum StatxFlags
    {
        NoFollow = 0x100,
        ForceSync = 0x2000,
    }

    internal enum UnixFileType : ushort
    {
        Directory = 0x4000,
        Mask = 0xF000,
    }

    [StructLayout(LayoutKind.Explicit, Size = 256)]
    internal struct StatxData
    {
        [FieldOffset(0)]
        internal StatxFields Fields;
        [FieldOffset(28)]
        internal UnixFileType FileType;
        [FieldOffset(32)]
        internal ulong Identity;
        [FieldOffset(40)]
        internal ulong Size;
        [FieldOffset(96)]
        internal long ChangeSeconds;
        [FieldOffset(104)]
        internal uint ChangeNanoseconds;
        [FieldOffset(112)]
        internal long ModifiedSeconds;
        [FieldOffset(120)]
        internal uint ModifiedNanoseconds;
        [FieldOffset(136)]
        internal uint DeviceMajor;
        [FieldOffset(140)]
        internal uint DeviceMinor;
        [FieldOffset(144)]
        internal ulong MountId;
    }

    [LibraryImport("libc", EntryPoint = "statx", SetLastError = true)]
    private static unsafe partial int Statx(int directoryDescriptor, byte* path, StatxFlags flags, StatxFields fields, StatxData* result);

    [LibraryImport("libc", EntryPoint = "statfs", SetLastError = true)]
    private static unsafe partial int Statfs(byte* path, void* result);

    private static unsafe DirectoryStampStatus ReadLinux(string path, out DirectoryChangeStamp stamp, out bool unsupportedFileSystem)
    {
        stamp = default;
        unsupportedFileSystem = false;
        const int currentDirectory = -100; // AT_FDCWD
        const StatxFields required = RequiredLinuxFields;
        int length = Encoding.UTF8.GetByteCount(path);
        using BufferScope<byte> buffer = new(stackalloc byte[512], checked(length + 1));
        Encoding.UTF8.GetBytes(path.AsSpan(), buffer.AsSpan());
        buffer[length] = 0;
        StatxData data;
        int result;
        using BufferScope<byte> fileSystem = new(stackalloc byte[256]);
        fixed (byte* pointer = buffer)
        {
            fixed (byte* information = fileSystem)
            {
                if (Statfs(pointer, information) != 0)
                {
                    return MissingOrUnavailable(Marshal.GetLastPInvokeError());
                }

                if (!IsSupportedLinuxFileSystem(unchecked((uint)MemoryMarshal.Read<nint>(fileSystem.AsSpan()))))
                {
                    unsupportedFileSystem = true;
                    return DirectoryStampStatus.Unavailable;
                }
            }

            result = Statx(currentDirectory, pointer, StatxFlags.NoFollow | StatxFlags.ForceSync, required, &data);
        }
        if (result != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            TraceFailure(path, error);
            return MissingOrUnavailable(error);
        }
        return CreateLinuxStamp(data, out stamp);
    }

    internal const StatxFields RequiredLinuxFields = StatxFields.Type | StatxFields.Mode
        | StatxFields.ChangeTime | StatxFields.ModificationTime | StatxFields.Identity | StatxFields.Size | StatxFields.MountId;

    internal static DirectoryStampStatus CreateLinuxStamp(in StatxData data, out DirectoryChangeStamp stamp)
    {
        stamp = default;
        if ((data.Fields & RequiredLinuxFields) != RequiredLinuxFields
            || (data.FileType & UnixFileType.Mask) != UnixFileType.Directory
            || data.Identity == 0 || data.MountId == 0 || data.Size > long.MaxValue
            || data.ChangeNanoseconds >= 1_000_000_000 || data.ModifiedNanoseconds >= 1_000_000_000)
        {
            return DirectoryStampStatus.Unavailable;
        }
        stamp = new DirectoryChangeStamp(
            ((ulong)data.DeviceMajor << 32) | data.DeviceMinor,
            data.Identity,
            data.MountId,
            data.ChangeSeconds,
            data.ChangeNanoseconds,
            data.ModifiedSeconds,
            data.ModifiedNanoseconds,
            (long)data.Size,
            (uint)data.FileType);
        return DirectoryStampStatus.Available;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    internal struct MacOSStatData
    {
        [FieldOffset(0)]
        internal uint Device;
        [FieldOffset(4)]
        internal UnixFileType FileType;
        [FieldOffset(8)]
        internal ulong Identity;
        [FieldOffset(48)]
        internal long ModifiedSeconds;
        [FieldOffset(56)]
        internal long ModifiedNanoseconds;
        [FieldOffset(64)]
        internal long ChangeSeconds;
        [FieldOffset(72)]
        internal long ChangeNanoseconds;
        [FieldOffset(96)]
        internal long Size;
        [FieldOffset(120)]
        internal uint Generation;
    }

    [LibraryImport("libSystem.B.dylib", EntryPoint = "lstat$INODE64", SetLastError = true)]
    private static unsafe partial int LStatMacOSX64(byte* path, MacOSStatData* result);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "lstat", SetLastError = true)]
    private static unsafe partial int LStatMacOSArm64(byte* path, MacOSStatData* result);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "statfs$INODE64", SetLastError = true)]
    private static unsafe partial int StatfsMacOSX64(byte* path, void* result);

    [LibraryImport("libSystem.B.dylib", EntryPoint = "statfs", SetLastError = true)]
    private static unsafe partial int StatfsMacOSArm64(byte* path, void* result);

    private static unsafe DirectoryStampStatus ReadMacOS(string path, out DirectoryChangeStamp stamp, out bool unsupportedFileSystem)
    {
        stamp = default;
        unsupportedFileSystem = false;
        Architecture architecture = RuntimeInformation.ProcessArchitecture;
        if (architecture is not (Architecture.X64 or Architecture.Arm64)) { return DirectoryStampStatus.Unavailable; }
        int length = Encoding.UTF8.GetByteCount(path);
        using BufferScope<byte> buffer = new(stackalloc byte[512], checked(length + 1));
        Encoding.UTF8.GetBytes(path.AsSpan(), buffer.AsSpan());
        buffer[length] = 0;
        // Darwin's 64-bit statfs ABI includes two 1024-byte mount-path fields.
        using BufferScope<byte> fileSystem = new(stackalloc byte[256], 2168);
        MacOSStatData data;
        fixed (byte* pointer = buffer)
        {
            fixed (byte* information = fileSystem)
            {
                int result = architecture == Architecture.X64
                    ? StatfsMacOSX64(pointer, information) : StatfsMacOSArm64(pointer, information);
                if (result != 0) { return MissingOrUnavailable(Marshal.GetLastPInvokeError()); }
            }
            ReadOnlySpan<byte> name = fileSystem.AsSpan().Slice(72, 16);
            int terminator = name.IndexOf((byte)0);
            if (terminator < 0 || !IsSupportedMacOSFileSystem(name.Slice(0, terminator),
                MemoryMarshal.Read<uint>(fileSystem.AsSpan().Slice(64))))
            {
                unsupportedFileSystem = true;
                return DirectoryStampStatus.Unavailable;
            }

            int status = architecture == Architecture.X64
                ? LStatMacOSX64(pointer, &data) : LStatMacOSArm64(pointer, &data);
            if (status != 0) { return MissingOrUnavailable(Marshal.GetLastPInvokeError()); }
        }
        return CreateMacOSStamp(data, MemoryMarshal.Read<ulong>(fileSystem.AsSpan().Slice(48)), out stamp);
    }

    internal static DirectoryStampStatus CreateMacOSStamp(in MacOSStatData data, ulong volumeId, out DirectoryChangeStamp stamp)
    {
        stamp = default;
        if ((data.FileType & UnixFileType.Mask) != UnixFileType.Directory
            || data.Identity == 0 || data.Size < 0
            || data.ChangeNanoseconds is < 0 or >= 1_000_000_000
            || data.ModifiedNanoseconds is < 0 or >= 1_000_000_000)
        {
            return DirectoryStampStatus.Unavailable;
        }
        stamp = new DirectoryChangeStamp(data.Device, data.Identity, volumeId,
            data.ChangeSeconds, (uint)data.ChangeNanoseconds, data.ModifiedSeconds,
            (uint)data.ModifiedNanoseconds, data.Size, (uint)data.FileType, data.Generation);
        return DirectoryStampStatus.Available;
    }

    internal static bool IsSupportedMacOSFileSystem(ReadOnlySpan<byte> name, uint flags)
        => (flags & 0x1000) != 0 && (name.SequenceEqual("apfs"u8) || name.SequenceEqual("hfs"u8));

    private static DirectoryStampStatus MissingOrUnavailable(int error)
        => error is 2 or 20 ? DirectoryStampStatus.Missing : DirectoryStampStatus.Unavailable;

    // Filesystem identification is an admission policy, not proof of timestamp uniqueness.
    internal static bool IsSupportedLinuxFileSystem(uint type) => type is
        0xEF53       // ext2/ext3/ext4
        or 0x58465342 // XFS
        or 0x9123683E // Btrfs
        or 0x01021994; // tmpfs
#endif

#if NET
    private static void TraceFailure(string path, int error)
        => DebugTrace.WriteLine($"Could not read directory change time for '{path}': native error {error}.", category: nameof(DirectoryChangeTime));
#endif
}
