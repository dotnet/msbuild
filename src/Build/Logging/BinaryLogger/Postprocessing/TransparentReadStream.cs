﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// A wrapper stream that allows position tracking and forward seeking.
    /// </summary>
    internal sealed class TransparentReadStream : Stream
    {
        private readonly Stream _stream;
        private long _position;
        private long _maxAllowedPosition = long.MaxValue;

        public static Stream EnsureSeekableStream(Stream stream)
        {
            if (stream.CanSeek)
            {
                return stream;
            }

            if (!stream.CanRead)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("Binlog_StreamUtils_MustBeReadable"));
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
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("Binlog_StreamUtils_MustBeReadable"));
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

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => _stream.Length;
        public override long Position
        {
            get => _position;
            set => this.SkipBytes(value - _position);
        }

        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_position + count > _maxAllowedPosition)
            {
                count = (int)(_maxAllowedPosition - _position);
            }

            int cnt = _stream.Read(buffer, offset, count);
            _position += cnt;
            return cnt;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            if (origin != SeekOrigin.Current)
            {
                throw new NotSupportedException(ResourceUtilities.GetResourceString("Binlog_StreamUtils_SeekNonOrigin"));
            }

            this.SkipBytes(offset);

            return _position;
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException(ResourceUtilities.GetResourceString("Binlog_StreamUtils_SetLengthUnsupported"));
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException(ResourceUtilities.GetResourceString("Binlog_StreamUtils_WriteUnsupported"));
        }

        public override void Close() => _stream.Close();
    }
}
