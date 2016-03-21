// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.ComTypes;

namespace Microsoft.Extensions.Testing.Abstractions
{
    internal sealed class ComStreamWrapper : IStream
    {
        private readonly Stream _stream;

        public ComStreamWrapper(Stream stream)
        {
            Debug.Assert(stream != null);
            Debug.Assert(stream.CanSeek);

            _stream = stream;
        }

        public void Commit(int grfCommitFlags)
        {
            _stream.Flush();
        }

        /// <summary>
        /// The actual number of bytes read can be fewer than the number of bytes requested
        /// if an error occurs or if the end of the stream is reached during the read operation.
        /// </summary>
        public unsafe void Read(byte[] pv, int cb, IntPtr pcbRead)
        {
            int bytesRead = _stream.Read(pv, 0, cb);

            if (pcbRead != IntPtr.Zero)
            {
                *(int*)pcbRead = bytesRead;
            }
        }

        public unsafe void Seek(long dlibMove, int origin, IntPtr plibNewPosition)
        {
            long newPosition = _stream.Seek(dlibMove, (SeekOrigin)origin);
            if (plibNewPosition != IntPtr.Zero)
            {
                *(long*)plibNewPosition = newPosition;
            }
        }

        public void SetSize(long libNewSize)
        {
            _stream.SetLength(libNewSize);
        }

        public void Stat(out STATSTG pstatstg, int grfStatFlag)
        {
            pstatstg = new STATSTG()
            {
                cbSize = _stream.Length
            };
        }

        public unsafe void Write(byte[] pv, int cb, IntPtr pcbWritten)
        {
            _stream.Write(pv, 0, cb);
            if (pcbWritten != IntPtr.Zero)
            {
                *(int*)pcbWritten = cb;
            }
        }

        public void Clone(out IStream ppstm)
        {
            throw new NotSupportedException();
        }

        public void CopyTo(IStream pstm, long cb, IntPtr pcbRead, IntPtr pcbWritten)
        {
            throw new NotSupportedException();
        }

        public void LockRegion(long libOffset, long cb, int lockType)
        {
            throw new NotSupportedException();
        }

        public void Revert()
        {
            throw new NotSupportedException();
        }

        public void UnlockRegion(long libOffset, long cb, int lockType)
        {
            throw new NotSupportedException();
        }
    }
}
