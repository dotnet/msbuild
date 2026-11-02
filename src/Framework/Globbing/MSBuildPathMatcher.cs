// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

// Adapted from JeremyKuhne/touki at commit 9d925032a5e7d100c9380a7fe40b9ef64527bcab:
//   touki/Touki/Io/MatchMSBuild.cs
//   touki/Touki/Io/MatchMSBuild.PathMatchState.cs
//   touki/Touki/Io/MatchMSBuild.SpecSegment.cs
//   touki/Touki/Io/PathSegmentEnumerator.cs
//   touki/Touki/Io/ReversePathSegmentEnumerator.cs
//
// The matcher uses specialized paths for common globstar shapes and a bounded NFA-style state set
// for complex patterns. It is restricted to MSBuild syntax and uses FileMatcher's filename matching
// semantics.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Shared.Globbing;

/// <summary>
/// Describes how a directory pattern affects traversal.
/// </summary>
internal enum DirectoryMatchType : byte
{
    /// <summary>The directory can contain a match and should be traversed.</summary>
    MayContainMatchingFiles,

    /// <summary>The directory cannot contain a match.</summary>
    NoDescendantFilesMatch,

    /// <summary>Every file below the directory matches.</summary>
    AllDescendantFilesMatch,
}

/// <summary>
/// Incrementally matches the wildcard-directory and filename portions of an MSBuild file specification.
/// </summary>
internal sealed class MSBuildPathMatcher
{
    private const string RecursiveDirectoryMatch = "**";
    private const int StackStateCount = 64;

    private readonly string[] _directoryPatterns;
    private readonly bool[] _ignoreCaseDirectoryPatterns;
    private readonly Regex?[]? _cultureSensitiveDirectoryPatterns;
    private readonly string?[]? _win32DirectoryPatterns;
    private readonly bool[]? _enforceStrictWin32DirectoryMatch;
    private readonly string _filePattern;
    private readonly bool _ignoreCaseFilePattern;
    private readonly bool _useWin32FileNameMatch;
    private readonly bool _enforceStrictWin32Match;
    private readonly string? _win32FilePattern;
    private readonly bool _matchesAllFiles;
    private readonly Regex? _filePatternRegex;
    private readonly int _globstarCount;
    private readonly int _singleGlobstarIndex;
    private readonly bool _endsInGlobstar;

    /// <summary>
    /// Creates a matcher from the wildcard-directory and filename parts produced by <see cref="FileMatcher.GetFileSpecInfo"/>.
    /// </summary>
    internal MSBuildPathMatcher(
        string wildcardDirectoryPart,
        string filePattern,
        bool filesystemCaseSensitive = false,
        bool matchFileNameInternally = true,
        bool treatStarDotStarAsAllFiles = true,
        bool useTrailingDotRegex = true,
        bool useWin32FileNameMatch = false,
        bool useWin32DirectoryMatch = false,
        bool preserveLegacyRegexSemantics = false,
        bool useInvariantCulture = false)
    {
        ArgumentNullException.ThrowIfNull(wildcardDirectoryPart);
        ArgumentNullException.ThrowIfNull(filePattern);

        _directoryPatterns = SplitDirectoryPatterns(wildcardDirectoryPart);
        _ignoreCaseDirectoryPatterns = new bool[_directoryPatterns.Length];
        _cultureSensitiveDirectoryPatterns = preserveLegacyRegexSemantics
            ? new Regex?[_directoryPatterns.Length]
            : null;
        _win32DirectoryPatterns = useWin32DirectoryMatch
            ? new string?[_directoryPatterns.Length]
            : null;
        _enforceStrictWin32DirectoryMatch = useWin32DirectoryMatch
            ? new bool[_directoryPatterns.Length]
            : null;

        bool globstarSeen = false;
        int globstarCount = 0;
        int singleGlobstarIndex = -1;
        for (int index = 0; index < _directoryPatterns.Length; index++)
        {
            _ignoreCaseDirectoryPatterns[index] = !filesystemCaseSensitive || globstarSeen;
            if (preserveLegacyRegexSemantics && _directoryPatterns[index] != RecursiveDirectoryMatch)
            {
                _cultureSensitiveDirectoryPatterns![index] = CreateCultureSensitiveDirectoryRegex(
                    _directoryPatterns[index],
                    _ignoreCaseDirectoryPatterns[index],
                    useInvariantCulture);
            }

            if (useWin32DirectoryMatch
                && !globstarSeen
                && _directoryPatterns[index] != RecursiveDirectoryMatch)
            {
                _win32DirectoryPatterns![index] = FileMatcher.TranslateWin32Expression(_directoryPatterns[index]);
                _enforceStrictWin32DirectoryMatch![index] = FileMatcher.ShouldEnforceMatching(_directoryPatterns[index]);
            }

            if (_directoryPatterns[index] == RecursiveDirectoryMatch)
            {
                globstarSeen = true;
                globstarCount++;
                singleGlobstarIndex = index;
            }
        }

        _globstarCount = globstarCount;
        _singleGlobstarIndex = singleGlobstarIndex;
        _endsInGlobstar = _directoryPatterns.Length > 0
            && _directoryPatterns[^1] == RecursiveDirectoryMatch;

        _filePattern = filePattern;
        _ignoreCaseFilePattern = !filesystemCaseSensitive || matchFileNameInternally;
        _useWin32FileNameMatch = useWin32FileNameMatch;
        _enforceStrictWin32Match = useWin32FileNameMatch
            && FileMatcher.ShouldEnforceMatching(filePattern);
        _win32FilePattern = useWin32FileNameMatch
            ? FileMatcher.TranslateWin32Expression(filePattern)
            : null;
        _matchesAllFiles = filePattern.Length == 0
            || filePattern == "*"
            || (treatStarDotStarAsAllFiles && filePattern == "*.*");
        if (preserveLegacyRegexSemantics)
        {
            _filePatternRegex = CreateCultureSensitiveFileRegex(filePattern, useInvariantCulture);
        }
        else
        {
            _filePatternRegex = useTrailingDotRegex && filePattern.EndsWith(".", StringComparison.Ordinal)
                ? new Regex(
                    FileMatcher.RegularExpressionFromFileSpec(string.Empty, string.Empty, filePattern),
                    GetRegexOptions(_ignoreCaseFilePattern, useInvariantCulture))
                : null;
        }
    }

