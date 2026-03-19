// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;
#if NET
using System.Buffers;
#endif
using Microsoft.Build.Framework;
using Microsoft.NET.StringTools;

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

    /// <summary>
    ///  Special characters that need escaping.
    /// </summary>
    /// <remarks>
    ///  <c>%</c> MUST be first — since it is both a character we escape and part of every escape sequence,
    ///  placing it first ensures we don't double-escape sequences already present in the input.
    /// </remarks>
    private static readonly char[] s_charsToEscape = ['%', '*', '?', '@', '$', '(', ')', ';', '\''];

#if NET
    private static readonly SearchValues<char> s_searchValues = SearchValues.Create(s_charsToEscape);

    private static int IndexOfAnyEscapeChar(string value, int startIndex = 0)
    {
        int i = value.AsSpan(startIndex).IndexOfAny(s_searchValues);
        return i < 0 ? i : i + startIndex;
    }
#else
    private static int IndexOfAnyEscapeChar(string value, int startIndex = 0)
        => value.IndexOfAny(s_charsToEscape, startIndex);
#endif

    private static bool TryDecodeHexDigit(char ch, out int value)
    {
        if (ch is >= '0' and <= '9')
        {
            value = ch - '0';
            return true;
        }

        if (ch is >= 'A' and <= 'F')
        {
            value = ch - 'A' + 10;
            return true;
        }

        if (ch is >= 'a' and <= 'f')
        {
            value = ch - 'a' + 10;
            return true;
        }

        value = default;
        return false;
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

        // If there are no percent signs, return early without allocating a StringBuilder.
        int percentIndex = value.IndexOf('%');
        if (percentIndex == -1)
        {
            return trim ? value.Trim() : value;
        }

        int startIndex = 0;
        int endIndex = value.Length;

        if (trim)
        {
            while (startIndex < value.Length && char.IsWhiteSpace(value[startIndex]))
            {
                startIndex++;
            }

            if (startIndex == value.Length)
            {
                return string.Empty;
            }

            while (char.IsWhiteSpace(value[endIndex - 1]))
            {
                endIndex--;
            }
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

            percentIndex = value.IndexOf('%', percentIndex + 1);
        }
        while (percentIndex >= 0);

        if (sb is not null)
        {
            sb.Append(value, startIndex, endIndex - startIndex);

            return StringBuilderCache.GetStringAndRelease(sb);
        }

        // No escape sequences were decoded; return the original string, or the trimmed
        // slice if trim was requested.
        return startIndex == 0 && endIndex == value.Length
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
        // If there are no special chars, return early without allocating a StringBuilder.
        if (value.IsNullOrEmpty() || IndexOfAnyEscapeChar(value) < 0)
        {
            return value;
        }

        if (cache)
        {
            lock (s_escapedStringCache)
            {
                if (s_escapedStringCache.TryGetValue(value, out string? cachedEscapedString))
                {
                    return cachedEscapedString;
                }
            }
        }

        StringBuilder sb = StringBuilderCache.Acquire(value.Length * 2);

        int startIndex = 0;

        while (true)
        {
            int specialCharIndex = IndexOfAnyEscapeChar(value, startIndex);
            if (specialCharIndex == -1)
            {
                sb.Append(value, startIndex, value.Length - startIndex);
                break;
            }

            sb.Append(value, startIndex, specialCharIndex - startIndex);

            // Append escape sequence for special character.
            sb.Append('%');

            char ch = value[specialCharIndex];
            sb.Append(HexDigitChar(ch >> 4));
            sb.Append(HexDigitChar(ch & 0x0F));

            startIndex = specialCharIndex + 1;
        }

        if (!cache)
        {
            return StringBuilderCache.GetStringAndRelease(sb);
        }

        string result = Strings.WeakIntern(sb.ToString());
        StringBuilderCache.Release(sb);

        lock (s_escapedStringCache)
        {
            s_escapedStringCache[value] = result;
        }

        return result;
    }

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
