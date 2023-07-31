// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;

namespace Microsoft.DotNet.Cli.Utils
{
    /// <summary>
    /// An in-memory stream that will block any read calls until something was written to it.
    /// </summary>
    public sealed class BlockingMemoryStream : Stream
    {
        private readonly BlockingCollection<byte[]> _buffers = new BlockingCollection<byte[]>();
        private ArraySegment<byte> _remaining;

        public override void Write(byte[] buffer, int offset, int count)
        {
            byte[] tmp = new byte[count];
            Buffer.BlockCopy(buffer, offset, tmp, 0, count);
            _buffers.Add(tmp);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0)
            {
                return 0;
            }

            if (_remaining.Count == 0)
            {
                byte[] tmp;
                if (!_buffers.TryTake(out tmp, Timeout.Infinite) || tmp.Length == 0)
                {
                    return 0;
                }
                _remaining = new ArraySegment<byte>(tmp, 0, tmp.Length);
            }

            if (_remaining.Count <= count)
            {
                count = _remaining.Count;
                Buffer.BlockCopy(_remaining.Array, _remaining.Offset, buffer, offset, count);
                _remaining = default(ArraySegment<byte>);
            }
            else
            {
                Buffer.BlockCopy(_remaining.Array, _remaining.Offset, buffer, offset, count);
                _remaining = new ArraySegment<byte>(_remaining.Array, _remaining.Offset + count, _remaining.Count - count);
            }
            return count;
        }

        public void DoneWriting()
        {
            _buffers.CompleteAdding();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _buffers.Dispose();
            }

            base.Dispose(disposing);
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length { get { throw new NotImplementedException(); } }
        public override long Position { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotImplementedException(); }
        public override void SetLength(long value) { throw new NotImplementedException(); }
    }
}
