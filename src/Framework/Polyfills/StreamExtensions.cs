// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if !NET
using Microsoft.Build;

namespace System.IO;

internal static class StreamExtensions
{
    internal static void ReadExactly(this Stream stream, byte[] buffer, int offset, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfGreaterThan((uint)count, (uint)(buffer.Length - offset));

        while (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read <= 0)
            {
                throw new EndOfStreamException();
            }
            offset += read;
            count -= read;
        }
    }
}
#endif
