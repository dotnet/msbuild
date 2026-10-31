// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Shared.Globbing;

#nullable disable

namespace Microsoft.Build.Internal
{
    internal readonly struct FileSpecMatcherTester
    {
        private readonly string _currentDirectory;
        private readonly string _unescapedFileSpec;
        private readonly string _filenamePattern;
        private readonly GlobPattern _matcher;

        private FileSpecMatcherTester(string currentDirectory, string unescapedFileSpec, string filenamePattern, GlobPattern matcher)
        {
            Debug.Assert(!string.IsNullOrEmpty(unescapedFileSpec));
            Debug.Assert(currentDirectory != null);

            _currentDirectory = currentDirectory;
            _unescapedFileSpec = unescapedFileSpec;
            _filenamePattern = filenamePattern;
            _matcher = matcher;

            if (_matcher is null && _filenamePattern is null)
            {
                // We'll be testing files by comparing their normalized paths. Normalize our file spec right away
                // to avoid doing this work on each IsMatch call.
                _unescapedFileSpec = FileUtilities.NormalizePathForComparisonNoThrow(_unescapedFileSpec, _currentDirectory);
            }
        }

        public static FileSpecMatcherTester Parse(string currentDirectory, string fileSpec)
        {
            string unescapedFileSpec = EscapingUtilities.UnescapeAll(fileSpec);
            string filenamePattern = null;
            GlobPattern matcher = null;

            if (EngineFileUtilities.FilespecHasWildcards(fileSpec))
            {
                CreateGlobOrFilenamePattern(unescapedFileSpec, currentDirectory, out filenamePattern, out matcher);
            }

            return new FileSpecMatcherTester(currentDirectory, unescapedFileSpec, filenamePattern, matcher);
        }

        /// <summary>
        /// Returns true if the given file matches this file spec.
        /// </summary>
        public bool IsMatch(string fileToMatch)
        {
            Debug.Assert(!string.IsNullOrEmpty(fileToMatch));

            // Historically we've used slightly different normalization logic depending on the type of matching
            // performed in IsMatchNormalized. We have to keep doing it for compat.
            if (_matcher is null && _filenamePattern is null)
            {
                fileToMatch = FileUtilities.NormalizePathForComparisonNoThrow(fileToMatch, _currentDirectory);
            }
            else
            {
                fileToMatch = FileUtilities.GetFullPathNoThrow(Path.Combine(_currentDirectory, fileToMatch));
            }
            return IsMatchNormalized(fileToMatch);
        }

        /// <summary>
        /// Same as <see cref="IsMatch" /> but the argument is expected to be a normalized path.
        /// </summary>
        public bool IsMatchNormalized(string normalizedFileToMatch)
        {
            Debug.Assert(!string.IsNullOrEmpty(normalizedFileToMatch));

            // Select full-path globbing, filename matching, or literal path comparison.
            if (_matcher is not null)
            {
                return _matcher.IsMatch(normalizedFileToMatch);
            }

            if (_filenamePattern != null)
            {
                // Check file name first as it's more likely to not match.
                string filename = Path.GetFileName(normalizedFileToMatch);
                if (!GlobPattern.MatchesName(filename.AsSpan(), _filenamePattern))
                {
                    return false;
                }

                return normalizedFileToMatch.StartsWith(_currentDirectory, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(_unescapedFileSpec, normalizedFileToMatch, StringComparison.OrdinalIgnoreCase);
        }

        // this method parses the glob and extracts the fixed directory part in order to normalize it and make it absolute
        // without this normalization step, strings pointing outside the globbing cone would still match when they shouldn't
        // for example, we dont want "**/*.cs" to match "../Shared/Foo.cs"
        // todo: glob rooting knowledge partially duplicated with MSBuildGlob.Parse and FileMatcher.ComputeFileEnumerationCacheKey
        private static void CreateGlobOrFilenamePattern(string unescapedFileSpec, string currentDirectory, out string filenamePattern, out GlobPattern matcher)
        {
            FileMatcher.Default.SplitFileSpec(
                unescapedFileSpec,
                out string fixedDirPart,
                out string wildcardDirectoryPart,
                out string filenamePart);

            if (FileUtilities.PathIsInvalid(fixedDirPart))
            {
                filenamePattern = null;
                matcher = null;
                return;
            }

            // Most file specs have "**" as their directory specification so we special case these and make matching faster.
            if (string.IsNullOrEmpty(fixedDirPart) && FileMatcher.IsRecursiveDirectoryMatch(wildcardDirectoryPart))
            {
                filenamePattern = filenamePart;
                matcher = null;
                return;
            }

            var absoluteFixedDirPart = Path.Combine(currentDirectory, fixedDirPart);
            var normalizedFixedDirPart = string.IsNullOrEmpty(absoluteFixedDirPart)
                // currentDirectory is empty for some in-memory projects
                ? Directory.GetCurrentDirectory()
                : FileUtilities.GetFullPathNoThrow(absoluteFixedDirPart);

            normalizedFixedDirPart = FileUtilities.EnsureTrailingSlash(normalizedFixedDirPart);

            var recombinedFileSpec = string.Concat(normalizedFixedDirPart, wildcardDirectoryPart, filenamePart);

            FileMatcher.Default.GetFileSpecInfoWithGlob(
                recombinedFileSpec,
                out GlobPattern glob,
                out bool _,
                out bool isLegal);

            filenamePattern = null;
            matcher = isLegal ? glob : null;
        }
    }
}