    /// <summary>
    /// Returns whether a file in <paramref name="relativeDirectory"/> matches this specification.
    /// </summary>
    internal bool MatchesFile(ReadOnlySpan<char> relativeDirectory, ReadOnlySpan<char> fileName)
    {
        return GetDirectoryState(relativeDirectory, out bool directoryMatches, out _, out _)
            && directoryMatches
            && MatchesFileName(fileName);
    }

    /// <summary>
    /// Returns whether a file path in <paramref name="relativeDirectory"/> matches this specification.
    /// </summary>
    internal bool MatchesFile(ReadOnlySpan<char> relativeDirectory, string filePath)
    {
        return GetDirectoryState(relativeDirectory, out bool directoryMatches, out _, out _)
            && directoryMatches
            && MatchesFileName(filePath);
    }

    /// <summary>
    /// Returns whether files in <paramref name="relativeDirectory"/> can match this specification.
    /// </summary>
    internal bool MatchesFilesInDirectory(ReadOnlySpan<char> relativeDirectory)
    {
        return GetDirectoryState(relativeDirectory, out bool directoryMatches, out _, out _)
            && directoryMatches;
    }

    /// <summary>
    /// Returns whether a filename matches this specification after its directory has been matched.
    /// </summary>
    internal bool MatchesFileName(ReadOnlySpan<char> fileName) =>
        _matchesAllFiles || MatchesFileNameCore(fileName);

    /// <summary>
    /// Returns whether the filename portion of a path matches this specification after its directory has been matched.
    /// </summary>
    internal bool MatchesFileName(string filePath) => MatchesFileName(GetFileName(filePath));

    /// <summary>
    /// Returns whether a descendant of <paramref name="relativeDirectory"/> can match this specification.
    /// </summary>
    internal bool CanMatchDescendants(ReadOnlySpan<char> relativeDirectory)
    {
        return GetDirectoryState(relativeDirectory, out _, out _, out bool canMatchDescendants)
            && canMatchDescendants;
    }

    /// <summary>
    /// Describes whether files below a candidate directory can match this specification.
    /// </summary>
    internal DirectoryMatchType MatchesDirectory(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName)
    {
        if (!GetDirectoryState(
                currentDirectory,
                directoryName,
                out _,
                out bool terminalGlobstarIsActive,
                out _))
        {
            return DirectoryMatchType.NoDescendantFilesMatch;
        }

        return _matchesAllFiles && terminalGlobstarIsActive
            ? DirectoryMatchType.AllDescendantFilesMatch
            : DirectoryMatchType.MayContainMatchingFiles;
    }

