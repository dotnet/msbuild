// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;

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

        /// <summary>
        /// Indicates whether the specified string follows the pattern drive pattern: "<drive letter>:"
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool IsDrivePattern(string pattern)
        {
            // Format must be two characters long: "<drive letter>:"
            return pattern.Length == 2 &&
                StartsWithDrivePattern(pattern);
        }

        /// <summary>
        /// Indicates whether the specified string follows the pattern drive pattern: "<drive letter>:/" or "<drive letter>:\"
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool IsDrivePatternWithSlash(string pattern)
        {
            return pattern.Length == 3 &&
                    StartsWithDrivePattern(pattern) &&
                    (pattern[2] == _forwardSlash || pattern[2] == _backSlash);
        }

        /// <summary>
        /// Indicates whether the specified string starts with the drive pattern: "<drive letter>:".
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool StartsWithDrivePattern(string pattern)
        {
            // Format dictates a length of at least 2,
            // first character must be a letter,
            // second character must be a ":"
            return pattern.Length >= 2 &&
                ((pattern[0] >= 'A' && pattern[0] <= 'Z') || (pattern[0] >= 'a' && pattern[0] <= 'z')) &&
                pattern[1] == ':';
        }

        /// <summary>
        /// Indicates whether the specified string starts with the drive pattern: "<drive letter>:/" or "<drive letter>:\".
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool StartsWithDrivePatternWithSlash(string pattern)
        {
            // Format dictates a length of at least 3,
            // first character must be a letter,
            // second character must be a ":"
            // third character must be a slash.
            return pattern.Length >= 3 &&
                ((pattern[0] >= 'A' && pattern[0] <= 'Z') || (pattern[0] >= 'a' && pattern[0] <= 'z')) &&
                pattern[1] == ':' &&
                (pattern[2] == '\\' || pattern[2] == '/');
        }

        /// <summary>
        /// Indicates whether the specified file-spec comprises exactly "\\<server>\<share>" (with no trailing characters).
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool IsUncPattern(string pattern)
        {
            //Return value == pattern.length means:
            //  meets minimum unc requirements
            //  pattern does not end in a '/' or '\'
            //  if a subfolder were found the value returned would be length up to that subfolder, therefore no subfolder exists
            return StartsWithUncPatternMatchLength(pattern) == pattern.Length;
        }

        /// <summary>
        /// Indicates whether the specified file-spec begins with "\\<server>\<share>".
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static bool StartsWithUncPattern(string pattern)
        {
            //Any non -1 value returned means there was a match, therefore is begins with the pattern.
            return StartsWithUncPatternMatchLength(pattern) != -1;
        }

        /// <summary>
        /// Indicates whether the file-spec begins with a UNC pattern and how long the match is. -1 indicates no match.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
        internal static int StartsWithUncPatternMatchLength(string pattern)
        {
            if (!MeetsUncPatternMinimumRequirements(pattern))
            {
                return -1;
            }

            bool prevCharWasSlash = true;
            bool hasShare = false;

            for (int i = 2; i < pattern.Length; i++)
            {
                //Real UNC paths should only contain backslashes. However, the previous
                // regex pattern accepted both so functionality will be retained.
                if (pattern[i] == _backSlash ||
                    pattern[i] == _forwardSlash)
                {
                    if (prevCharWasSlash)
                    {
                        //We get here in the case of an extra slash.
                        return -1;
                    }
                    else if(hasShare)
                    {
                        return i;
                    }

                    hasShare = true;
                    prevCharWasSlash = true;
                }
                else
                {
                    prevCharWasSlash = false;
                }
            }

            if(!hasShare)
            {
                //no subfolder means no unc pattern. string is something like "\\abc" in this case
                return -1;
            }

            return pattern.Length;
        }

        /// <summary>
        /// Indicates whether the given path is a UNC or drive pattern root.
        /// <para>Note: This function mimics the behavior of checking if Path.GetDirectoryName(path) == null.</para>
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static bool IsRootPath(string path)
        {
            // Eliminate all non-rooted paths
            if (!Path.IsPathRooted(path))
            {
                return false;
            }

            int uncMatchLength = FileUtilitiesRegex.StartsWithUncPatternMatchLength(path);

            // Determine if the given path is a standard drive/unc pattern root
            if (FileUtilitiesRegex.IsDrivePattern(path) ||
                FileUtilitiesRegex.IsDrivePatternWithSlash(path) ||
                uncMatchLength == path.Length)
            {
                return true;
            }

            // Eliminate all non-root unc paths.
            if (uncMatchLength != -1)
            {
                return false;
            }

            // Eliminate any drive patterns that don't have a slash after the colon or where the 4th character is a non-slash
            // A non-slash at [3] is specifically checked here because Path.GetDirectoryName considers "C:///" a valid root.
            if (FileUtilitiesRegex.StartsWithDrivePattern(path) &&
                ((path.Length >= 3 && path[2] != _backSlash && path[2] != _forwardSlash) ||
                (path.Length >= 4 && path[3] != _backSlash && path[3] != _forwardSlash)))
            {
                return false;
            }

            // There are some edge cases that can get to this point.
            // After eliminating valid / invalid roots, fall back on original behavior.
            return Path.GetDirectoryName(path) == null;
        }

        /// <summary>
        /// Indicates whether or not the file-spec meets the minimum requirements of a UNC pattern.
        /// UNC pattern requires a minimum length of 5 and first two characters must be a slash.
        /// </summary>
        /// <param name="pattern"></param>
        /// <returns></returns>
#if !NET35
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
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
