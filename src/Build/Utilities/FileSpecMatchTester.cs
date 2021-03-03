// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Internal
{
    internal readonly struct FileSpecMatcherTester
    {
        private readonly string _currentDirectory;
        private readonly string _unescapedFileSpec;
        private readonly string _filenamePattern;
        private readonly Regex _regex;
        
        private FileSpecMatcherTester(string currentDirectory, string unescapedFileSpec, string filenamePattern, Regex regex)
        {
            Debug.Assert(!string.IsNullOrEmpty(unescapedFileSpec));
            Debug.Assert(currentDirectory != null);

            _currentDirectory = currentDirectory;
            _unescapedFileSpec = unescapedFileSpec;
            _filenamePattern = filenamePattern;
            _regex = regex;
        }

        public static FileSpecMatcherTester Parse(string currentDirectory, string fileSpec)
        {
            string unescapedFileSpec = EscapingUtilities.UnescapeAll(fileSpec);
            string filenamePattern = null;
            Regex regex = null;

            if (EngineFileUtilities.FilespecHasWildcards(fileSpec))
            {
                CreateRegexOrFilenamePattern(unescapedFileSpec, currentDirectory, out filenamePattern, out regex);
            }

            return new FileSpecMatcherTester(currentDirectory, unescapedFileSpec, filenamePattern, regex);
        }

        public bool IsMatch(string fileToMatch)
        {
            Debug.Assert(!string.IsNullOrEmpty(fileToMatch));

            // We do the matching using one of three code paths, depending on the value of _filenamePattern and _regex.
            if (_regex != null)
            {
                string normalizedFileToMatch = FileUtilities.GetFullPathNoThrow(Path.Combine(_currentDirectory, fileToMatch));
                return _regex.IsMatch(normalizedFileToMatch);
            }

            if (_filenamePattern != null)
            {
                // Check file name first as it's more likely to not match.
                string filename = Path.GetFileName(fileToMatch);
                if (!FileMatcher.IsMatch(filename, _filenamePattern))
                {
                    return false;
                }

                var normalizedFileToMatch = FileUtilities.GetFullPathNoThrow(Path.Combine(_currentDirectory, fileToMatch));
                return normalizedFileToMatch.StartsWith(_currentDirectory, StringComparison.OrdinalIgnoreCase);
            }

            return FileUtilities.ComparePathsNoThrow(_unescapedFileSpec, fileToMatch, _currentDirectory, alwaysIgnoreCase: true);
        }

        // this method parses the glob and extracts the fixed directory part in order to normalize it and make it absolute
        // without this normalization step, strings pointing outside the globbing cone would still match when they shouldn't
        // for example, we dont want "**/*.cs" to match "../Shared/Foo.cs"
        // todo: glob rooting knowledge partially duplicated with MSBuildGlob.Parse and FileMatcher.ComputeFileEnumerationCacheKey
        private static void CreateRegexOrFilenamePattern(string unescapedFileSpec, string currentDirectory, out string filenamePattern, out Regex regex)
        {
            FileMatcher.Default.SplitFileSpec(
                unescapedFileSpec,
                out string fixedDirPart,
                out string wildcardDirectoryPart,
                out string filenamePart);

            if (FileUtilities.PathIsInvalid(fixedDirPart))
            {
                filenamePattern = null;
                regex = null;
                return;
            }

            // Most file specs have "**" as their directory specification so we special case these and make matching faster.
            if (string.IsNullOrEmpty(fixedDirPart) && FileMatcher.IsRecursiveDirectoryMatch(wildcardDirectoryPart))
            {
                filenamePattern = filenamePart;
                regex = null;
                return;
            }

            var absoluteFixedDirPart = Path.Combine(currentDirectory, fixedDirPart);
            var normalizedFixedDirPart = string.IsNullOrEmpty(absoluteFixedDirPart)
                // currentDirectory is empty for some in-memory projects
                ? Directory.GetCurrentDirectory()
                : FileUtilities.GetFullPathNoThrow(absoluteFixedDirPart);

            normalizedFixedDirPart = FileUtilities.EnsureTrailingSlash(normalizedFixedDirPart);

            var recombinedFileSpec = string.Concat(normalizedFixedDirPart, wildcardDirectoryPart, filenamePart);

            FileMatcher.Default.GetFileSpecInfoWithRegexObject(
                recombinedFileSpec,
                out Regex regexObject,
                out bool _,
                out bool isLegal);

            filenamePattern = null;
            regex = isLegal ? regexObject : null;
        }
    }
}
