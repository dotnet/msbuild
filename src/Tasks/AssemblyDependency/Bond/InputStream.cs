using System;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Implements IInputStream on top of System.Stream
    /// </summary>
    internal class InputStream : InputBuffer
    {
        // Default setting for maximum incremental allocation chunk size before reading from stream
        public const int DefaultAllocationChunk = 128 * 1024 * 1024;

        // Active setting for maximum incremental allocation chunk size before reading from stream
        public static int ActiveAllocationChunk
        {
            get { return activeAllocationChunk; }
            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Value must be positive.");
                }
                activeAllocationChunk = value;
            }
        }

        static readonly byte[][] EmptyTempBuffers = new byte[0][];

        static int activeAllocationChunk;

        readonly Stream stream;
        readonly int bufferLength;

        static InputStream()
        {
            ActiveAllocationChunk = DefaultAllocationChunk;
        }

        // When we read more data from the stream we can overwrite the
        // existing buffer only if it hasn't been exposed via ReadBytes or
        // Clone. Otherwise a new buffer has to be allocated.
        bool canReuseBuffer;

        public override long Length
        {
            get { return stream.Length; }
        }

        public override long Position
        {
            get { return stream.Position - (end - position); }
            set { position = checked ((int)(value - stream.Position)) + end; }
        }

        public InputStream(Stream stream, int bufferLength = 64 * 1024)
            : base(new byte[bufferLength], 0, 0)
        {
            this.stream = stream;
            this.bufferLength = bufferLength;
            canReuseBuffer = true;
        }

        /// <summary>
        /// Read an array of bytes verbatim
        /// </summary>
        /// <param name="count">Number of bytes to read</param>
        /// <exception cref="EndOfStreamException"/>
        public override ArraySegment<byte> ReadBytes(int count)
        {
            var result = base.ReadBytes(count);
            canReuseBuffer = false;
            return result;
        }

        internal override void EndOfStream(int count)
        {
            // The unread bytes left in the buffer. May be negative, which
            // indicates that this stream has been advanced beyond where we
            // are in the underlying stream and some bytes will need to be
            // skipped.
            var remaining = end - position;

            bool failed = false;
            byte[][] tempBuffers = EmptyTempBuffers;

            // Check whether we need to read in chunks to avoid allocating a
            // ton of memory ahead of time.
            if ((count > buffer.Length && (count - buffer.Length > ActiveAllocationChunk)))
            {
                // Calculate number of temp buffers; we round down since the
                // last chunk is read directly into final buffer. Note:
                // Difference is adjusted by -1 to round down correctly in
                // cases where the difference is exactly a multiple of the
                // allocation chunk size.
                int numTempBuffers = (count - buffer.Length - 1) / ActiveAllocationChunk;

                tempBuffers = new byte[numTempBuffers][];

                for (int i = 0; i < tempBuffers.Length; i++)
                {
                    tempBuffers[i] = new byte[ActiveAllocationChunk];

                    if (remaining < 0)
                    {
                        // We need to skip ahead in the underlying stream.
                        // Borrow the buffer to do the skipping before we do
                        // the real read.

                        // Only should happen for the first iteration, as we
                        // reset remaining.
                        Debug.Assert(i == 0);

                        AdvanceUnderlyingStream(-remaining, tempBuffers[i]);
                        remaining = 0;
                    }

                    var bytesRead = stream.Read(tempBuffers[i], 0, ActiveAllocationChunk);
                    if (bytesRead != ActiveAllocationChunk)
                    {
                        failed = true;
                        break;
                    }
                }
            }

            if (!failed)
            {
                var oldBuffer = buffer;

                if (!canReuseBuffer || count > buffer.Length)
                {
                    buffer = new byte[Math.Max(bufferLength, count)];
                    canReuseBuffer = true;
                }

                int offset;

                if (remaining > 0)
                {
                    // Copy any remaining bytes from the old buffer into the
                    // final buffer. This may just move the bytes to the
                    // beginning of the buffer.
                    Buffer.BlockCopy(oldBuffer, position, buffer, 0, remaining);
                    offset = remaining;
                }
                else if (remaining < 0)
                {
                    // Nothing in the old buffer, but we need to skip ahead
                    // in the underlying stream.
                    AdvanceUnderlyingStream(-remaining, buffer);
                    offset = 0;
                }
                else
                {
                    // The stars are aligned, so just start at the beginning
                    // of the final buffer.
                    offset = 0;
                }

                // Copy from any temp buffers into the final buffer. In the
                // common case, there are no temp buffers.
                foreach (byte[] tempBuffer in tempBuffers)
                {
                    Buffer.BlockCopy(
                        tempBuffer,
                        0,
                        buffer,
                        offset,
                        tempBuffer.Length);
                    offset += tempBuffer.Length;
                }

                // Read the final block; update valid length and position.
                end = offset + stream.Read(buffer, offset, buffer.Length - offset);
                position = 0;
            }

            if (count > end)
            {
                base.EndOfStream(count - end);
            }
        }

        /// <summary>
        /// Advances the underlying stream by <paramref name="count"/> bytes.
        /// </summary>
        /// <remarks>Correctly handles streams that cannot Seek.</remarks>
        /// <param name="count">The number of bytes to advance.</param>
        /// <param name="scratchBuffer">
        /// An already allocated buffer to use if dummy reads need to be
        /// performed.
        /// </param>
        void AdvanceUnderlyingStream(int count, byte[] scratchBuffer)
        {
            Debug.Assert(scratchBuffer != null);

            if (stream.CanSeek)
            {
                stream.Seek(count, SeekOrigin.Current);
            }
            else
            {
                while (count > 0)
                {
                    int bytesRead = stream.Read(
                        scratchBuffer,
                        offset: 0,
                        count: Math.Min(scratchBuffer.Length, count));
                    count -= bytesRead;

                    if (bytesRead == 0)
                    {
                        base.EndOfStream(count);
                    }
                }
            }
        }
    }
}
