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
        internal readonly string _fileSpec;
        internal readonly string _globRoot;
        private readonly string _matchFileExpression;
        internal readonly bool _needsRecursion;
        private readonly Lazy<Regex> _regex;

        /// <summary>
        ///     The fixed directory part.
        /// </summary>
        public string FixedDirectoryPart { get; }

        /// <summary>
        ///     The wildcard directory part
        /// </summary>
        public string WildcardDirectoryPart { get; }

        /// <summary>
        ///     The file name part
        /// </summary>
        public string FilenamePart { get; }

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
        public bool IsLegal { get; }

        private MSBuildGlob(
            string globRoot,
            string fileSpec,
            string fixedDirectoryPart,
            string wildcardDirectoryPart,
            string filenamePart,
            string matchFileExpression,
            bool needsRecursion,
            bool isLegalFileSpec)
        {
            FixedDirectoryPart = fixedDirectoryPart;
            WildcardDirectoryPart = wildcardDirectoryPart;
            FilenamePart = filenamePart;
            IsLegal = isLegalFileSpec;

            _globRoot = globRoot;
            _fileSpec = fileSpec;
            _matchFileExpression = matchFileExpression;
            _needsRecursion = needsRecursion;

            // compile the regex since it's expected to be used multiple times
            _regex =
                new Lazy<Regex>(
                    () => new Regex(_matchFileExpression, FileMatcher.DefaultRegexOptions | RegexOptions.Compiled),
                    true);
        }

        /// <inheritdoc />
        public bool IsMatch(string stringToMatch)
        {
            ErrorUtilities.VerifyThrowArgumentNull(stringToMatch, nameof(stringToMatch));

            if (FileUtilities.PathIsInvalid(stringToMatch))
            {
                return false;
            }

            var normalizedString = NormalizeMatchInput(stringToMatch);

            return _regex.Value.IsMatch(normalizedString);
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
                _regex.Value,
                out isMatch,
                out fixedDirectoryPart,
                out wildcardDirectoryPart,
                out filenamePart);

            return new MatchInfoResult(isMatch, fixedDirectoryPart, wildcardDirectoryPart, filenamePart);
        }

        private string NormalizeMatchInput(string stringToMatch)
        {
            var rootedInput = Path.Combine(_globRoot, stringToMatch);
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

            string fixedDirectoryPart = null;
            string wildcardDirectoryPart = null;
            string filenamePart = null;

            string matchFileExpression;
            bool needsRecursion;
            bool isLegalFileSpec;

            FileMatcher.GetFileSpecInfo(
                fileSpec,
                out fixedDirectoryPart,
                out wildcardDirectoryPart,
                out filenamePart,
                out matchFileExpression,
                out needsRecursion,
                out isLegalFileSpec,
                FileMatcher.s_defaultGetFileSystemEntries,
                (fixedDirPart, wildcardDirPart, filePart) =>
                {
                    var normalizedFixedPart = NormalizeTheFixedDirectoryPartAgainstTheGlobRoot(fixedDirPart, globRoot);

                    return Tuple.Create(normalizedFixedPart, wildcardDirPart, filePart);
                });

            return new MSBuildGlob(
                globRoot,
                fileSpec,
                fixedDirectoryPart,
                wildcardDirectoryPart,
                filenamePart,
                matchFileExpression,
                needsRecursion,
                isLegalFileSpec);
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