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
    /// This class pre-read the stream into an internal buffer such that it could inline ReadBytes().
    /// For example, BinaryReader.Read7BitEncodedInt() calls ReadByte() byte by byte with a high overhead
    /// This class will pre-read 5 bytes for quick access.  Unused bytes will remain the buffer for next read operation.
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
        private Decoder decoder;

        public BufferedBinaryReader(Stream stream, Encoding? encoding = null, int bufferCapacity = 32768)
        {
            if (!stream.CanRead)
            {
                throw new InvalidOperationException(ResourceUtilities.GetResourceString("Binlog_StreamUtils_MustBeReadable"));
            }

            baseStream = stream;
            this.bufferCapacity = bufferCapacity;  // Note: bufferCapacity must be large enough for an BulkRead7BitEncodedInt operation.
            this.encoding = encoding ?? new UTF8Encoding();
            this.decoder = this.encoding.GetDecoder();  // Note: decode will remember partially decoded characters
            buffer = new byte[this.bufferCapacity];
            charBuffer = new char[bufferCapacity + 1];
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
                        throw new ArgumentException(nameof(value), "non-negative value expected.");
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
        /// If <see cref="BytesCountAllowedToRead"/> is set, then this is the number of remaining bytes allowed to read.  Is 0 when not set.
        /// </summary>
        public int BytesCountAllowedToReadRemaining => maxAllowedPosition == long.MaxValue ? 0 : (int)(maxAllowedPosition - baseStreamPosition);

        /// <summary>
        /// Reads a 32-bit signed integer.
        /// </summary>
        /// <returns>Return a integer.</returns>
        /// <remarks>Logic copied from BCL <see href="https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/BinaryReader.cs">BinaryReader.cs</see></remarks>
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
        private char[] charBuffer;

        /// <summary>
        /// Reads a string with a prefixed of the length.
        /// </summary>
        /// <returns>A string.</returns>
        /// <remarks>Logic refactored from BCL <see href="https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/BinaryReader.cs">BinaryReader.cs</see> to leverage local buffers as to avoid extra allocations.</remarks>
        public string ReadString()
        {
            // Length of the string in bytes, not chars
            int stringLength = Read7BitEncodedInt();
            int stringOffsetPos = 0;
            int readChunk = 0;
            int charRead = 0;

            if (stringLength == 0)
            {
                return string.Empty;
            }

            if (stringLength < 0)
            {
                throw new FormatException();
            }

            cachedBuilder ??= new StringBuilder();

            // Read the content from the local buffer.
            if (bufferLength > 0)
            {
                readChunk = stringLength < (bufferLength - bufferOffset) ? stringLength : bufferLength - bufferOffset;
                charRead = decoder.GetChars(buffer, bufferOffset, readChunk, charBuffer, 0, flush: false);
                bufferOffset += readChunk;
                baseStreamPosition += readChunk;
                stringOffsetPos += readChunk;

                // If the string is fits in the buffer, then cast to string without using string builder.
                if (stringLength == readChunk)
                {
                    return new string(charBuffer, 0, charRead);
                }
                else
                {
                    cachedBuilder.Append(charBuffer, 0, charRead);
                }
            }

            // Loop to read the stream multiple times, as the string could be larger then local buffer.
            do
            {
                readChunk = Math.Min(stringLength - stringOffsetPos, bufferCapacity);
                FillBuffer(readChunk);
                charRead = decoder.GetChars(buffer, bufferOffset, readChunk, charBuffer, 0, flush: false);
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
        /// <remarks>Logic copied from BCL <see href="https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/BinaryReader.cs">BinaryReader.cs</see></remarks>
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
        /// <remarks>Logic copied from BCL <see href="https://github.com/dotnet/runtime/blob/main/src/libraries/System.Private.CoreLib/src/System/IO/BinaryReader.cs">BinaryReader.cs</see></remarks>
        public bool ReadBoolean()
        {
            FillBuffer(1);
            var result = buffer[bufferOffset] != 0;
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

            // Avoid an allocation if the current buffer is large enough.
            byte[] result;
            if (count < this.bufferCapacity)
            {
                if (this.bufferOffset > 0)
                {
                    // content to the start of the buffer.
                    LoadBuffer();
                }

                result = this.buffer;
            }
            else
            {
                result = new byte[count];
            }

            Array.Copy(buffer, bufferOffset, result, 0, count);
            bufferOffset += count;
            baseStreamPosition += count;
            return result;
        }

        private byte[] resultGuidBytes = new byte[16];

        /// <summary>
        /// Read a 16 bytes that represents a GUID.
        /// </summary>
        /// <returns>A byte array containing a GUID.</returns>
        /// <remarks><see cref="Guid"/> constructor requires exactly a 16 byte array.  Use this instead of <see cref="ReadBytes"/> to guarantee returning an acceptable array size.</remarks>
        public byte[] ReadGuid()
        {
            const int guidCount = 16;
            FillBuffer(16);
            Array.Copy(buffer, bufferOffset, resultGuidBytes, 0, guidCount);
            bufferOffset += guidCount;
            baseStreamPosition += guidCount;

            return resultGuidBytes;
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
        /// Reads in a 32-bit integer with a 7bit encoding.
        /// </summary>
        /// <returns>A 32-bit integer.</returns>
        public int Read7BitEncodedInt()
        {
            // Prefill up to 5 bytes per Int32.
            FillBuffer(5, throwOnEOF: false);

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

                // Continue reading more bytes when the high bit of the byte is set.
            } while ((b & 0x80) != 0);

            return count;
        }

        public const int MaxBulkRead7BitLength = 10;
        private int[] resultIntArray = new int[MaxBulkRead7BitLength];

        /// <summary>
        /// An optimized bulk read of many continuous 7BitEncodedInt.
        /// </summary>
        /// <param name="numIntegers">Number of 7BitEncodedInt to read up to <see cref="MaxBulkRead7BitLength"/>.</param>
        /// <returns>An array of Integers with the results.</returns>
        /// <remarks>This will reuse the same array for results to avoid extra allocations.</remarks>
        public int[] BulkRead7BitEncodedInt(int numIntegers)
        {
            if (numIntegers > MaxBulkRead7BitLength)
            {
                throw new ArgumentOutOfRangeException();
            }

            // Prefill up to 5 bytes per integer.
            FillBuffer(5 * numIntegers, throwOnEOF: false);
            int count = 0;
            int shift = 0;
            byte b;

            for (int i = 0; i < numIntegers; i++)
            {
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

                    // Continue reading more bytes when the high bit of the byte is set.
                } while ((b & 0x80) != 0);

                resultIntArray[i] = count;
            }

            return resultIntArray;
        }

        /// <summary>
        /// Seek forward by a number of bytes.
        /// </summary>
        /// <param name="count">Number of bytes to advance forward.</param>
        /// <param name="current">Must be <see cref="SeekOrigin.Current"/>.</param>
        public void Seek(int count, SeekOrigin current)
        {
            if (current != SeekOrigin.Current || count < 0)
            {
                throw new NotSupportedException("Seeking is forward only and from SeekOrigin.Current.");
            }

            if (count == 0)
            {
                return;
            }

            // Check if count is within current buffer.
            if (bufferLength - bufferOffset > count)
            {
                bufferOffset += count;
                baseStreamPosition += count;
                return;
            }

            var remainder = count - (bufferLength - bufferOffset);
            bufferLength = 0;
            bufferOffset = 0;
            baseStreamPosition += count;

            var newPosition = baseStream.Seek(remainder, current);
            if (newPosition != baseStreamPosition)
            {
                // EOF
                baseStreamPosition = newPosition;
                return;
            }

            LoadBuffer();
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

        /// <summary>
        /// <inheritdoc/>
        /// </summary>
        public void Dispose()
        {
            ((IDisposable)baseStream).Dispose();
        }

        /// <summary>
        /// Prefill the buffer.
        /// </summary>
        /// <param name="numBytes">Number of bytes to prefill.</param>
        /// <param name="throwOnEOF">Throw if <paramref name="numBytes"/> exceed the number of bytes actually read.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void FillBuffer(int numBytes, bool throwOnEOF = true)
        {
            if (bufferLength - bufferOffset >= numBytes)
            {
                return;  // enough space in the current buffer;
            }

            LoadBuffer();

            if (throwOnEOF && bufferLength < numBytes)
            {
                throw new EndOfStreamException();
            }
        }

        /// <summary>
        /// Read from the stream to fill the internal buffer with size of set by <see cref="bufferCapacity"/>.
        /// </summary>
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
            if (maxAllowedPosition < baseStreamPosition + 1
                || bufferOffset >= bufferLength)
            {
                throw new EndOfStreamException();
            }

            baseStreamPosition++;
            return buffer[bufferOffset++];
        }
    }
}
