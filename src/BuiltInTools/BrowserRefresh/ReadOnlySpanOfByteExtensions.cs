// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace System;

internal static class ReadOnlySpanOfByteExtensions
{
    public static int LastIndexOfNonWhiteSpace(this ReadOnlySpan<byte> buffer)
    {
        for (var i = buffer.Length - 1; i >= 0; i--)
        {
            if (!char.IsWhiteSpace(Convert.ToChar(buffer[i])))
            {
                return i;
            }
        }

        return -1;
    }

    public static bool EndsWithIgnoreCase(this ReadOnlySpan<byte> buffer, ReadOnlySpan<byte> value)
    {
        if (buffer.Length < value.Length)
        {
            return false;
        }

        for (var i = 1; i <= value.Length; i++)
        {
            if (char.ToLowerInvariant(Convert.ToChar(value[^i])) != char.ToLowerInvariant(Convert.ToChar(buffer[^i])))
            {
                return false;
            }
        }

        return true;
    }
}
