// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Shared;
using Microsoft.Build.Shared.EscapingStringExtensions;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Internal
{
    internal struct FileSpecMatcherTester
    {
        private readonly string _currentDirectory;
        private readonly string _unescapedFileSpec;
        private readonly Regex _regex;
        
        private FileSpecMatcherTester(string currentDirectory, string unescapedFileSpec, Regex regex)
        {
            Debug.Assert(!string.IsNullOrEmpty(unescapedFileSpec));

            _currentDirectory = currentDirectory;
            _unescapedFileSpec = unescapedFileSpec;
            _regex = regex;
        }

        public static FileSpecMatcherTester Parse(string currentDirectory, string fileSpec)
        {
            string unescapedFileSpec = fileSpec.Unescape();
            Regex regex = EngineFileUtilities.FilespecHasWildcards(fileSpec) ? CreateRegex(unescapedFileSpec, currentDirectory) : null;

            return new FileSpecMatcherTester(currentDirectory, unescapedFileSpec, regex);
        }

        public bool IsMatch(string fileToMatch)
        {
            Debug.Assert(!string.IsNullOrEmpty(fileToMatch));

            // check if there is a regex matching the file
            if (_regex != null)
            {
                var normalizedFileToMatch = FileUtilities.GetFullPathNoThrow(Path.Combine(_currentDirectory, fileToMatch));
                return _regex.IsMatch(normalizedFileToMatch);
            }

            return FileUtilities.ComparePathsNoThrow(_unescapedFileSpec, fileToMatch, _currentDirectory);
        }

        // this method parses the glob and extracts the fixed directory part in order to normalize it and make it absolute
        // without this normalization step, strings pointing outside the globbing cone would still match when they shouldn't
        // for example, we dont want "**/*.cs" to match "../Shared/Foo.cs"
        // todo: glob rooting partially duplicated with MSBuildGlob.Parse
        private static Regex CreateRegex(string unescapedFileSpec, string currentDirectory)
        {
            Regex regex = null;
            string fixedDirPart = null;
            string wildcardDirectoryPart = null;
            string filenamePart = null;

            FileMatcher.Default.SplitFileSpec(
                unescapedFileSpec,
                out fixedDirPart,
                out wildcardDirectoryPart,
                out filenamePart);

            if (FileUtilities.PathIsInvalid(fixedDirPart))
            {
                return null;
            }

            var absoluteFixedDirPart = Path.Combine(currentDirectory, fixedDirPart);
            var normalizedFixedDirPart = string.IsNullOrEmpty(absoluteFixedDirPart)
                // currentDirectory is empty for some in-memory projects
                ? Directory.GetCurrentDirectory()
                : FileUtilities.GetFullPathNoThrow(absoluteFixedDirPart);

            normalizedFixedDirPart = FileUtilities.EnsureTrailingSlash(normalizedFixedDirPart);

            var recombinedFileSpec = string.Join("", normalizedFixedDirPart, wildcardDirectoryPart, filenamePart);

            bool isRecursive;
            bool isLegal;

            FileMatcher.Default.GetFileSpecInfoWithRegexObject(
                recombinedFileSpec,
                out regex,
                out isRecursive,
                out isLegal);

            return isLegal ? regex : null;
        }
    }
}
