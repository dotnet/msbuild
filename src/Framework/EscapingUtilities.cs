// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
#if NET
using System.Buffers;
#endif
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Framework.Utilities;
using Microsoft.NET.StringTools;

#pragma warning disable SA1519 // Braces should not be omitted from multi-line child statement

namespace Microsoft.Build.Shared;

/// <summary>
///  Provides static methods for escaping and unescaping strings using the MSBuild <c>%XX</c> format,
///  where <c>XX</c> is the two-digit hexadecimal representation of the character's ASCII value.
/// </summary>
internal static class EscapingUtilities
{
    /// <summary>
    ///  Cache of escaped strings for use in performance-critical scenarios with significant expected string reuse.
    /// </summary>
    /// <remarks>
    ///  The cache currently grows unbounded.
    /// </remarks>
    private static readonly Dictionary<string, string> s_escapedStringCache = new(StringComparer.Ordinal);

    private static bool TryGetFromCache(string value, [NotNullWhen(true)] out string? result)
    {
        lock (s_escapedStringCache)
        {
            return s_escapedStringCache.TryGetValue(value, out result);
        }
    }

    private static void AddToCache(string key, string value)
    {
        lock (s_escapedStringCache)
        {
            s_escapedStringCache[key] = value;
        }
    }

#if NET
    private static readonly SearchValues<char> s_searchValues = SearchValues.Create(['%', '*', '?', '@', '$', '(', ')', ';', '\'']);

    private static int IndexOfAnyEscapeChar(string value, int startIndex = 0)
    {
        int i = value.AsSpan(startIndex).IndexOfAny(s_searchValues);
        return i < 0 ? i : i + startIndex;
    }
#else
    // All chars in s_charsToEscape lie within the ASCII range ['$' (0x24) .. '@' (0x40)].
    // Encoding each as bit (c - '$') in a uint gives a 29-bit bitmask that replaces the
    // per-char O(k) array scan inside IndexOfAny with a single range check + bit test.
    //   Bit:  0='$'  1='%'  3='\''  4='('  5=')'  6='*'  23=';'  27='?'  28='@'
    private const uint EscapeCharBitmask = 0x1880_007Bu;

    private static int IndexOfAnyEscapeChar(string value, int startIndex = 0)
    {
        for (int i = startIndex; i < value.Length; i++)
        {
            int offset = value[i] - '$';
            if ((uint)offset <= 28u && ((EscapeCharBitmask >> offset) & 1u) != 0)
            {
                return i;
            }
        }

        return -1;
    }
#endif

    private static bool TryDecodeHexDigit(char c, out int digit)
    {
        digit = HexConverter.FromChar(c);
        return digit != 0xff;
    }

    /// <summary>
    ///  Returns the lowercase hexadecimal digit character for <paramref name="value"/>.
    /// </summary>
    /// <param name="value">A value in the range [0, 15].</param>
    /// <returns>The character <c>0</c>–<c>9</c> or <c>a</c>–<c>f</c>.</returns>
    private static char HexDigitChar(int value)
        => (char)(value + (value < 10 ? '0' : 'a' - 10));

    /// <summary>
    ///  Replaces all instances of <c>%XX</c> in the input string with the character represented
    ///  by the hexadecimal number <c>XX</c>.
    /// </summary>
    /// <param name="value">The string to unescape.</param>
    /// <param name="trim">Whether the string should be trimmed before being unescaped.</param>
    /// <returns>
    ///  The unescaped string.
    /// </returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? UnescapeAll(string? value, bool trim = false)
    {
        if (value.IsNullOrEmpty())
        {
            return value;
        }

        int startIndex = 0;
        int endIndex = value.Length;

        if (trim)
        {
            while (startIndex < endIndex && char.IsWhiteSpace(value[startIndex]))
            {
                startIndex++;
            }

            if (startIndex == endIndex)
            {
                return string.Empty;
            }

            while (char.IsWhiteSpace(value[endIndex - 1]))
            {
                endIndex--;
            }
        }

        // Search only within the active [startIndex, endIndex) window.
        int percentIndex = value.IndexOf('%', startIndex, endIndex - startIndex);
        if (percentIndex == -1)
        {
            // value contains no escape sequences.
            return GetDefaultResult(value, startIndex, endIndex);
        }

        StringBuilder? sb = null;

        do
        {
            // There must be two hex characters following the percent sign.
            if (percentIndex <= endIndex - 3 &&
                TryDecodeHexDigit(value[percentIndex + 1], out int hi) &&
                TryDecodeHexDigit(value[percentIndex + 2], out int lo))
            {
                sb ??= StringBuilderCache.Acquire(value.Length);

                sb.Append(value, startIndex, percentIndex - startIndex);
                sb.Append((char)((hi << 4) + lo));
                startIndex = percentIndex + 3;
            }

            int nextIndex = percentIndex + 1;
            percentIndex = value.IndexOf('%', nextIndex, endIndex - nextIndex);
        }
        while (percentIndex >= 0);

        if (sb is null)
        {
            // No escape sequences were decoded; return the original string, or the trimmed
            // slice if trim was requested.
            return GetDefaultResult(value, startIndex, endIndex);
        }

        sb.Append(value, startIndex, endIndex - startIndex);

        return StringBuilderCache.GetStringAndRelease(sb);

        static string GetDefaultResult(string value, int startIndex, int endIndex)
            => startIndex == 0 && endIndex == value.Length
                ? value
                : value.Substring(startIndex, endIndex - startIndex);
    }

