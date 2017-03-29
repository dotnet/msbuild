// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.IO;
using System.Linq;
using Microsoft.Build.Shared;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Internal
{
    internal class EngineFileUtilities
    {
        /// <summary>
        /// Used for the purposes of evaluating an item specification. Given a filespec that may include wildcard characters * and
        /// ?, we translate it into an actual list of files. If the input filespec doesn't contain any wildcard characters, and it
        /// doesn't appear to point to an actual file on disk, then we just give back the input string as an array of length one,
        /// assuming that it wasn't really intended to be a filename (as items are not required to necessarily represent files).
        /// Any wildcards passed in that are unescaped will be treated as real wildcards.
        /// The "include" of items passed back from the filesystem will be returned canonically escaped.
        /// The ordering of the list returned is deterministic (it is sorted).
        /// Will never throw IO exceptions. If path is invalid, just returns filespec verbatim.
        /// </summary>
        /// <param name="directoryEscaped">The directory to evaluate, escaped.</param>
        /// <param name="filespecEscaped">The filespec to evaluate, escaped.</param>
        /// <returns>Array of file paths, unescaped.</returns>
        internal static string[] GetFileListUnescaped
            (
            string directoryEscaped,
            string filespecEscaped
            )

        {
            return GetFileList(directoryEscaped, filespecEscaped, false /* returnEscaped */);
        }

        /// <summary>
        /// Used for the purposes of evaluating an item specification. Given a filespec that may include wildcard characters * and
        /// ?, we translate it into an actual list of files. If the input filespec doesn't contain any wildcard characters, and it
        /// doesn't appear to point to an actual file on disk, then we just give back the input string as an array of length one,
        /// assuming that it wasn't really intended to be a filename (as items are not required to necessarily represent files).
        /// Any wildcards passed in that are unescaped will be treated as real wildcards.
        /// The "include" of items passed back from the filesystem will be returned canonically escaped.
        /// The ordering of the list returned is deterministic (it is sorted).
        /// Will never throw IO exceptions. If path is invalid, just returns filespec verbatim.
        /// </summary>
        /// <param name="directoryEscaped">The directory to evaluate, escaped.</param>
        /// <param name="filespecEscaped">The filespec to evaluate, escaped.</param>
        /// <param name="excludeSpecsEscaped">Filespecs to exclude, escaped.</param>
        /// <returns>Array of file paths, escaped.</returns>
        internal static string[] GetFileListEscaped
            (
            string directoryEscaped,
            string filespecEscaped,
            IEnumerable<string> excludeSpecsEscaped = null
            )
        {
            return GetFileList(directoryEscaped, filespecEscaped, true /* returnEscaped */, excludeSpecsEscaped);
        }

        private static bool FilespecHasWildcards(string filespecEscaped)
        {
            bool containsEscapedWildcards = EscapingUtilities.ContainsEscapedWildcards(filespecEscaped);
            bool containsRealWildcards = FileMatcher.HasWildcards(filespecEscaped);

            if (containsEscapedWildcards && containsRealWildcards)
            {
                // Umm, this makes no sense.  The item's Include has both escaped wildcards and 
                // real wildcards.  What does he want us to do?  Go to the file system and find
                // files that literally have '*' in their filename?  Well, that's not going to 
                // happen because '*' is an illegal character to have in a filename.

                return false;
            }
            else if (!containsEscapedWildcards && containsRealWildcards)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Used for the purposes of evaluating an item specification. Given a filespec that may include wildcard characters * and
        /// ?, we translate it into an actual list of files. If the input filespec doesn't contain any wildcard characters, and it
        /// doesn't appear to point to an actual file on disk, then we just give back the input string as an array of length one,
        /// assuming that it wasn't really intended to be a filename (as items are not required to necessarily represent files).
        /// Any wildcards passed in that are unescaped will be treated as real wildcards.
        /// The "include" of items passed back from the filesystem will be returned canonically escaped.
        /// The ordering of the list returned is deterministic (it is sorted).
        /// Will never throw IO exceptions: if there is no match, returns the input verbatim.
        /// </summary>
        /// <param name="directoryEscaped">The directory to evaluate, escaped.</param>
        /// <param name="filespecEscaped">The filespec to evaluate, escaped.</param>
        /// <param name="returnEscaped"><code>true</code> to return escaped specs.</param>
        /// <param name="excludeSpecsEscaped">The exclude specification, escaped.</param>
        /// <returns>Array of file paths.</returns>
        private static string[] GetFileList
            (
            string directoryEscaped,
            string filespecEscaped,
            bool returnEscaped,
            IEnumerable<string> excludeSpecsEscaped = null
            )
        {
            ErrorUtilities.VerifyThrowInternalLength(filespecEscaped, "filespecEscaped");

            if (excludeSpecsEscaped == null)
            {
                excludeSpecsEscaped = Enumerable.Empty<string>();
            }

            string[] fileList;

            if (FilespecHasWildcards(filespecEscaped))
            {
                // Unescape before handing it to the filesystem.
                var directoryUnescaped = EscapingUtilities.UnescapeAll(directoryEscaped);
                var filespecUnescaped = EscapingUtilities.UnescapeAll(filespecEscaped);
                var excludeSpecsUnescaped = excludeSpecsEscaped.Where(IsValidExclude).Select(EscapingUtilities.UnescapeAll).ToList();

                // Get the list of actual files which match the filespec.  Put
                // the list into a string array.  If the filespec started out
                // as a relative path, we will get back a bunch of relative paths.
                // If the filespec started out as an absolute path, we will get
                // back a bunch of absolute paths.
                fileList = FileMatcher.GetFiles(directoryUnescaped, filespecUnescaped, excludeSpecsUnescaped);

                ErrorUtilities.VerifyThrow(fileList != null, "We must have a list of files here, even if it's empty.");

                // Before actually returning the file list, we sort them alphabetically.  This
                // provides a certain amount of extra determinism and reproducability.  That is,
                // we're sure that the build will behave in exactly the same way every time,
                // and on every machine.
                Array.Sort(fileList, StringComparer.OrdinalIgnoreCase);

                if (returnEscaped)
                {
                    // We must now go back and make sure all special characters are escaped because we always 
                    // store data in the engine in escaped form so it doesn't interfere with our parsing.
                    // Note that this means that characters that were not escaped in the original filespec
                    // may now be escaped, but that's not easy to avoid.
                    for (int i = 0; i < fileList.Length; i++)
                    {
                        fileList[i] = EscapingUtilities.Escape(fileList[i]);
                    }
                }
            }
            else
            {
                // Just return the original string.
                fileList = new string[] { returnEscaped ? filespecEscaped : EscapingUtilities.UnescapeAll(filespecEscaped) };
            }

            return fileList;
        }

        private static bool IsValidExclude(string exclude)
        {
            // TODO: assumption on legal path characters: https://github.com/Microsoft/msbuild/issues/781
            // Excludes that have both wildcards and non escaped wildcards will never be matched on Windows, because
            // wildcard characters are invalid in Windows paths.
            // Filtering these excludes early keeps the glob expander simpler. Otherwise unescaping logic would reach all the way down to
            // filespec parsing (parse escaped string (to correctly ignore escaped wildcards) and then
            // unescape the path fragments to unfold potentially escaped wildcard chars)
            var hasBothWildcardsAndEscapedWildcards = FileMatcher.HasWildcards(exclude) && EscapingUtilities.ContainsEscapedWildcards(exclude);
            return !hasBothWildcardsAndEscapedWildcards;
        }

        /// Returns a Func that will return true IFF its argument matches any of the specified filespecs
        /// Assumes filespec may be escaped, so it unescapes it
        /// The returned function makes no escaping assumptions or escaping operations. Its callers should control escaping.
        internal static Func<string, bool> GetFileSpecMatchTester(IList<string> filespecsEscaped, string currentDirectory)
        {
            var matchers = filespecsEscaped
                .Select(fs => new Lazy<Func<string, bool>>(() => GetFileSpecMatchTester(fs, currentDirectory)))
                .ToList();

            return file => matchers.Any(m => m.Value(file));
        }

        internal static Func<string, bool> GetFileSpecMatchTester(string filespec, string currentDirectory)
        {
            Debug.Assert(!string.IsNullOrEmpty(filespec));

            var unescapedSpec = EscapingUtilities.UnescapeAll(filespec);

            var regex = FilespecHasWildcards(filespec) ? CreateRegex(unescapedSpec, currentDirectory) : null;

            return fileToMatch =>
            {
                Debug.Assert(!string.IsNullOrEmpty(fileToMatch));

                // check if there is a regex matching the file
                if (regex != null)
                {
                    var normalizedFileToMatch = FileUtilities.GetFullPathNoThrow(Path.Combine(currentDirectory, fileToMatch));
                    return regex.IsMatch(normalizedFileToMatch);
                }

                return FileUtilities.ComparePathsNoThrow(unescapedSpec, fileToMatch, currentDirectory);
            };
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

            FileMatcher.SplitFileSpec(
                unescapedFileSpec,
                out fixedDirPart,
                out wildcardDirectoryPart,
                out filenamePart,
                FileMatcher.s_defaultGetFileSystemEntries);

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

            FileMatcher.GetFileSpecInfoWithRegexObject(
                recombinedFileSpec,
                out regex,
                out isRecursive,
                out isLegal,
                FileMatcher.s_defaultGetFileSystemEntries);

            return isLegal ? regex : null;
        }
    }
}