    private bool GetDirectoryState(
        ReadOnlySpan<char> relativeDirectory,
        out bool directoryMatches,
        out bool terminalGlobstarIsActive,
        out bool canMatchDescendants)
    {
        PathSegmentEnumerator segments = new(relativeDirectory);
        return GetDirectoryState(
            ref segments,
            out directoryMatches,
            out terminalGlobstarIsActive,
            out canMatchDescendants);
    }

    private bool GetDirectoryState(
        ReadOnlySpan<char> currentDirectory,
        ReadOnlySpan<char> directoryName,
        out bool directoryMatches,
        out bool terminalGlobstarIsActive,
        out bool canMatchDescendants)
    {
        PathSegmentEnumerator segments = new(currentDirectory, directoryName);
        return GetDirectoryState(
            ref segments,
            out directoryMatches,
            out terminalGlobstarIsActive,
            out canMatchDescendants);
    }

    private bool GetDirectoryState(
        ref PathSegmentEnumerator segments,
        out bool directoryMatches,
        out bool terminalGlobstarIsActive,
        out bool canMatchDescendants)
    {
        canMatchDescendants = false;
        PathMatchState state = _globstarCount switch
        {
            0 => MatchWithoutGlobstar(ref segments),
            1 => MatchSingleGlobstar(ref segments),
            2 when _directoryPatterns.Length == 3
                && _directoryPatterns[0] == RecursiveDirectoryMatch
                && _directoryPatterns[2] == RecursiveDirectoryMatch => MatchGlobstarAnchor(ref segments),
            _ => MatchMultipleGlobstars(ref segments, out canMatchDescendants),
        };

        directoryMatches = state == PathMatchState.FullMatch;
        terminalGlobstarIsActive = directoryMatches && _endsInGlobstar;
        if (_globstarCount == 0)
        {
            canMatchDescendants = state == PathMatchState.PartialMatch;
        }
        else if (_globstarCount <= 2
            && (_globstarCount == 1
                || (_directoryPatterns.Length == 3
                    && _directoryPatterns[0] == RecursiveDirectoryMatch
                    && _directoryPatterns[2] == RecursiveDirectoryMatch)))
        {
            canMatchDescendants = state != PathMatchState.NoMatch;
        }

        return state != PathMatchState.NoMatch;
    }

    private PathMatchState MatchWithoutGlobstar(ref PathSegmentEnumerator segments)
    {
        int patternIndex = 0;
        while (segments.MoveNext())
        {
            if (patternIndex >= _directoryPatterns.Length
                || !MatchesDirectoryName(segments.Current, patternIndex))
            {
                return PathMatchState.NoMatch;
            }

            patternIndex++;
        }

        return patternIndex == _directoryPatterns.Length
            ? PathMatchState.FullMatch
            : PathMatchState.PartialMatch;
    }

    private PathMatchState MatchSingleGlobstar(ref PathSegmentEnumerator segments)
    {
        if (_singleGlobstarIndex == 0)
        {
            ReversePathSegmentEnumerator reversePath = new(segments.FirstPath, segments.SecondPath);
            for (int patternIndex = _directoryPatterns.Length - 1; patternIndex > 0; patternIndex--)
            {
                if (!reversePath.MovePrevious()
                    || !MatchesDirectoryName(reversePath.Current, patternIndex))
                {
                    return PathMatchState.PartialMatch;
                }
            }

            return PathMatchState.FullMatch;
        }

        if (_singleGlobstarIndex == _directoryPatterns.Length - 1)
        {
            for (int patternIndex = 0; patternIndex < _singleGlobstarIndex; patternIndex++)
            {
                if (!segments.MoveNext())
                {
                    return PathMatchState.PartialMatch;
                }

                if (!MatchesDirectoryName(segments.Current, patternIndex))
                {
                    return PathMatchState.NoMatch;
                }
            }

            return PathMatchState.FullMatch;
        }

        PathSegmentEnumerator counter = segments;
        int pathSegmentCount = 0;
        while (counter.MoveNext())
        {
            pathSegmentCount++;
        }

        for (int patternIndex = 0; patternIndex < _singleGlobstarIndex; patternIndex++)
        {
            if (!segments.MoveNext())
            {
                return PathMatchState.PartialMatch;
            }

            if (!MatchesDirectoryName(segments.Current, patternIndex))
            {
                return PathMatchState.NoMatch;
            }
        }

        int suffixSegmentCount = _directoryPatterns.Length - _singleGlobstarIndex - 1;
        int remainingPathSegmentCount = pathSegmentCount - _singleGlobstarIndex;
        if (remainingPathSegmentCount < suffixSegmentCount)
        {
            return PathMatchState.PartialMatch;
        }

        int globstarSegmentCount = remainingPathSegmentCount - suffixSegmentCount;
        for (int index = 0; index < globstarSegmentCount; index++)
        {
            segments.MoveNext();
        }

        for (int suffixIndex = 0; suffixIndex < suffixSegmentCount; suffixIndex++)
        {
            segments.MoveNext();
            if (!MatchesDirectoryName(
                    segments.Current,
                    _singleGlobstarIndex + suffixIndex + 1))
            {
                return PathMatchState.PartialMatch;
            }
        }

        return PathMatchState.FullMatch;
    }

