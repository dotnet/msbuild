// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// This is write-only, append-only stream that always buffers the wrapped stream
    /// into the chunks of the same size (except the possible shorter last chunk).
    /// So unlike the <see cref="BufferedStream"/> it never writes to the wrapped stream
    /// until it has full chunk or is closing.
    ///
    /// This is not supposed to bring performance benefits, but it allows to avoid nondeterministic
    /// GZipStream output for the identical input.
    /// </summary>
    internal class GreedyBufferedStream : Stream
    {
        private readonly Stream _stream;
        private readonly byte[] _buffer;
        private int _position;

        public GreedyBufferedStream(Stream stream, int bufferSize)
        {
            _stream = stream;
            _buffer = new byte[bufferSize];
        }

        public override void Flush()
        {
            _stream.Write(_buffer, 0, _position);
            _position = 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw UnsupportedException;

        public override long Seek(long offset, SeekOrigin origin) => throw UnsupportedException;

        public override void SetLength(long value) => throw UnsupportedException;

        public override void Write(byte[] buffer, int offset, int count)
        {
            // Appends input to the buffer until it is full - then flushes it to the wrapped stream.
            // Repeat above until all input is processed.

            int srcOffset = offset;
            do
            {
                int currentCount = Math.Min(count, _buffer.Length - _position);
                Buffer.BlockCopy(buffer, srcOffset, _buffer, _position, currentCount);
                _position += currentCount;
                count -= currentCount;
                srcOffset += currentCount;

                if (_position == _buffer.Length)
                {
                    Flush();
                }
            } while (count > 0);
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length + _position;

        public override long Position
        {
            get => _stream.Position + _position;
            set => throw UnsupportedException;
        }

        public override void Close()
        {
            Flush();
            _stream.Close();
            base.Close();
        }

        private Exception UnsupportedException => new NotSupportedException("GreedyBufferedStream is write-only, append-only");
    }
}
