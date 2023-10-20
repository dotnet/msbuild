// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Logging
{
    internal static class StreamExtensions
    {
        private static bool CheckIsSkipNeeded(long bytesCount)
        {
            if(bytesCount is < 0 or > int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(nameof(bytesCount), ResourceUtilities.FormatResourceStringStripCodeAndKeyword("Binlog_StreamUtils_UnsupportedSkipOffset",
                    bytesCount));
            }

            return bytesCount > 0;
        }

        public static long SkipBytes(this Stream stream, long bytesCount, bool throwOnEndOfStream)
        {
            if (!CheckIsSkipNeeded(bytesCount))
            {
                return 0;
            }

            byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
            using var _ = new CleanupScope(() => ArrayPool<byte>.Shared.Return(buffer));
            return SkipBytes(stream, bytesCount, throwOnEndOfStream, buffer);
        }

        public static long SkipBytes(this Stream stream, long bytesCount, bool throwOnEndOfStream, byte[] buffer)
        {
            if (!CheckIsSkipNeeded(bytesCount))
            {
                return 0;
            }

            long totalRead = 0;
            while (totalRead < bytesCount)
            {
                int read = stream.Read(buffer, 0, (int)Math.Min(bytesCount - totalRead, buffer.Length));
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
            return TransparentReadStream.EnsureSeekableStream(stream);
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
