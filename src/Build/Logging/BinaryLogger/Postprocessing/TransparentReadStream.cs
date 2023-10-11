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

        public static Stream EnsureSeekableStream(Stream stream)
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

        public static TransparentReadStream EnsureTransparentReadStream(Stream stream)
        {
            if (stream is TransparentReadStream transparentReadStream)
            {
                return transparentReadStream;
            }

            if (!stream.CanRead)
            {
                throw new InvalidOperationException("Stream must be readable.");
            }

            return new TransparentReadStream(stream);
        }

        private TransparentReadStream(Stream stream)
        {
            _stream = stream;
        }

        public int? BytesCountAllowedToRead
        {
            set { _maxAllowedPosition = value.HasValue ? _position + value.Value : long.MaxValue; }
        }

        // if we haven't constrained the allowed read size - do not report it being unfinished either.
        public int BytesCountAllowedToReadRemaining =>
            _maxAllowedPosition == long.MaxValue ? 0 : (int)(_maxAllowedPosition - _position);

        private long _maxAllowedPosition = long.MaxValue;
        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;
        public override long Position
        {
            get => _position;
            set => this.SkipBytes((int)(value - _position), true);
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position + count > _maxAllowedPosition)
            {
                throw new StreamChunkOverReadException(
                    $"Attempt to read {count} bytes, when only {_maxAllowedPosition - _position} are allowed to be read.");
            }

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

            this.SkipBytes((int)offset, true);

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

        public override void Close() => _stream.Close();
    }
}
