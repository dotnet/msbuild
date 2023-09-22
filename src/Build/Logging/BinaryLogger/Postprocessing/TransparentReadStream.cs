// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// A wrapper stream that allows position tracking and forward seeking.
    /// </summary>
    internal class TransparentReadStream : Stream
    {
        private readonly Stream _stream;
        private long _position;

        public static Stream CreateSeekableStream(Stream stream)
        {
            if (stream.CanSeek)
            {
                return stream;
            }

            if(!stream.CanRead)
            {
                throw new InvalidOperationException("Stream must be readable.");
            }

            return new TransparentReadStream(stream);
        }

        private TransparentReadStream(Stream stream)
        {
            _stream = stream;
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;
        public override long Position
        {
            get => _position;
            set => SkipBytes(value - _position);
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int cnt = _stream.Read(buffer, offset, count);
            _position += cnt;
            return cnt;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if(origin != SeekOrigin.Current)
            {
                throw new InvalidOperationException("Only seeking from SeekOrigin.Current is supported.");
            }

            SkipBytes(offset);

            return _position;
        }

        public override void SetLength(long value)
        {
            throw new InvalidOperationException("Expanding stream is not supported.");
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new InvalidOperationException("Writing is not supported.");
        }

        private void SkipBytes(long count)
        {
            if(count < 0)
            {
                throw new InvalidOperationException("Seeking backwards is not supported.");
            }

            if(count == 0)
            {
                return;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent((int)count);
            try
            {
                _position += _stream.ReadAtLeast(buffer, 0, (int)count, throwOnEndOfStream: true);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        public override void Close() => _stream.Close();
    }
}
