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
        /// <param name="drivePattern"></param>
        /// <returns></returns>
        internal static bool IsDrivePattern(string drivePattern)
        {
            // Format must be two characters long: "<drive letter>:"
            return drivePattern.Length == 2 &&
                DoesStartWithDrivePattern(drivePattern);
        }

        /// <summary>
        /// Indicates whether the specified string begins with "<drive letter>:".
        /// </summary>
        /// <param name="drivePattern"></param>
        /// <returns></returns>
        internal static bool DoesStartWithDrivePattern(string drivePattern)
        {
            // Format dictates a length of at least 2
            // First character must be a letter
            // Second character must be a ":"
            return drivePattern.Length >= 2 &&
                (drivePattern[0] >= 'A' && drivePattern[0] <= 'Z') || (drivePattern[0] >= 'a' && drivePattern[0] <= 'z') &&
                drivePattern[1] == ':';
        }

        internal static bool DoesStartWithUncPattern(string drivePattern)
        {
            //Format dictates a minimum length of 5: "\\a\b"
            if (drivePattern.Length < 5)
            {
                return false;
            }

            //Enforce unix/windows slashes &
            //  first two characters matching
            if ((drivePattern[0] != Path.DirectorySeparatorChar &&
                drivePattern[0] != Path.AltDirectorySeparatorChar) ||
                drivePattern[0] != drivePattern[1])
            {
                return false;
            }

            bool searchingForSlash = false;
            int slashesFound = 0;

            for (int i = 2; i < drivePattern.Length; i++)
            {
                if (drivePattern[i] == Path.DirectorySeparatorChar ||
                    drivePattern[i] == Path.AltDirectorySeparatorChar)
                {
                    if (!searchingForSlash)
                    {
                        //We get here in the case of an extra slash somewhere
                        //Ex: "\\a\b\\c...", "\\\a\\b\c
                        return false;
                    }

                    slashesFound++;
                    searchingForSlash = false;
                }
                else
                {
                    if(slashesFound >= 1)
                    {
                        //Finding a character after a slash verifies the beginning of a unc pattern
                        //Ex: "\\a\b..."
                        return true;
                    }

                    searchingForSlash = true;
                }
            }

            return false;
        }

    }
}