    private PathMatchState MatchGlobstarAnchor(ref PathSegmentEnumerator segments)
    {
        while (segments.MoveNext())
        {
            if (MatchesDirectoryName(segments.Current, patternIndex: 1))
            {
                return PathMatchState.FullMatch;
            }
        }

        return PathMatchState.PartialMatch;
    }

    private PathMatchState MatchMultipleGlobstars(
        ref PathSegmentEnumerator segments,
        out bool canMatchDescendants)
    {
        int stateCount = _directoryPatterns.Length + 1;
        using BufferScope<byte> stateBuffer = new(stackalloc byte[StackStateCount * 2], checked(stateCount * 2));
        Span<byte> current = stateBuffer.Slice(0, stateCount);
        Span<byte> next = stateBuffer.Slice(stateCount, stateCount);
        current.Clear();
        next.Clear();

        current[0] = 1;
        AddGlobstarEpsilonTransitions(current);

        while (segments.MoveNext())
        {
            ReadOnlySpan<char> segment = segments.Current;
            next.Clear();
            bool hasActiveState = false;

            for (int patternIndex = 0; patternIndex < _directoryPatterns.Length; patternIndex++)
            {
                if (current[patternIndex] == 0)
                {
                    continue;
                }

                string pattern = _directoryPatterns[patternIndex];
                if (pattern == RecursiveDirectoryMatch)
                {
                    next[patternIndex] = 1;
                    hasActiveState = true;
                }
                else if (MatchesDirectoryName(segment, patternIndex))
                {
                    next[patternIndex + 1] = 1;
                    hasActiveState = true;
                }
            }

            if (!hasActiveState)
            {
                canMatchDescendants = false;
                return PathMatchState.NoMatch;
            }

            AddGlobstarEpsilonTransitions(next);

            Span<byte> swap = current;
            current = next;
            next = swap;
        }

        canMatchDescendants = false;
        for (int patternIndex = 0; patternIndex < _directoryPatterns.Length; patternIndex++)
        {
            if (current[patternIndex] != 0)
            {
                canMatchDescendants = true;
                break;
            }
        }

        if (current[_directoryPatterns.Length] != 0)
        {
            return PathMatchState.FullMatch;
        }

        return canMatchDescendants
            ? PathMatchState.PartialMatch
            : PathMatchState.NoMatch;
    }

    private void AddGlobstarEpsilonTransitions(Span<byte> states)
    {
        for (int patternIndex = 0; patternIndex < _directoryPatterns.Length; patternIndex++)
        {
            if (states[patternIndex] != 0
                && _directoryPatterns[patternIndex] == RecursiveDirectoryMatch)
            {
                states[patternIndex + 1] = 1;
            }
        }
    }

    private enum PathMatchState : byte
    {
        NoMatch,
        PartialMatch,
        FullMatch,
    }

    private static ReadOnlySpan<char> GetFileName(string path)
    {
        ReadOnlySpan<char> span = path.AsSpan();
        for (int index = span.Length - 1; index >= 0; index--)
        {
            if (IsCandidateDirectorySeparator(span[index]))
            {
                return span[(index + 1)..];
            }
        }

        return span;
    }

