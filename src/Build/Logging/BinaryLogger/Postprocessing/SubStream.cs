// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Bounded read-only, forward-only view over an underlying stream.
    /// </summary>
    internal sealed class SubStream : Stream
    {
        // Do not Dispose/Close on Dispose/Close !!
        private readonly Stream _stream;
        private readonly long _length;
        private long _position;

        public SubStream(Stream stream, long length)
        {
            _stream = stream;
            _length = length;

            if (!stream.CanRead)
            {
                throw new NotSupportedException(ResourceUtilities.GetResourceString("Binlog_StreamUtils_MustBeReadable"));
            }
        }

        public bool IsAtEnd => _position >= _length;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => _length;

        public override long Position { get => _position; set => throw new NotImplementedException(); }

        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = Math.Min((int)Math.Max(Length - _position, 0), count);
            int read = _stream.Read(buffer, offset, count);
            _position += read;
            return read;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }
}
