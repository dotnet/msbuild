using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Build.Framework.Logging;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    /// <summary>
    /// Combines BufferedStream, BinaryReader, and TransparentReadStream into a single optimized class.
    /// </summary>
    /// <remarks>
    /// This class combines BinaryReader and BufferedStream by pre-reading from the stream and inlining ReadBytes().
    /// For example, BinaryReader.Read7BitEncodedInt() calls ReadByte() byte by byte with a high overhead
    /// while this class will prefill 5 bytes for quick access.  Unused bytes will remain the buffer for next read operation.
    /// This class assumes that it is the only reader of the stream and does not support concurrent reads from the stream.
    /// Use the Slice() method to create a new stream.
    /// </remarks>
    internal class BufferedBinaryReader : IBinaryReader
    {
        private Stream baseStream;
        private long baseStreamPosition = 0;  // virtual Position of the base stream.
        private long maxAllowedPosition = long.MaxValue;
        private int bufferCapacity;
        private byte[] buffer;
        private int bufferOffset = 0;
        private int bufferLength = 0;
        private Encoding encoding;

        public BufferedBinaryReader(Stream stream, Encoding? encoding = null, int bufferCapacity = 32768)
        {
            if (!stream.CanRead)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("Binlog_StreamUtils_MustBeReadable"));
            }

            baseStream = stream;
            this.bufferCapacity = bufferCapacity;  // Note: bufferSize must be large enough for an Read operation.
            this.encoding = encoding ?? new UTF8Encoding();
            buffer = new byte[this.bufferCapacity];
        }

        /// <summary>
        /// Position of the base stream.
        /// </summary>
        public long Position => baseStreamPosition;

        /// <summary>
        /// Number of bytes allowed to read.  If set, then read functions will throw if exceeded by the amount.
        /// </summary>
        public int? BytesCountAllowedToRead
        {
            set
            {
                if (value.HasValue)
                {
                    if (value.Value < 0)
                    {
                        throw new Exception();
                    }

                    maxAllowedPosition = baseStreamPosition + value.Value;
                }
                else
                {
                    maxAllowedPosition = long.MaxValue;
                }
            }
        }

        /// <summary>
        /// If <see cref="BytesCountAllowedToRead"/> is set, then this is the number of bytes remaining to read.  Otherwise, 0.
        /// </summary>
        public int BytesCountAllowedToReadRemaining => maxAllowedPosition == long.MaxValue ? 0 : (int)(maxAllowedPosition - baseStreamPosition);

        /// <summary>
        /// Reads a 32-bit signed integer.
        /// </summary>
        /// <returns>Return a integer.</returns>
        public int ReadInt32()
        {
            FillBuffer(4);

            var result = (int)(buffer[bufferOffset] | buffer[bufferOffset + 1] << 8 | buffer[bufferOffset + 2] << 16 | buffer[bufferOffset + 3] << 24);
            bufferOffset += 4;
            baseStreamPosition += 4;
            return result;
        }

        // Reusable StringBuilder for ReadString().
        private StringBuilder? cachedBuilder;

        // Reusable char[] for ReadString().
        private char[]? charBuffer;

        /// <summary>
        /// Reads a string with a prefixed of the length.
        /// </summary>
        /// <returns>A string.</returns>
        public string ReadString()
        {
            int stringLength = Read7BitEncodedInt();
            int stringOffsetPos = 0;
            int readChunk = 0;

            if (stringLength == 0)
            {
                return string.Empty;
            }

            if (stringLength < 0)
            {
                throw new Exception();
            }

            if (charBuffer == null)
            {
                charBuffer = new char[bufferCapacity + 1];
            }

            int charRead = 0;

            if (bufferLength > 0)
            {
                // Read content in the buffer.
                readChunk = stringLength < (bufferLength - bufferOffset) ? stringLength : bufferLength - bufferOffset;
                charRead = encoding.GetChars(buffer, bufferOffset, readChunk, charBuffer, 0);
                bufferOffset += readChunk;
                baseStreamPosition += readChunk;
                if (stringLength == readChunk)
                {
                    // if the string is fits in the buffer, then cast to string without using string builder.
                    return new string(charBuffer, 0, charRead);
                }
                else
                {
                    cachedBuilder ??= new StringBuilder();
                    cachedBuilder.Append(charBuffer, 0, charRead);
                }
            }

            cachedBuilder ??= new StringBuilder();
            stringOffsetPos += readChunk;

            do
            {
                // Read up to bufferCapacity;
                readChunk = Math.Min(stringLength - stringOffsetPos, bufferCapacity);
                FillBuffer(readChunk);
                charRead = encoding.GetChars(buffer, bufferOffset, readChunk, charBuffer, 0);
                bufferOffset += readChunk;
                baseStreamPosition += readChunk;
                cachedBuilder.Append(charBuffer, 0, charRead);
                stringOffsetPos += readChunk;
            } while (stringOffsetPos < stringLength);

            string result = cachedBuilder.ToString();
            cachedBuilder.Clear();
            return result;
        }

        /// <summary>
        /// Reads an 8-byte signed integer.
        /// </summary>
        /// <returns></returns>
        public long ReadInt64()
        {
            FillBuffer(8);
            uint lo = (uint)(buffer[bufferOffset + 0] | buffer[bufferOffset + 1] << 8 |
                             buffer[bufferOffset + 2] << 16 | buffer[bufferOffset + 3] << 24);
            uint hi = (uint)(buffer[bufferOffset + 4] | buffer[bufferOffset + 5] << 8 |
                             buffer[bufferOffset + 6] << 16 | buffer[bufferOffset + 7] << 24);
            var result = (long)((ulong)hi) << 32 | lo;
            bufferOffset += 8;
            baseStreamPosition += 8;
            return result;
        }

        /// <summary>
        /// Reads a Boolean value.
        /// </summary>
        /// <returns>true if the byte is nonzero; otherwise, false.</returns>
        public bool ReadBoolean()
        {
            FillBuffer(1);
            var result = (buffer[bufferOffset] != 0);
            bufferOffset++;
            baseStreamPosition++;
            return result;
        }

        /// <summary>
        /// Reads the specified number of bytes into a new byte array.
        /// </summary>
        /// <param name="count">The number of bytes to read.</param>
        /// <returns>A byte array containing data read.</returns>
        public byte[] ReadBytes(int count)
        {
            if (count == 0)
            {
                return Array.Empty<byte>();
            }

            FillBuffer(count);
            if (bufferLength == 0)
            {
                return Array.Empty<byte>();
            }

            var result = new byte[count];
            Array.Copy(buffer, bufferOffset, result, 0, count);
            bufferOffset += count;
            baseStreamPosition += count;
            return result;
        }

        /// <summary>
        /// Reads the next byte.
        /// </summary>
        /// <returns>A byte.</returns>
        public byte ReadByte()
        {
            FillBuffer(1);
            return InternalReadByte();
        }

        /// <summary>
        /// Reads in a 32-bit integer in compressed format.
        /// </summary>
        /// <returns>A 32-bit integer.</returns>
        public int Read7BitEncodedInt()
        {
            FillBuffer(5);
            // Read out an Int32 7 bits at a time.  The high bit
            // of the byte when on means to continue reading more bytes.
            int count = 0;
            int shift = 0;
            byte b;
            do
            {
                // Check for a corrupted stream.  Read a max of 5 bytes.
                // In a future version, add a DataFormatException.
                if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                {
                    throw new FormatException();
                }

                b = InternalReadByte();
                count |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);

            return count;
        }

        public const int MaxBulkRead7BitLength = 10;
        private int[] resultInt = new int[MaxBulkRead7BitLength];

        /// <summary>
        /// An optimized bulk read of many continuous 7BitEncodedInt.
        /// </summary>
        /// <param name="numIntegers">Number of 7BitEncodedInt to read up to <see cref="MaxBulkRead7BitLength"/>.</param>
        /// <returns>An array of Integers with the results.</returns>
        /// <remarks>This will reuse the same result buffer so further calls will clear the results.</remarks>
        public int[] BulkRead7BitEncodedInt(int numIntegers)
        {
            FillBuffer(5 * numIntegers);
            int count = 0;
            int shift = 0;
            byte b;

            for (int i = 0; i < numIntegers; i++)
            {
                // Read out an Int32 7 bits at a time.  The high bit
                // of the byte when on means to continue reading more bytes.
                count = 0;
                shift = 0;
                b = 0;

                do
                {
                    // Check for a corrupted stream.  Read a max of 5 bytes.
                    // In a future version, add a DataFormatException.
                    if (shift == 5 * 7)  // 5 bytes max per Int32, shift += 7
                    {
                        throw new FormatException();
                    }

                    b = InternalReadByte();
                    count |= (b & 0x7F) << shift;
                    shift += 7;
                } while ((b & 0x80) != 0);

                resultInt[i] = count;
            }

            return resultInt;
        }

        /// <summary>
        /// See forward by a number of bytes.
        /// </summary>
        /// <param name="count">Number of bytes to advance forward.</param>
        /// <param name="current">Must be <see cref="SeekOrigin.Current"/>.</param>
        public void Seek(int count, SeekOrigin current)
        {
            if (current != SeekOrigin.Current || count < 0)
            {
                throw new NotSupportedException("Only seeking from SeekOrigin.Current and forward.");
            }

            if (count == 0)
            {
                return;
            }

            // TODO: optimized to avoid writing to the buffer.
            FillBuffer(count);
            bufferOffset += count;
            baseStreamPosition += count;
        }

        /// <summary>
        /// Slice a portion the stream into a new stream.
        /// </summary>
        /// <param name="numBytes">Number of bytes to consume.</param>
        /// <returns>A new stream from the current position.</returns>
        /// <remarks>Slice a portion of the current stream into a new stream.  This will advance <see cref="BufferedBinaryReader"/>.</remarks>
        public Stream Slice(int numBytes)
        {
            // create a memory stream of this number of bytes.
            if (numBytes == 0)
            {
                return Stream.Null;
            }

            if (numBytes < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(numBytes));
            }

            Stream resultStream;
            if (numBytes < bufferLength - bufferOffset)
            {
                MemoryStream memoryStream = new MemoryStream(numBytes);
                memoryStream.Write(buffer, bufferOffset, numBytes);
                memoryStream.Position = 0;
                resultStream = memoryStream;
            }
            else
            {
                MemoryStream memoryStream = new MemoryStream(bufferLength - bufferOffset);
                memoryStream.Write(buffer, bufferOffset, bufferLength - bufferOffset);
                memoryStream.Position = 0;
                resultStream = memoryStream.Concat(baseStream.Slice(numBytes - (bufferLength - bufferOffset)));
            }

            bufferOffset += numBytes;
            baseStreamPosition += numBytes;

            return resultStream;
        }

        public void Dispose()
        {
            ((IDisposable)baseStream).Dispose();
        }

        /// <summary>
        /// Prefill the buffer.
        /// </summary>
        /// <param name="numBytes">Number of bytes to prefill.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillBuffer(int numBytes)
        {
            if (bufferLength - bufferOffset >= numBytes)
            {
                return;  // enough space in the current buffer;
            }

            LoadBuffer();
        }

        private void LoadBuffer()
        {
            int numBytes = bufferCapacity;  // fill as much of the buffer as possible.
            int bytesRead = 0;
            int offset = bufferLength - bufferOffset;

            // Copy the remainder to the start.
            if (offset > 0)
            {
                Array.Copy(buffer, bufferOffset, buffer, 0, offset);
                bytesRead = offset;
            }

            do
            {
                offset = baseStream.Read(buffer, bytesRead, numBytes - bytesRead);
                if (offset == 0)
                {
                    break;  // Reached the End Of Stream
                }
                bytesRead += offset;
            } while (bytesRead < numBytes);

            bufferLength = bytesRead;
            bufferOffset = 0;
        }

        /// <summary>
        /// Inlined ReadByte that assumes that there is enough space created by FillBuffer().
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private byte InternalReadByte()
        {
            if (maxAllowedPosition < baseStreamPosition + 1)
            {
                throw new EndOfStreamException();
            }

            baseStreamPosition++;
            return buffer[bufferOffset++];
        }
    }
}
