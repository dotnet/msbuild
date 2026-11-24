// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Shared.Globbing;

/// <summary>
/// Matches MSBuild globs without translating them into another pattern language.
/// </summary>
internal sealed class GlobPattern
{
    private readonly string _pattern;
    private readonly TextInfo? _caseFolding;
    private readonly int _wildcardStart;
    private readonly int _filenameStart;
    private readonly bool _nameOnly;
    private readonly bool _trailingDot;
    private readonly bool _ordinal;

    internal GlobPattern(string fixedDirectory, string wildcardDirectory, string filename, bool useInvariantCulture)
    {
        _caseFolding = (useInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture).TextInfo;
        _trailingDot = filename.EndsWith(".", StringComparison.Ordinal);
        using BufferScope<char> buffer = new(stackalloc char[256], checked(fixedDirectory.Length + wildcardDirectory.Length + filename.Length));
        Span<char> pattern = buffer;
        int length = 0;
        bool unc = NativeMethods.IsWindows && fixedDirectory.StartsWith(@"\\", StringComparison.Ordinal);
        if (unc)
        {
            pattern[length++] = '\\';
            pattern[length++] = '\\';
        }

        AppendDirectory(pattern, ref length, fixedDirectory, wildcard: false, unc);
        _wildcardStart = length;
        AppendDirectory(pattern, ref length, wildcardDirectory, wildcard: true, unc: false);
        _filenameStart = length;
        ReadOnlySpan<char> normalizedFilename = NormalizeFilename(filename, _trailingDot);
        normalizedFilename.CopyTo(pattern[length..]);
        length += normalizedFilename.Length;
        _pattern = pattern[..length].ToString();
        _ordinal = useInvariantCulture && !_trailingDot && filename.IndexOf('?') < 0 && IsAsciiName(filename);
    }

    private GlobPattern(string name, bool ignoreCase, bool useInvariantCulture, bool isFilePattern)
    {
        _nameOnly = true;
        _caseFolding = ignoreCase
            ? (useInvariantCulture ? CultureInfo.InvariantCulture : CultureInfo.CurrentCulture).TextInfo
            : null;
        _trailingDot = isFilePattern && name.EndsWith(".", StringComparison.Ordinal);
        _pattern = isFilePattern ? NormalizeFilename(name, _trailingDot) : name;
        _ordinal = ignoreCase && useInvariantCulture && !_trailingDot && IsAsciiName(_pattern);
    }

    internal static GlobPattern ForName(string pattern, bool ignoreCase, bool useInvariantCulture, bool isFilePattern) =>
        new(pattern, ignoreCase, useInvariantCulture, isFilePattern);

    internal bool IsMatch(string input) => IsMatch(input.AsSpan());

    internal bool IsMatch(ReadOnlySpan<char> input)
    {
        if (_nameOnly && _ordinal && IsAsciiName(input))
        {
            return MatchOrdinal(input, _pattern);
        }

        int matchedFilename = -1;
        if (!_nameOnly && _ordinal)
        {
            int start = input.LastIndexOfAny('/', '\\') + 1;
            ReadOnlySpan<char> filename = input[start..];
            if (!filename.IsEmpty && IsAsciiName(filename))
            {
                if (!MatchOrdinal(filename, _pattern.AsSpan(_filenameStart)))
                {
                    return false;
                }

                matchedFilename = start;
            }
        }

        return Match(input, out _, out _, matchedFilename) >= 0;
    }

    internal void GetMatchInfo(string input, out bool isMatch, out string wildcardDirectory, out string filename)
    {
        int matchEnd = Match(input.AsSpan(), out int wildcardStart, out int filenameStart);
        isMatch = matchEnd >= 0;
        wildcardDirectory = isMatch ? input.Substring(wildcardStart, filenameStart - wildcardStart) : string.Empty;
        filename = isMatch ? input.Substring(filenameStart, matchEnd - filenameStart) : string.Empty;
    }

