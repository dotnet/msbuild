// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace dia2
{
    [Guid("0000000C-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComImport]
    public interface IStream : ISequentialStream
    {
        new void RemoteRead([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] byte[] pv, int cb, out uint pcbRead);

        new void RemoteWrite([In] ref byte pv, [In] uint cb, out uint pcbWritten);

        void RemoteSeek([In] _LARGE_INTEGER dlibMove, [In] uint dwOrigin, out _ULARGE_INTEGER plibNewPosition);

        void SetSize([In] _ULARGE_INTEGER libNewSize);

        void RemoteCopyTo([MarshalAs(UnmanagedType.Interface)] [In] IStream pstm, [In] _ULARGE_INTEGER cb, out _ULARGE_INTEGER pcbRead, out _ULARGE_INTEGER pcbWritten);

        void Commit([In] uint grfCommitFlags);

        void Revert();

        void LockRegion([In] _ULARGE_INTEGER libOffset, [In] _ULARGE_INTEGER cb, [In] uint dwLockType);

        void UnlockRegion([In] _ULARGE_INTEGER libOffset, [In] _ULARGE_INTEGER cb, [In] uint dwLockType);

        void Stat(out tagSTATSTG pstatstg, [In] uint grfStatFlag);

        void Clone([MarshalAs(UnmanagedType.Interface)] out IStream ppstm);
    }
}
