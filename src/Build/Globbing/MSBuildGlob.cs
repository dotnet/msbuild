// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using System.Globalization;
using System.IO;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.Globbing;
using Microsoft.NET.StringTools;

#nullable disable

namespace Microsoft.Build.Globbing
{
    /// <summary>
    ///     Represents a parsed MSBuild glob.
    ///     An MSBuild glob is composed of three parts:
    ///     - fixed directory part: "a/b/" in "a/b/**/*test*/**/*.cs"
    ///     - wildcard directory part: "**/*test*/**/" in "a/b/**/*test*/**/*.cs"
    ///     - file name part: "*.cs" in "a/b/**/*test*/**/*.cs"
    /// </summary>
    public class MSBuildGlob : IMSBuildGlob
    {
        private readonly struct GlobState
        {
            public string GlobRoot { get; }
            public string FileSpec { get; }
            public bool IsLegal { get; }
            public string FixedDirectoryPart { get; }
            public string WildcardDirectoryPart { get; }
            public string FilenamePart { get; }
            public bool NeedsRecursion { get; }
            public GlobPattern Matcher { get; }

            public GlobState(string globRoot, string fileSpec, bool isLegal, string fixedDirectoryPart, string wildcardDirectoryPart, string filenamePart, bool needsRecursion, GlobPattern matcher)
            {
                GlobRoot = globRoot;
                FileSpec = fileSpec;
                IsLegal = isLegal;
                FixedDirectoryPart = fixedDirectoryPart;
                WildcardDirectoryPart = wildcardDirectoryPart;
                FilenamePart = filenamePart;
                NeedsRecursion = needsRecursion;
                Matcher = matcher;
            }
        }

        private static readonly WeakValueDictionary<string, GlobPattern> s_matcherCache = new();

        private readonly Lazy<GlobState> _state;

        internal string TestOnlyGlobRoot => _state.Value.GlobRoot;
        internal string TestOnlyFileSpec => _state.Value.FileSpec;
        internal bool TestOnlyNeedsRecursion => _state.Value.NeedsRecursion;
        internal GlobPattern TestOnlyMatcher => _state.Value.Matcher;

        /// <summary>
        ///     The fixed directory part.
        /// </summary>
        public string FixedDirectoryPart => _state.Value.FixedDirectoryPart;

        /// <summary>
        ///     The wildcard directory part
        /// </summary>
        public string WildcardDirectoryPart => _state.Value.WildcardDirectoryPart;

        /// <summary>
        ///     The file name part
        /// </summary>
        public string FilenamePart => _state.Value.FilenamePart;

        /// <summary>
        ///     Whether the glob was parsed sucsesfully from a string.
        ///     Illegal glob strings contain:
        ///     - invalid path characters (other than the wildcard characters themselves)
        ///     - "..."
        ///     - ":"
        ///     In addition, the wildcard directory part:
        ///     - cannot contain ".."
        ///     - if ** is present it must appear alone between slashes
        /// </summary>
        public bool IsLegal => _state.Value.IsLegal;

        private MSBuildGlob(Lazy<GlobState> state)
        {
            this._state = state;
        }

        /// <inheritdoc />
        public bool IsMatch(string stringToMatch)
        {
            ArgumentNullException.ThrowIfNull(stringToMatch);

            if (!IsLegal)
            {
                return false;
            }

            if (FileUtilities.PathIsInvalid(stringToMatch))
            {
                return false;
            }

            var normalizedString = NormalizeMatchInput(stringToMatch);

            return _state.Value.Matcher.IsMatch(normalizedString);
        }

        /// <summary>
        ///     Similar to <see cref="IsMatch" /> but also provides the match groups for the glob parts
        /// </summary>
        /// <param name="stringToMatch"></param>
        /// <returns></returns>
        public MatchInfoResult MatchInfo(string stringToMatch)
        {
            ArgumentNullException.ThrowIfNull(stringToMatch);

            if (FileUtilities.PathIsInvalid(stringToMatch) || !IsLegal)
            {
                return MatchInfoResult.Empty;
            }

            string normalizedInput = NormalizeMatchInput(stringToMatch);

            _state.Value.Matcher.GetMatchInfo(
                normalizedInput,
                out bool isMatch,
                out string wildcardDirectoryPart,
                out string filenamePart);

            // The fixed directory part can be inferred from the other two captures.
            int fixedDirectoryPartLength = normalizedInput.Length - wildcardDirectoryPart.Length - filenamePart.Length;
            string fixedDirectoryPart = normalizedInput.Substring(0, fixedDirectoryPartLength);

            return new MatchInfoResult(isMatch, fixedDirectoryPart, wildcardDirectoryPart, filenamePart);
        }

        private string NormalizeMatchInput(string stringToMatch)
        {
            var rootedInput = Path.Combine(_state.Value.GlobRoot, stringToMatch);
            var normalizedInput = FileUtilities.GetFullPathNoThrow(rootedInput);

            // Degenerate case when the string to match is empty.
            // Ensure trailing slash because the fixed directory part has a trailing slash.
            if (string.IsNullOrEmpty(stringToMatch))
            {
                normalizedInput += Path.DirectorySeparatorChar;
            }

            return normalizedInput;
        }