    private int Match(ReadOnlySpan<char> input, out int wildcardStart, out int filenameStart, int matchedFilename = -1)
    {
        using BufferScope<Backtrack> backtracks = new(stackalloc Backtrack[16]);
        int count = 0;
        int patternIndex = 0;
        int inputIndex = 0;
        wildcardStart = filenameStart = 0;

        while (true)
        {
            if (patternIndex == _wildcardStart)
            {
                wildcardStart = inputIndex;
            }

            if (patternIndex == _filenameStart)
            {
                filenameStart = inputIndex;
                if (matchedFilename >= 0)
                {
                    if (inputIndex == matchedFilename)
                    {
                        return input.Length;
                    }

                    goto Retry;
                }
            }

            if (patternIndex == _pattern.Length)
            {
                // Preserve the historical end anchor's acceptance of a final newline.
                if (inputIndex == input.Length || (inputIndex == input.Length - 1 && input[inputIndex] == '\n'))
                {
                    return inputIndex;
                }

                goto Retry;
            }

            char value = _pattern[patternIndex];
            bool inFilename = patternIndex >= _filenameStart;
            bool recursive = !_nameOnly && !inFilename && _pattern.AsSpan(patternIndex).StartsWith("**/");
            bool separator = !_nameOnly && value == '/';
            if (value == '*' || separator)
            {
                bool beforeRecursive = separator && patternIndex >= _wildcardStart
                    && _pattern.AsSpan(patternIndex + 1).StartsWith("**/");
                int maximum = inputIndex;
                while (maximum < input.Length && (!beforeRecursive || maximum == inputIndex)
                    && (recursive ? input[maximum] != '\n'
                        : separator ? FileUtilities.IsAnySlash(input[maximum])
                        : inFilename && _trailingDot ? input[maximum] != '.'
                        : _nameOnly || !FileUtilities.IsAnySlash(input[maximum])))
                {
                    maximum++;
                }

                Backtrack state = new()
                {
                    PatternIndex = patternIndex + (recursive ? 3 : 1),
                    Minimum = inputIndex + (separator ? 1 : 0),
                    Maximum = maximum,
                    Next = maximum,
                    Phase = recursive ? patternIndex == _wildcardStart ? 0 : -1 : 3,
                    WildcardStart = wildcardStart,
                    FilenameStart = filenameStart,
                };
                if (count == backtracks.Length)
                {
                    backtracks.EnsureCapacity(count + 1, copy: true);
                }

                backtracks[count++] = state;
            }
            else
            {
                // A trailing-dot '?' historically consumes two characters.
                int width = value == '?' && inFilename && _trailingDot ? 2 : 1;
                if (inputIndex + width <= input.Length && (value == '?'
                    ? input[inputIndex + width - 1] != '\n' && (width == 1 || input[inputIndex] != '.')
                    : value == input[inputIndex] || (_caseFolding is not null && _caseFolding.ToLower(value) == _caseFolding.ToLower(input[inputIndex]))))
                {
                    inputIndex += width;
                    patternIndex++;
                    continue;
                }
            }

        Retry:
            while (count > 0)
            {
                ref Backtrack state = ref backtracks[count - 1];
                if (TryNext(input, ref state, out inputIndex))
                {
                    patternIndex = state.PatternIndex;
                    wildcardStart = state.WildcardStart;
                    filenameStart = state.FilenameStart;
                    break;
                }

                count--;
            }

            if (count == 0)
            {
                return -1;
            }
        }
    }

    private static bool TryNext(ReadOnlySpan<char> input, ref Backtrack state, out int position)
    {
        if (state.Phase == 3)
        {
            position = state.Next--;
            return position >= state.Minimum;
        }

        if (state.Phase == -1)
        {
            state.Phase = 0;
            position = state.Minimum;
            return true;
        }

        // Preserve the recursive-directory alternative order when reporting captures.
        while (state.Phase < 2)
        {
            bool backslashFirst = state.Minimum > state.WildcardStart && input[state.Minimum - 1] == '\\';
            char separator = (state.Phase == 0) != backslashFirst ? '/' : '\\';
            int separatorIndex = input.Slice(state.Minimum, state.Next - state.Minimum).LastIndexOf(separator);
            if (separatorIndex >= 0)
            {
                position = state.Minimum + separatorIndex + 1;
                state.Next = position - 1;
                return true;
            }

            state.Phase++;
            state.Next = state.Maximum;
        }

        position = state.Minimum;
        state.Phase = 3;
        state.Next = state.Minimum - 1;
        return state.Minimum == state.WildcardStart;
    }