    /// <summary>
    ///  Escapes special characters in the input string by replacing them with their <c>%XX</c> equivalents.
    /// </summary>
    /// <param name="value">The string to escape.</param>
    /// <param name="cache">
    ///  <see langword="true"/> if the cache should be checked for an existing result and the
    ///  new result should be stored. Note: This is only recommended when significant repetition of
    ///  the escaped string is expected. The cache currently grows unbounded.
    /// </param>
    /// <returns>The escaped string.</returns>
    [return: NotNullIfNotNull(nameof(value))]
    public static string? Escape(string? value, bool cache = false)
    {
        if (value.IsNullOrEmpty())
        {
            return value;
        }

        // Find the first special char; if none, return early without allocating anything.
        int firstSpecialCharIndex = IndexOfAnyEscapeChar(value);
        if (firstSpecialCharIndex < 0)
        {
            return value;
        }

        if (cache && TryGetFromCache(value, out string? result))
        {
            return result;
        }

        using RefArrayBuilder<int> specialCharIndices = new(initialCapacity: 16);
        int specialCharIndex = firstSpecialCharIndex;

        do
        {
            specialCharIndices.Add(specialCharIndex);
            specialCharIndex = IndexOfAnyEscapeChar(value, specialCharIndex + 1);
        }
        while (specialCharIndex >= 0);

        result = Encode(value, specialCharIndices.AsSpan());

        if (cache)
        {
            result = Strings.WeakIntern(result);
            AddToCache(value, result);
        }

        return result;

        static string Encode(string value, ReadOnlySpan<int> specialCharIndices)
        {
            // Each special char expands from 1 to 3 chars (%XX), a net gain of 2 each.
            int length = value.Length + (specialCharIndices.Length * 2);

#if NET
            return string.Create(length, new EncodingHelper(value, specialCharIndices), static (destination, state) =>
            {
                var (source, specialCharIndices) = state;

                int sourceIndex = 0;

                foreach (int specialCharIndex in specialCharIndices)
                {
                    int charsToCopy = specialCharIndex - sourceIndex;
                    if (charsToCopy > 0)
                    {
                        source.Slice(sourceIndex, charsToCopy).CopyTo(destination);
                    }

                    destination = destination[charsToCopy..];

                    char ch = source[specialCharIndex];
                    destination[0] = '%';
                    destination[1] = HexDigitChar(ch >> 4);
                    destination[2] = HexDigitChar(ch & 0x0F);
                    destination = destination[3..];

                    sourceIndex = specialCharIndex + 1;
                }

                if (sourceIndex < source.Length)
                {
                    source.Slice(sourceIndex).CopyTo(destination);
                }
            });

#else

            string result = new('\0', length);

            unsafe
            {
                fixed (char* src = value)
                fixed (char* dst = result)
                {
                    int srcIndex = 0;
                    int dstIndex = 0;

                    foreach (int specialCharIdx in specialCharIndices)
                    {
                        int charsToCopy = specialCharIdx - srcIndex;
                        if (charsToCopy > 0)
                        {
                            Buffer.MemoryCopy(src + srcIndex, dst + dstIndex, charsToCopy * sizeof(char), charsToCopy * sizeof(char));
                            dstIndex += charsToCopy;
                        }

                        char ch = src[specialCharIdx];
                        dst[dstIndex] = '%';
                        dst[dstIndex + 1] = HexDigitChar(ch >> 4);
                        dst[dstIndex + 2] = HexDigitChar(ch & 0x0F);
                        dstIndex += 3;

                        srcIndex = specialCharIdx + 1;
                    }

                    int remainingChars = value.Length - srcIndex;
                    if (remainingChars > 0)
                    {
                        Buffer.MemoryCopy(src + srcIndex, dst + dstIndex, remainingChars * sizeof(char), remainingChars * sizeof(char));
                    }
                }
            }

            return result;
#endif
        }
    }

#if NET
    private readonly ref struct EncodingHelper(ReadOnlySpan<char> value, ReadOnlySpan<int> indices)
    {
        public readonly ReadOnlySpan<char> Value = value;
        public readonly ReadOnlySpan<int> Indices = indices;

        public void Deconstruct(out ReadOnlySpan<char> value, out ReadOnlySpan<int> indices)
        {
            value = Value;
            indices = Indices;
        }
    }
#endif

    /// <summary>
    ///  Determines whether <paramref name="value"/> contains the escaped form of
    ///  <c>*</c> (<c>%2a</c>/<c>%2A</c>) or <c>?</c> (<c>%3f</c>/<c>%3F</c>).
    /// </summary>
    /// <param name="value">The string to check.</param>
    /// <returns>
    ///  <see langword="true"/> if the string contains an escaped wildcard; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool ContainsEscapedWildcards(string value)
    {
        if (value.Length < 3)
        {
            return false;
        }

        // Search for '%', knowing it must be followed by at least 2 more characters.
        int percentIndex = value.IndexOf('%', startIndex: 0, value.Length - 2);

        while (percentIndex != -1)
        {
            char c = value[percentIndex + 1];

            if ((c is '2' && value[percentIndex + 2] is 'a' or 'A') ||
                (c is '3' && value[percentIndex + 2] is 'f' or 'F'))
            {
                // %2a or %2A → '*'
                // %3f or %3F → '?'
                return true;
            }

            percentIndex = value.IndexOf('%', percentIndex + 1, value.Length - (percentIndex + 1) - 2);
        }

        return false;
    }
}