        /// <summary>
        ///     Parse the given <paramref name="fileSpec" /> into a <see cref="MSBuildGlob" /> using a given
        ///     <paramref name="globRoot" />.
        /// </summary>
        /// <param name="globRoot">
        ///     The root of the glob.
        ///     The fixed directory part of the glob and the match arguments (<see cref="IsMatch" /> and <see cref="MatchInfo" />)
        ///     will get normalized against this root.
        ///     If empty, the current working directory is used.
        ///     Cannot be null, and cannot contain invalid path arguments.
        /// </param>
        /// <param name="fileSpec">The string to parse</param>
        /// <returns></returns>
        public static MSBuildGlob Parse(string globRoot, string fileSpec)
        {
            ArgumentNullException.ThrowIfNull(globRoot);
            ArgumentNullException.ThrowIfNull(fileSpec);
            ErrorUtilities.VerifyThrowArgumentInvalidPath(globRoot, nameof(globRoot));

            if (string.IsNullOrEmpty(globRoot))
            {
                globRoot = Directory.GetCurrentDirectory();
            }

            globRoot = Strings.WeakIntern(FileUtilities.NormalizePath(globRoot).WithTrailingSlash());

            var lazyState = new Lazy<GlobState>(() =>
            {
                FileMatcher.Default.GetFileSpecInfo(
                    fileSpec,
                    out string fixedDirectoryPart,
                    out string wildcardDirectoryPart,
                    out string filenamePart,
                    out bool needsRecursion,
                    out bool isLegalFileSpec,
                    (fixedDirPart, wildcardDirPart, filePart) =>
                    {
                        var normalizedFixedPart = NormalizeTheFixedDirectoryPartAgainstTheGlobRoot(fixedDirPart, globRoot);

                        return (normalizedFixedPart, wildcardDirPart, filePart);
                    });

                GlobPattern matcher = null;
                if (isLegalFileSpec)
                {
                    bool useInvariantCulture = ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave18_11)
                        && !Traits.Instance.UseLegacyCultureSensitiveFileGlobs;
                    string patternKey = $"{fixedDirectoryPart.Length}:{fixedDirectoryPart}{wildcardDirectoryPart.Length}:{wildcardDirectoryPart}{filenamePart}";
                    string matcherCacheKey = useInvariantCulture
                        ? $"i;{patternKey}"
                        : $"c:{CultureInfo.CurrentCulture.Name};{patternKey}";

                    lock (s_matcherCache)
                    {
                        s_matcherCache.TryGetValue(matcherCacheKey, out matcher);
                    }

                    if (matcher is null)
                    {
                        GlobPattern newMatcher = new(fixedDirectoryPart, wildcardDirectoryPart, filenamePart, useInvariantCulture);
                        lock (s_matcherCache)
                        {
                            if (!s_matcherCache.TryGetValue(matcherCacheKey, out matcher))
                            {
                                s_matcherCache[matcherCacheKey] = newMatcher;
                            }
                        }
                        matcher ??= newMatcher;
                    }
                }
                return new GlobState(globRoot, fileSpec, isLegalFileSpec, fixedDirectoryPart, wildcardDirectoryPart, filenamePart, needsRecursion, matcher);
            },
            true);

            return new MSBuildGlob(lazyState);
        }

        private static string NormalizeTheFixedDirectoryPartAgainstTheGlobRoot(string fixedDirPart, string globRoot)
        {
            // todo: glob normalization is duplicated with FileSpecMatcherTester
            // concatenate the glob parent to the fixed dir part
            var parentedFixedPart = Path.Combine(globRoot, fixedDirPart);
            var normalizedFixedPart = FileUtilities.GetFullPathNoThrow(parentedFixedPart);
            return normalizedFixedPart.WithTrailingSlash();
        }

        /// <summary>
        ///     See <see cref="Parse(string,string)" />.
        ///     The glob root, if the glob is not fully qualified, will be the current working directory.
        /// </summary>
        /// <param name="fileSpec"></param>
        /// <returns></returns>
        public static MSBuildGlob Parse(string fileSpec)
        {
            return Parse(string.Empty, fileSpec);
        }

        /// <summary>
        ///     Return type of <see cref="MSBuildGlob.MatchInfo" />
        /// </summary>
        public readonly struct MatchInfoResult
        {
            /// <summary>
            ///     Whether the <see cref="MSBuildGlob.MatchInfo" /> argument was matched against the glob
            /// </summary>
            public bool IsMatch { get; }

            /// <summary>
            ///     The fixed directory part match
            /// </summary>
            public string FixedDirectoryPartMatchGroup { get; }

            /// <summary>
            ///     The wildcard directory part match
            /// </summary>
            public string WildcardDirectoryPartMatchGroup { get; }

            /// <summary>
            ///     The file name part match
            /// </summary>
            public string FilenamePartMatchGroup { get; }

            internal static MatchInfoResult Empty
                => new MatchInfoResult(false, string.Empty, string.Empty, string.Empty);

            internal MatchInfoResult(
                bool isMatch,
                string fixedDirectoryPartMatchGroup,
                string wildcardDirectoryPartMatchGroup,
                string filenamePartMatchGroup)
            {
                IsMatch = isMatch;
                FixedDirectoryPartMatchGroup = fixedDirectoryPartMatchGroup;
                WildcardDirectoryPartMatchGroup = wildcardDirectoryPartMatchGroup;
                FilenamePartMatchGroup = filenamePartMatchGroup;
            }
        }
    }
}
