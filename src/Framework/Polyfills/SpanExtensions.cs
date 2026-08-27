// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Polyfills for Span<T> APIs that were added in newer .NET versions:
//   - Span<char>.Replace(char, char) — .NET 8+
//   - ReadOnlySpan<T>.IndexOfAnyExcept<T>(T) — .NET 7+
//
// Lives in the System namespace alongside MemoryExtensions so callers can use
// the methods without an extra using.

#if !NET

using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace System;

internal static class SpanExtensions
{
    /// <summary>
    ///  Replaces all occurrences of <paramref name="oldValue"/> with <paramref name="newValue"/> in
    ///  <paramref name="span"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe void Replace(this Span<char> span, char oldValue, char newValue)
    {
        if (oldValue == newValue)
        {
            return;
        }

        fixed (char* p = span)
        {
            char* ptr = p;
            char* end = p + span.Length;

            while (ptr < end)
            {
                if (*ptr == oldValue)
                {
                    *ptr = newValue;
                }

                ptr++;
            }
        }
    }

    /// <summary>
    ///  Searches for the first index of any value other than the specified <paramref name="value"/>.
    /// </summary>
    /// <returns>
    ///  The index of the first element that is not equal to <paramref name="value"/>, or -1 if every element equals it.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int IndexOfAnyExcept<T>(this ReadOnlySpan<T> span, T value)
        where T : IEquatable<T>
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < span.Length; i++)
        {
            if (!comparer.Equals(span[i], value))
            {
                return i;
            }
        }

        return -1;
    }
}

#endif
