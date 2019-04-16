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
        private static readonly char _backSlash = '\\';
        private static readonly char _forwardSlash = '/';

        // regular expression used to match file-specs comprising exactly "<drive letter>:" (with no trailing characters)
        internal static readonly Regex DrivePattern = new Regex(@"^[A-Za-z]:$", RegexOptions.Compiled);

        // regular expression used to match file-specs beginning with "<drive letter>:"
        internal static readonly Regex StartWithDrivePattern = new Regex(@"^[A-Za-z]:", RegexOptions.Compiled);

        private static readonly string s_baseUncPattern = string.Format(
            CultureInfo.InvariantCulture,
            @"^[\{0}\{1}][\{0}\{1}][^\{0}\{1}]+[\{0}\{1}][^\{0}\{1}]+",
            _backSlash, _forwardSlash);

        // regular expression used to match UNC paths beginning with "\\<server>\<share>"
        internal static readonly Regex StartsWithUncPattern = new Regex(s_baseUncPattern, RegexOptions.Compiled);

        // regular expression used to match UNC paths comprising exactly "\\<server>\<share>"
        internal static readonly Regex UncPattern =
            new Regex(
                string.Format(CultureInfo.InvariantCulture, @"{0}$", s_baseUncPattern),
                RegexOptions.Compiled);


        /// <summary>
        /// Indicates whether the specified string follows the pattern drive pattern: "<drive letter>:"
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool IsDrivePattern(string pattern)
        {
            // Format must be two characters long: "<drive letter>:"
            return pattern.Length == 2 &&
                DoesStartWithDrivePattern(pattern);
        }

        /// <summary>
        /// Indicates whether the specified string starts with the drive pattern: "<drive letter>:".
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool DoesStartWithDrivePattern(string pattern)
        {
            // Format dictates a length of at least 2,
            // first character must be a letter,
            // second character must be a ":"
            return pattern.Length >= 2 &&
                ((pattern[0] >= 'A' && pattern[0] <= 'Z') || (pattern[0] >= 'a' && pattern[0] <= 'z')) &&
                pattern[1] == ':';
        }

        /// <summary>
        /// Indicates whether the specified file-spec comprises exactly "\\<server>\<path>" (with no trailing characters).
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool IsUncPattern(string pattern)
        {
            if(!MeetsUncPatternMinimumRequirements(pattern))
            {
                return false;
            }

            bool prevCharWasSlash = true;
            bool hasSubfolder = false;
            for (int i = 2; i < pattern.Length; i++)
            {
                if (pattern[i] == _backSlash ||
                    pattern[i] == _forwardSlash)
                {
                    if (prevCharWasSlash || hasSubfolder)
                    {
                        //We get here in the case of an extra slash or multiple subfolders
                        //  Note this function is meant to mimic the UncPattern regex above.
                        return false;
                    }

                    hasSubfolder = true;
                    prevCharWasSlash = true;
                }
                else
                {
                    prevCharWasSlash = false;
                }
            }

            //Valid unc patterns don't end with slashes & have at least 1 subfolder
            return !prevCharWasSlash && hasSubfolder;
        }

        /// <summary>
        /// Indicates whether the specified file-spec begins with "\\<server>\<path>".
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool DoesStartWithUncPattern(string pattern)
        {
            if (!MeetsUncPatternMinimumRequirements(pattern))
            {
                return false;
            }

            bool prevCharWasSlash = true;
            bool hasSubfolder = false;

            for (int i = 2; i < pattern.Length; i++)
            {
                if (pattern[i] == _backSlash ||
                    pattern[i] == _forwardSlash)
                {
                    if (prevCharWasSlash)
                    {
                        //We get here in the case of an extra slash.
                        return false;
                    }

                    hasSubfolder = true;
                    prevCharWasSlash = true;
                }
                else
                {
                    if (hasSubfolder)
                    {
                        //A character after a subfolder confirms the beginning of a unc pattern
                        return true;
                    }

                    prevCharWasSlash = false;
                }
            }

            return false;
        }

        /// <summary>
        /// Indicates whether the file-spec begins with a UNC pattern and how long the match is. -1 indicates no match.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static int DoesStartWithUncPatternMatchLength(string pattern)
        {
            if (!MeetsUncPatternMinimumRequirements(pattern))
            {
                return -1;
            }

            bool prevCharWasSlash = true;
            bool hasSubfolder = false;

            for (int i = 2; i < pattern.Length; i++)
            {
                if (pattern[i] == _backSlash ||
                    pattern[i] == _forwardSlash)
                {
                    if (prevCharWasSlash)
                    {
                        //We get here in the case of an extra slash.
                        return -1;
                    }
                    else if(hasSubfolder)
                    {
                        return i;
                    }

                    hasSubfolder = true;
                    prevCharWasSlash = true;
                }
                else
                {
                    prevCharWasSlash = false;
                }
            }

            if(!hasSubfolder)
            {
                //no subfolder means no unc pattern. string is something like "\\abc" in this case
                return -1;
            }

            return pattern.Length;
        }

        /// <summary>
        /// Indicates whether or not the file-spec meets the minimum requirements of a UNC pattern.
        /// UNC pattern requires a minimum length of 5 and first two characters must be a slash.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool MeetsUncPatternMinimumRequirements(string pattern)
        {
            return pattern.Length >= 5 &&
                (pattern[0] == _backSlash ||
                pattern[0] == _forwardSlash) &&
                (pattern[1] == _backSlash ||
                pattern[1] == _forwardSlash);
       }

    }
}
