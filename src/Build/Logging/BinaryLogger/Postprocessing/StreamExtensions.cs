// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;

namespace Microsoft.Build.Logging
{
    internal static class StreamExtensions
    {
        public static int ReadAtLeast(this Stream stream, byte[] buffer, int offset, int minimumBytes, bool throwOnEndOfStream)
        {
            Debug.Assert(offset + minimumBytes <= buffer.Length);

            int totalRead = 0;
            while (totalRead < minimumBytes)
            {
                int read = stream.Read(buffer, offset, minimumBytes - totalRead);
                if (read == 0)
                {
                    if (throwOnEndOfStream)
                    {
                        throw new InvalidDataException("Unexpected end of stream.");
                    }

                    return totalRead;
                }

                totalRead += read;
                offset += read;
            }

            return totalRead;
        }

        public static int SkipBytes(this Stream stream, int bytesCount, bool throwOnEndOfStream)
        {
            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
            using var _ = new CleanupScope(() => ArrayPool<byte>.Shared.Return(buffer));
            return SkipBytes(stream, bytesCount, throwOnEndOfStream, buffer);
        }

        public static int SkipBytes(this Stream stream, int bytesCount, bool throwOnEndOfStream, byte[] buffer)
        {
            int totalRead = 0;
            while (totalRead < bytesCount)
            {
                int read = stream.Read(buffer, 0,  Math.Min(bytesCount - totalRead, buffer.Length));
                if (read == 0)
                {
                    if (throwOnEndOfStream)
                    {
                        throw new InvalidDataException("Unexpected end of stream.");
                    }

                    return totalRead;
                }

                totalRead += read;
            }

            return totalRead;
        }

        public static Stream ToReadableSeekableStream(this Stream stream)
        {
            return TransparentReadStream.CreateSeekableStream(stream);
        }

        /// <summary>
        /// Creates bounded read-only, forward-only view over an underlying stream.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="length"></param>
        /// <returns></returns>
        public static Stream Slice(this Stream stream, long length)
        {
            return new SubStream(stream, length);
        }
    }
}
