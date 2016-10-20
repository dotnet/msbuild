// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Base class for the implementation of a node endpoint for out-of-proc nodes.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;

namespace Microsoft.Build.BackEnd
{
    internal class BufferedReadStream : Stream
    {
        const int BUFFER_SIZE = 1024;

        Stream _innerStream;
        byte[] _buffer;

        int _remainingBufferSize;
        int _currentIndexInBuffer;

        public BufferedReadStream(Stream innerStream)
        {
            _innerStream = innerStream;
            _buffer = new byte[BUFFER_SIZE];

            _remainingBufferSize = 0;
        }

        public override bool CanRead { get { return _innerStream.CanRead; } }

        public override bool CanSeek { get { return false; } }

        public override bool CanWrite { get { return _innerStream.CanWrite; } }

        public override long Length { get { return _innerStream.Length; } }

        public override long Position
        {
            get { return _innerStream.Position; }
            set { _innerStream.Position = value; }
        }

        public override void Flush()
        {
            _innerStream.Flush();
        }

        public override int ReadByte()
        {
            if (_remainingBufferSize > 0)
            {
                int ret = _buffer[_currentIndexInBuffer];
                _currentIndexInBuffer++;
                _remainingBufferSize--;
                return ret;
            }
            else
            {
                //  Let the base class handle it, which will end up calling the Read() method
                return base.ReadByte();
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count > BUFFER_SIZE)
            {
                //  Trying to read more data than the buffer can hold
                int alreadyCopied = 0;
                if (_remainingBufferSize > 0)
                {
                    Array.Copy(_buffer, _currentIndexInBuffer, buffer, offset, _remainingBufferSize);
                    alreadyCopied = _remainingBufferSize;
                    _currentIndexInBuffer = 0;
                    _remainingBufferSize = 0;
                }
                int innerReadCount = _innerStream.Read(buffer, offset + alreadyCopied, count - alreadyCopied);
                return innerReadCount + alreadyCopied;
            }
            else if (count <= _remainingBufferSize)
            {
                //  Enough data buffered to satisfy read request
                Array.Copy(_buffer, _currentIndexInBuffer, buffer, offset, count);
                _currentIndexInBuffer += count;
                _remainingBufferSize -= count;
                return count;
            }
            else
            {
                //  Need to read more data
                int alreadyCopied = 0;
                if (_remainingBufferSize > 0)
                {
                    Array.Copy(_buffer, _currentIndexInBuffer, buffer, offset, _remainingBufferSize);
                    alreadyCopied = _remainingBufferSize;
                    _currentIndexInBuffer = 0;
                    _remainingBufferSize = 0;
                }

                int innerReadCount = _innerStream.Read(_buffer, 0, BUFFER_SIZE);
                _currentIndexInBuffer = 0;
                _remainingBufferSize = innerReadCount;

                int remainingCopyCount;

                if (alreadyCopied + innerReadCount >= count)
                {
                    remainingCopyCount = count - alreadyCopied;
                }
                else
                {
                    remainingCopyCount = innerReadCount;
                }

                Array.Copy(_buffer, 0, buffer, offset + alreadyCopied, remainingCopyCount);
                _currentIndexInBuffer += remainingCopyCount;
                _remainingBufferSize -= remainingCopyCount;

                return alreadyCopied + remainingCopyCount;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _innerStream.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _innerStream.Dispose();
            }

            base.Dispose(disposing);
        }

        
    }
}