    private static void AppendDirectory(Span<char> destination, ref int length, string directory, bool wildcard, bool unc)
    {
        int index = unc ? FileMatcher.LastIndexOfDirectorySequence(directory, 0) + 1 : 0;
        while (index < directory.Length)
        {
            if (wildcard && index + 2 < directory.Length && directory[index] == '*' && directory[index + 1] == '*'
                && FileUtilities.IsAnySlash(directory[index + 2]))
            {
                "**/".AsSpan().CopyTo(destination[length..]);
                length += 3;
                index = FileMatcher.LastIndexOfDirectoryOrRecursiveSequence(directory, index);
            }
            else
            {
                index = FileMatcher.LastIndexOfDirectorySequence(directory, index);
                destination[length++] = FileUtilities.IsAnySlash(directory[index]) ? '/' : directory[index];
                index++;
            }
        }
    }

    private static string NormalizeFilename(string filename, bool trailingDot) =>
        trailingDot ? filename[..^1] : filename.Replace("*.*", "*");

    private static bool IsAsciiName(ReadOnlySpan<char> name)
    {
        foreach (char value in name)
        {
            if (value > 0x7F || value == '\n')
            {
                return false;
            }
        }

        return true;
    }

    internal static bool MatchesName(ReadOnlySpan<char> input, string pattern, bool ignoreCase = true)
    {
        if (ignoreCase && input == ReadOnlySpan<char>.Empty)
        {
            InternalError.Throw($"Unexpected empty '{nameof(input)}' provided.");
        }

        ArgumentNullException.ThrowIfNull(pattern);
        return MatchOrdinal(input, pattern, ignoreCase);
    }

    private static bool MatchOrdinal(ReadOnlySpan<char> input, ReadOnlySpan<char> pattern, bool ignoreCase = true)
    {
        // Check the fixed suffix first: "*.cs" need not scan an entire filename.
        while (!pattern.IsEmpty && pattern[^1] != '*')
        {
            if (input.IsEmpty || (pattern[^1] != '?' && !EqualsOrdinal(input[^1], pattern[^1], ignoreCase)))
            {
                return false;
            }

            input = input[..^1];
            pattern = pattern[..^1];
        }

        if (pattern.Length <= 1)
        {
            return !pattern.IsEmpty || input.IsEmpty;
        }

        int inputIndex = 0;
        int patternIndex = 0;
        int patternAfterStar = -1;
        int inputAfterStar = 0;
        while (inputIndex < input.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternAfterStar = ++patternIndex;
                inputAfterStar = inputIndex;
            }
            else if (patternIndex < pattern.Length && (pattern[patternIndex] == '?'
                || EqualsOrdinal(input[inputIndex], pattern[patternIndex], ignoreCase)))
            {
                inputIndex++;
                patternIndex++;
            }
            else if (patternAfterStar >= 0)
            {
                patternIndex = patternAfterStar;
                inputIndex = ++inputAfterStar;
            }
            else
            {
                return false;
            }
        }

        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool EqualsOrdinal(char left, char right, bool ignoreCase)
    {
        if (!ignoreCase)
        {
            return left == right;
        }

        char lower = (char)(left | 0x20);
        if (lower is >= 'a' and <= 'z')
        {
            return lower == (right | 0x20);
        }

        return left < 128 || right < 128 ? left == right
            : MemoryExtensions.Equals([left], [right], StringComparison.OrdinalIgnoreCase);
    }

    private struct Backtrack
    {
        internal int PatternIndex;
        internal int Minimum;
        internal int Maximum;
        internal int Next;
        internal int Phase;
        internal int WildcardStart;
        internal int FilenameStart;
    }
}
