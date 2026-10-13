// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if FEATURE_WINDOWSINTEROP
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Win32.SafeHandles;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Storage.FileSystem;

namespace Microsoft.Build.Shared;

[SupportedOSPlatform("windows6.1")]
internal sealed unsafe class DirectoryWatcher
{
    private readonly object _gate = new();
    private readonly ConcurrentDictionary<string, Watch> _watches = new(StringComparer.Ordinal);
    private static long s_version;
    private static long s_created;
    private static long s_closed;
    private static long s_notifications;
    private static long s_failures;
    private const FILE_NOTIFY_CHANGE Filter = FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_FILE_NAME | FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_DIR_NAME
        | FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_ATTRIBUTES | FILE_NOTIFY_CHANGE.FILE_NOTIFY_CHANGE_SECURITY;

    internal bool TryRead(string path, out DirectoryChangeStamp stamp)
    {
        stamp = default;
        if (!DirectoryChangeTime.TryRead(path, out DirectoryChangeStamp native)) { return false; }
        if (!_watches.TryGetValue(path, out Watch? watch))
        {
            lock (_gate)
            {
                if (!_watches.TryGetValue(path, out watch))
                {
                    watch = new Watch();
                    watch.Open(path, native);
                    _watches.TryAdd(path, watch);
                }
            }
        }
        return watch.TryRead(path, native, out stamp);
    }

    internal void Clear()
    {
        lock (_gate)
        {
            foreach (Watch watch in _watches.Values) { watch.Close(); }
            _watches.Clear();
        }
    }

    internal static long[] GetStatistics()
        => [Volatile.Read(ref s_created), Volatile.Read(ref s_closed),
            Volatile.Read(ref s_notifications), Volatile.Read(ref s_failures)];

    private static long NextVersion() => Interlocked.Increment(ref s_version);

    private static void TraceFailure(string path)
    {
        Interlocked.Increment(ref s_failures);
        DebugTrace.WriteLine($"Directory watcher validation failed for '{path}': Win32 {Marshal.GetLastWin32Error()}.", category: nameof(DirectoryWatcher));
    }

    private sealed class Watch
    {
        private readonly object _gate = new();
        private NotificationHandle? _handle;
        private DirectoryChangeStamp _native;
        private long _version;
        private bool _disabled;

        internal void Open(string path, DirectoryChangeStamp native)
        {
            string fullPath = Path.GetFullPath(path);
            string root = Path.GetPathRoot(fullPath)!;
            fixed (char* pointer = root)
            {
                uint drive = PInvoke.GetDriveType(new PCWSTR(pointer));
                if (drive is not (PInvoke.DRIVE_FIXED or PInvoke.DRIVE_REMOVABLE or PInvoke.DRIVE_RAMDISK))
                {
                    DebugTrace.WriteLine($"Directory watcher sharing is unavailable on '{path}' (drive type {drive}).", category: nameof(DirectoryWatcher));
                    _disabled = true;
                    return;
                }
            }
            HANDLE handle;
            fixed (char* pointer = fullPath)
            {
                handle = PInvoke.FindFirstChangeNotification(new PCWSTR(pointer), false, Filter);
            }
            if (handle == HANDLE.INVALID_HANDLE_VALUE)
            {
                TraceFailure(path);
                _disabled = true;
                return;
            }
            _handle = new NotificationHandle(handle);
            Interlocked.Increment(ref s_created);
            if (!DirectoryChangeTime.TryRead(path, out DirectoryChangeStamp after) || native != after)
            {
                DebugTrace.WriteLine($"Directory watcher admission raced with a change to '{path}'.", category: nameof(DirectoryWatcher));
                Disable();
                return;
            }
            _native = native;
            _version = NextVersion();
        }

        internal bool TryRead(string path, DirectoryChangeStamp native, out DirectoryChangeStamp stamp)
        {
            lock (_gate)
            {
                stamp = default;
                if (_disabled || _handle is null) { return false; }
                if (_native.VolumeOrDevice != native.VolumeOrDevice
                    || _native.IdentityLow != native.IdentityLow || _native.IdentityHigh != native.IdentityHigh)
                {
                    _handle.Dispose();
                    _handle = null;
                    Open(path, native);
                    if (_disabled || _handle is null) { return false; }
                }
                if (_native != native)
                {
                    _native = native;
                    _version = NextVersion();
                }
                HANDLE handle = (HANDLE)_handle.DangerousGetHandle();
                WAIT_EVENT result = PInvoke.WaitForSingleObject(handle, 0);
                if (result == WAIT_EVENT.WAIT_OBJECT_0)
                {
                    Interlocked.Increment(ref s_notifications);
                    _version = NextVersion();
                    if (!PInvoke.FindNextChangeNotification(handle))
                    {
                        TraceFailure(path);
                        Disable();
                        return false;
                    }
                    // A second queued change must not be mistaken for a quiet watcher.
                    result = PInvoke.WaitForSingleObject(handle, 0);
                    if (result == WAIT_EVENT.WAIT_OBJECT_0) { return false; }
                }
                if (result != WAIT_EVENT.WAIT_TIMEOUT)
                {
                    TraceFailure(path);
                    Disable();
                    return false;
                }
                stamp = new DirectoryChangeStamp(native.VolumeOrDevice, native.IdentityLow, native.IdentityHigh, _version);
                return true;
            }
        }

        internal void Close()
        {
            lock (_gate) { Disable(); }
        }

        private void Disable()
        {
            _disabled = true;
            _handle?.Dispose();
            _handle = null;
        }
    }

    private sealed class NotificationHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal NotificationHandle(HANDLE value) : base(ownsHandle: true) => SetHandle((nint)value.Value);

        protected override bool ReleaseHandle()
        {
            bool success = PInvoke.FindCloseChangeNotification((HANDLE)handle);
            if (!success) { TraceFailure("closing directory notification"); }
            Interlocked.Increment(ref s_closed);
            return success;
        }
    }
}
#endif