    private static bool MatchesName(
        ReadOnlySpan<char> name,
        string pattern,
        bool ignoreCase,
        Regex? regex)
    {
        if (regex is not null)
        {
#if NET
            return regex.IsMatch(name);
#else
            return regex.IsMatch(name.ToString());
#endif
        }

        if (ignoreCase)
        {
            return FileMatcher.IsMatch(name, pattern);
        }

        int nameIndex = 0;
        int patternIndex = 0;
        int patternAfterStar = -1;
        int nameAfterStar = -1;

        while (nameIndex < name.Length)
        {
            if (patternIndex < pattern.Length
                && (pattern[patternIndex] == '?' || pattern[patternIndex] == name[nameIndex]))
            {
                patternIndex++;
                nameIndex++;
            }
            else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternAfterStar = ++patternIndex;
                nameAfterStar = nameIndex;
            }
            else if (patternAfterStar >= 0)
            {
                patternIndex = patternAfterStar;
                nameIndex = ++nameAfterStar;
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

    private bool MatchesFileNameCore(ReadOnlySpan<char> fileName)
    {
        if (!_useWin32FileNameMatch)
        {
            return MatchesName(fileName, _filePattern, _ignoreCaseFilePattern, _filePatternRegex);
        }

        return FileMatcher.IsWin32FileNameMatch(fileName, _win32FilePattern!)
            && (!_enforceStrictWin32Match || FileMatcher.IsMatch(fileName, _filePattern));
    }

    private bool MatchesDirectoryName(ReadOnlySpan<char> directoryName, int patternIndex)
    {
        Regex? cultureSensitivePattern = _cultureSensitiveDirectoryPatterns?[patternIndex];
        if (cultureSensitivePattern is not null)
        {
            return MatchesName(
                directoryName,
                _directoryPatterns[patternIndex],
                ignoreCase: true,
                cultureSensitivePattern);
        }

        string? win32Pattern = _win32DirectoryPatterns?[patternIndex];
        if (win32Pattern is null)
        {
            return MatchesName(
                directoryName,
                _directoryPatterns[patternIndex],
                _ignoreCaseDirectoryPatterns[patternIndex],
                regex: null);
        }

        return FileMatcher.IsWin32FileNameMatch(directoryName, win32Pattern)
            && (!_enforceStrictWin32DirectoryMatch![patternIndex]
                || FileMatcher.IsMatch(directoryName, _directoryPatterns[patternIndex]));
    }

    private static Regex CreateCultureSensitiveDirectoryRegex(
        string pattern,
        bool ignoreCase,
        bool useInvariantCulture)
    {
        StringBuilder expression = new(pattern.Length + 8);
        expression.Append('^');

        foreach (char value in pattern)
        {
            if (value == '*')
            {
                expression.Append("[\\s\\S]*");
            }
            else if (value == '?')
            {
                expression.Append('.');
            }
            else
            {
                AppendRegexLiteral(expression, value);
            }
        }

        expression.Append('$');
        return new Regex(expression.ToString(), GetRegexOptions(ignoreCase, useInvariantCulture));
    }

    private static Regex CreateCultureSensitiveFileRegex(string pattern, bool useInvariantCulture)
    {
        StringBuilder expression = new(pattern.Length + 8);
        expression.Append('^');
        bool hasTrailingDot = pattern.EndsWith(".", StringComparison.Ordinal);
        int patternLength = hasTrailingDot ? pattern.Length - 1 : pattern.Length;

        for (int index = 0; index < patternLength; index++)
        {
            char value = pattern[index];
            if (value == '*')
            {
                expression.Append(hasTrailingDot ? "[^.]*" : "[\\s\\S]*");
            }
            else if (value == '?')
            {
                expression.Append(hasTrailingDot ? "[^.]." : ".");
            }
            else
            {
                AppendRegexLiteral(expression, value);
            }

            if (!hasTrailingDot
                && index < patternLength - 2
                && value == '*'
                && pattern[index + 1] == '.'
                && pattern[index + 2] == '*')
            {
                index += 2;
            }
        }

        expression.Append('$');
        return new Regex(expression.ToString(), GetRegexOptions(ignoreCase: true, useInvariantCulture));
    }

    private static RegexOptions GetRegexOptions(bool ignoreCase, bool useInvariantCulture)
    {
        RegexOptions options = ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None;
        return useInvariantCulture ? options | RegexOptions.CultureInvariant : options;
    }

    private static void AppendRegexLiteral(StringBuilder expression, char value)
    {
        if (value is '\\' or '.' or '$' or '^' or '{' or '[' or '(' or '|' or ')' or '+' or ']')
        {
            expression.Append('\\');
        }

        expression.Append(value);
    }

    private static string[] SplitDirectoryPatterns(string wildcardDirectoryPart)
    {
        if (wildcardDirectoryPart.Length == 0)
        {
            return [];
        }

        List<string> patterns = [];
        PathSegmentEnumerator segments = new(
            wildcardDirectoryPart,
            matchBothDirectorySeparators: true);

        while (segments.MoveNext())
        {
            ReadOnlySpan<char> segment = segments.Current;
            if (segment.SequenceEqual(".")
                && (patterns.Count == 0 || patterns[^1] == RecursiveDirectoryMatch))
            {
                continue;
            }

            if (segment.SequenceEqual(RecursiveDirectoryMatch)
                && patterns.Count > 0
                && patterns[^1] == RecursiveDirectoryMatch)
            {
                continue;
            }

            patterns.Add(segment.ToString());
        }

        return [.. patterns];
    }

    /// <summary>
    /// Walks non-empty path segments in a physical candidate path or an MSBuild specification.
    /// </summary>
    private ref struct PathSegmentEnumerator
    {
        private readonly ReadOnlySpan<char> _firstPath;
        private readonly ReadOnlySpan<char> _secondPath;
        private readonly bool _matchBothDirectorySeparators;
        private ReadOnlySpan<char> _current;
        private int _position;
        private bool _readingSecondPath;

        internal PathSegmentEnumerator(
            ReadOnlySpan<char> path,
            bool matchBothDirectorySeparators = false)
            : this(path, default, matchBothDirectorySeparators)
        {
        }

        internal PathSegmentEnumerator(
            ReadOnlySpan<char> firstPath,
            ReadOnlySpan<char> secondPath)
            : this(firstPath, secondPath, matchBothDirectorySeparators: false)
        {
        }

        private PathSegmentEnumerator(
            ReadOnlySpan<char> firstPath,
            ReadOnlySpan<char> secondPath,
            bool matchBothDirectorySeparators)
        {
            _firstPath = firstPath;
            _secondPath = secondPath;
            _matchBothDirectorySeparators = matchBothDirectorySeparators;
        }

        internal readonly ReadOnlySpan<char> Current => _current;
        internal readonly ReadOnlySpan<char> FirstPath => _firstPath;
        internal readonly ReadOnlySpan<char> SecondPath => _secondPath;

        internal bool MoveNext()
        {
            while (true)
            {
                ReadOnlySpan<char> path = _readingSecondPath ? _secondPath : _firstPath;
                while (_position < path.Length && IsDirectorySeparator(path[_position]))
                {
                    _position++;
                }

                if (_position < path.Length)
                {
                    int start = _position;
                    while (_position < path.Length && !IsDirectorySeparator(path[_position]))
                    {
                        _position++;
                    }

                    _current = path[start.._position];
                    return true;
                }

                if (_readingSecondPath)
                {
                    _current = default;
                    return false;
                }

                _readingSecondPath = true;
                _position = 0;
            }
        }

        private readonly bool IsDirectorySeparator(char value) =>
            _matchBothDirectorySeparators
                ? value is '/' or '\\'
                : IsCandidateDirectorySeparator(value);
    }

    private ref struct ReversePathSegmentEnumerator
    {
        private readonly ReadOnlySpan<char> _firstPath;
        private readonly ReadOnlySpan<char> _secondPath;
        private ReadOnlySpan<char> _current;
        private int _position;
        private bool _readingSecondPath;

        internal ReversePathSegmentEnumerator(
            ReadOnlySpan<char> firstPath,
            ReadOnlySpan<char> secondPath)
        {
            _firstPath = firstPath;
            _secondPath = secondPath;
            _readingSecondPath = !secondPath.IsEmpty;
            _position = _readingSecondPath ? secondPath.Length : firstPath.Length;
        }

        internal readonly ReadOnlySpan<char> Current => _current;

        internal bool MovePrevious()
        {
            while (true)
            {
                ReadOnlySpan<char> path = _readingSecondPath ? _secondPath : _firstPath;
                int end = _position;
                while (end > 0 && IsDirectorySeparator(path[end - 1]))
                {
                    end--;
                }

                if (end == 0)
                {
                    if (_readingSecondPath)
                    {
                        _readingSecondPath = false;
                        _position = _firstPath.Length;
                        continue;
                    }

                    _current = default;
                    return false;
                }

                int start = end - 1;
                while (start >= 0 && !IsDirectorySeparator(path[start]))
                {
                    start--;
                }

                _current = path[(start + 1)..end];
                _position = start < 0 ? 0 : start;
                return true;
            }
        }

        private static bool IsDirectorySeparator(char value) => IsCandidateDirectorySeparator(value);
    }

    private static bool IsCandidateDirectorySeparator(char value) =>
        value == Path.DirectorySeparatorChar || value == Path.AltDirectorySeparatorChar;
}