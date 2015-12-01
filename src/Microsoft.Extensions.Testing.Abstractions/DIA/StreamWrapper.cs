// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;

namespace dia2
{
    public class StreamWrapper : IStream
    {
        private Stream _stream;

        public StreamWrapper(Stream stream)
        {
            _stream = stream;
        }

        public void RemoteRead(byte[] pv, int cb, out uint pcbRead)
        {
            pcbRead = (uint)_stream.Read(pv, 0, cb);
        }

        public void Stat(out tagSTATSTG pstatstg, [In]uint grfStatFlag)
        {
            pstatstg = new tagSTATSTG();
            pstatstg.cbSize.QuadPart = (ulong)_stream.Length;
        }

        public void RemoteSeek([In]_LARGE_INTEGER dlibMove, [In]uint dwOrigin, out _ULARGE_INTEGER plibNewPosition)
        {
            plibNewPosition.QuadPart = (ulong)_stream.Seek(dlibMove.QuadPart, (SeekOrigin)dwOrigin);
        }

        public void RemoteRead(byte[] pv, [In]uint cb, out uint pcbRead)
        {
            pcbRead = (uint)_stream.Read(pv, offset: 0, count: (int)cb);
        }

        public void SetSize([In]_ULARGE_INTEGER libNewSize)
        {
            throw new NotImplementedException();
        }

        public void RemoteCopyTo([In, MarshalAs(UnmanagedType.Interface)]IStream pstm, [In]_ULARGE_INTEGER cb, out _ULARGE_INTEGER pcbRead, out _ULARGE_INTEGER pcbWritten)
        {
            throw new NotImplementedException();
        }

        public void Commit([In]uint grfCommitFlags)
        {
            throw new NotImplementedException();
        }

        public void Revert()
        {
            throw new NotImplementedException();
        }

        public void LockRegion([In]_ULARGE_INTEGER libOffset, [In]_ULARGE_INTEGER cb, [In]uint dwLockType)
        {
            throw new NotImplementedException();
        }

        public void UnlockRegion([In]_ULARGE_INTEGER libOffset, [In]_ULARGE_INTEGER cb, [In]uint dwLockType)
        {
            throw new NotImplementedException();
        }

        public void Clone([MarshalAs(UnmanagedType.Interface)]out IStream ppstm)
        {
            throw new NotImplementedException();
        }

        public void RemoteWrite([In]ref byte pv, [In]uint cb, out uint pcbWritten)
        {
            throw new NotImplementedException();
        }
    }
}