// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class contains utility methods for file IO.
    /// Separate from FileUtilities because some assemblies may only need the patterns.
    /// PERF\COVERAGE NOTE: Try to keep classes in 'shared' as granular as possible. All the methods in 
    /// each class get pulled into the resulting assembly.
    /// </summary>
    internal static class FileUtilitiesRegex
    {
        // regular expression used to match file-specs comprising exactly "<drive letter>:" (with no trailing characters)
        internal static readonly Regex DrivePattern = new Regex(@"^[A-Za-z]:$", RegexOptions.Compiled);

        // regular expression used to match file-specs beginning with "<drive letter>:"
        internal static readonly Regex StartWithDrivePattern = new Regex(@"^[A-Za-z]:", RegexOptions.Compiled);

        private static readonly string s_baseUncPattern = string.Format(
            CultureInfo.InvariantCulture,
            @"^[\{0}\{1}][\{0}\{1}][^\{0}\{1}]+[\{0}\{1}][^\{0}\{1}]+",
            Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        // regular expression used to match UNC paths beginning with "\\<server>\<share>"
        internal static readonly Regex StartsWithUncPattern = new Regex(s_baseUncPattern, RegexOptions.Compiled);

        // regular expression used to match UNC paths comprising exactly "\\<server>\<share>"
        internal static readonly Regex UncPattern =
            new Regex(
                string.Format(CultureInfo.InvariantCulture, @"{0}$", s_baseUncPattern),
                RegexOptions.Compiled);


        /// <summary>
        /// Indicates whether the specified string follows the pattern "<drive letter>:".
        /// Using this function over regex results in improved memory performance.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool IsDrivePattern(string pattern)
        {
            // Format must be two characters long: "<drive letter>:"
            return pattern.Length == 2 &&
                DoesStartWithDrivePattern(pattern);
        }

        internal static bool IsUncPattern(string pattern)
        {
            if(!DoesStartWithUncPattern(pattern))
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// Indicates whether the specified string begins with "<drive letter>:".
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool DoesStartWithDrivePattern(string pattern)
        {
            // Format dictates a length of at least 2
            // First character must be a letter
            // Second character must be a ":"
            return pattern.Length >= 2 &&
                ((pattern[0] >= 'A' && pattern[0] <= 'Z') || (pattern[0] >= 'a' && pattern[0] <= 'z')) &&
                pattern[1] == ':';
        }

        internal static bool DoesStartWithUncPattern(string pattern)
        {
            if (!MeetsUncPatternMinimumRequirements(pattern))
            {
                return false;
            }

            bool searchingForSlash = false;
            int slashesFound = 0;

            for (int i = 2; i < pattern.Length; i++)
            {
                if (pattern[i] == Path.DirectorySeparatorChar ||
                    pattern[i] == Path.AltDirectorySeparatorChar)
                {
                    if (!searchingForSlash)
                    {
                        //We get here in the case of an extra slash somewhere. Ex: "\\a\b\\c...", "\\\a\\b\c"
                        return false;
                    }

                    slashesFound++;
                    searchingForSlash = false;
                }
                else
                {
                    if (slashesFound >= 1)
                    {
                        //Found a char after slash, therefore beginning of unc pattern
                        return true;
                    }

                    searchingForSlash = true;
                }
            }

            return false;
        }

        internal static bool MeetsUncPatternMinimumRequirements(string pattern)
        {
            //Format dictates a minimum length of 5: "\\a\b" &
            //  first two characters must be unix/windows slash
            return pattern.Length >= 5 &&
                (pattern[0] == Path.DirectorySeparatorChar ||
                pattern[0] == Path.AltDirectorySeparatorChar) &&
                (pattern[1] == Path.DirectorySeparatorChar ||
                pattern[1] == Path.AltDirectorySeparatorChar);
       }

    }
}
