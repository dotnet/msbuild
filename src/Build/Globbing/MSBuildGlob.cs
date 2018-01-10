// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------


using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Build.Shared;

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
        private struct GlobState
        {
            public string GlobRoot { get; }
            public string FileSpec { get; }
            public bool IsLegal { get; }
            public string FixedDirectoryPart { get; }
            public string WildcardDirectoryPart { get; }
            public string FilenamePart { get; }
            public string MatchFileExpression { get; }
            public bool NeedsRecursion { get; }
            public Regex Regex { get; }

            public GlobState(string globRoot, string fileSpec, bool isLegal, string fixedDirectoryPart, string wildcardDirectoryPart, string filenamePart, string matchFileExpression, bool needsRecursion, Regex regex)
            {
                GlobRoot = globRoot;
                FileSpec = fileSpec;
                IsLegal = isLegal;
                FixedDirectoryPart = fixedDirectoryPart;
                WildcardDirectoryPart = wildcardDirectoryPart;
                FilenamePart = filenamePart;
                MatchFileExpression = matchFileExpression;
                NeedsRecursion = needsRecursion;
                Regex = regex;
            }
        }

        private readonly Lazy<GlobState> _state;

        internal string TestOnlyGlobRoot => _state.Value.GlobRoot;
        internal string TestOnlyFileSpec => _state.Value.FileSpec;
        internal bool TestOnlyNeedsRecursion => _state.Value.NeedsRecursion;

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
            ErrorUtilities.VerifyThrowArgumentNull(stringToMatch, nameof(stringToMatch));

            if (!IsLegal)
            {
                return false;
            }

            if (FileUtilities.PathIsInvalid(stringToMatch))
            {
                return false;
            }

            var normalizedString = NormalizeMatchInput(stringToMatch);

            return _state.Value.Regex.IsMatch(normalizedString);
        }

        /// <summary>
        ///     Similar to <see cref="IsMatch" /> but also provides the match groups for the glob parts
        /// </summary>
        /// <param name="stringToMatch"></param>
        /// <returns></returns>
        public MatchInfoResult MatchInfo(string stringToMatch)
        {
            ErrorUtilities.VerifyThrowArgumentNull(stringToMatch, nameof(stringToMatch));

            if (FileUtilities.PathIsInvalid(stringToMatch) ||
                !IsLegal)
            {
                return MatchInfoResult.Empty;
            }

            var normalizedInput = NormalizeMatchInput(stringToMatch);

            bool isMatch;
            string fixedDirectoryPart, wildcardDirectoryPart, filenamePart;
            FileMatcher.GetRegexMatchInfo(
                normalizedInput,
                _state.Value.Regex,
                out isMatch,
                out fixedDirectoryPart,
                out wildcardDirectoryPart,
                out filenamePart);

            return new MatchInfoResult(isMatch, fixedDirectoryPart, wildcardDirectoryPart, filenamePart);
        }

        private string NormalizeMatchInput(string stringToMatch)
        {
            var rootedInput = Path.Combine(_state.Value.GlobRoot, stringToMatch);
            var normalizedInput = FileUtilities.GetFullPathNoThrow(rootedInput);

            // Degenerate case when the string to match is empty.
            // Ensure trailing slash because the fixed directory part has a trailing slash.
            if (stringToMatch == string.Empty)
            {
                normalizedInput = normalizedInput + Path.DirectorySeparatorChar;
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
            ErrorUtilities.VerifyThrowArgumentNull(globRoot, nameof(globRoot));
            ErrorUtilities.VerifyThrowArgumentNull(fileSpec, nameof(fileSpec));
            ErrorUtilities.VerifyThrowArgumentInvalidPath(globRoot, nameof(globRoot));

            if (globRoot == string.Empty)
            {
                globRoot = Directory.GetCurrentDirectory();
            }

            globRoot = FileUtilities.NormalizePath(globRoot).WithTrailingSlash();

            var lazyState = new Lazy<GlobState>(() =>
            {
                string fixedDirectoryPart = null;
                string wildcardDirectoryPart = null;
                string filenamePart = null;

                string matchFileExpression;
                bool needsRecursion;
                bool isLegalFileSpec;

                FileMatcher.Default.GetFileSpecInfo(
                    fileSpec,
                    out fixedDirectoryPart,
                    out wildcardDirectoryPart,
                    out filenamePart,
                    out matchFileExpression,
                    out needsRecursion,
                    out isLegalFileSpec,
                    (fixedDirPart, wildcardDirPart, filePart) =>
                    {
                        var normalizedFixedPart = NormalizeTheFixedDirectoryPartAgainstTheGlobRoot(fixedDirPart, globRoot);

                        return Tuple.Create(normalizedFixedPart, wildcardDirPart, filePart);
                    });

                // compile the regex since it's expected to be used multiple times
                var regex = isLegalFileSpec
                    ? new Regex(matchFileExpression, FileMatcher.DefaultRegexOptions | RegexOptions.Compiled)
                    : null;

                return new GlobState(globRoot, fileSpec, isLegalFileSpec, fixedDirectoryPart, wildcardDirectoryPart, filenamePart, matchFileExpression, needsRecursion, regex);
            },
            true);

            return new MSBuildGlob(lazyState);
        }

        private static string NormalizeTheFixedDirectoryPartAgainstTheGlobRoot(string fixedDirPart, string globRoot)
        {
            // todo: glob normalization is duplicated with EngineFileUtilities.CreateRegex
            // concatenate the glob parent to the fixed dir part
            var parentedFixedPart = Path.Combine(globRoot, fixedDirPart);
            var normalizedFixedPart = FileUtilities.GetFullPathNoThrow(parentedFixedPart);
            normalizedFixedPart = normalizedFixedPart.WithTrailingSlash();

            return normalizedFixedPart;
        }

        /// <summary>
        ///     See <see cref="Parse(string,string)" />.
        ///     The glob root will be the current working directory.
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
        public struct MatchInfoResult
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
