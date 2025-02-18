// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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

        public override void Flush() => _stream.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _stream.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count)
        {
            count = Math.Min((int)Math.Max(Length - _position, 0), count);
            int read = _stream.Read(buffer, offset, count);
            _position += read;
            return read;
        }

        public override int ReadByte()
        {
            if (Length - _position > 0)
            {
                int value = _stream.ReadByte();
                if (value >= 0)
                {
                    _position++;
                    return value;
                }
            }

            return -1;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            count = Math.Min((int)Math.Max(Length - _position, 0), count);
            int read = await _stream.ReadAsync(
#if NET
                buffer.AsMemory(offset, count),
#else
                buffer, offset, count,
#endif
                cancellationToken).ConfigureAwait(false);
            _position += read;
            return read;
        }

#if NET
        public override int Read(Span<byte> buffer)
        {
            buffer = buffer.Slice(0, Math.Min((int)Math.Max(Length - _position, 0), buffer.Length));
            int read = _stream.Read(buffer);
            _position += read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            buffer = buffer.Slice(0, Math.Min((int)Math.Max(Length - _position, 0), buffer.Length));
            int read = await _stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            _position += read;
            return read;
        }
#endif

        public override long Seek(long offset, SeekOrigin origin) => throw new NotImplementedException();
        public override void SetLength(long value) => throw new NotImplementedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotImplementedException();
    }
}
