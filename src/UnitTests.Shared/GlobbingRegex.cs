// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests.Shared
{
    public static class GlobbingRegex
    {
        private static class FileSpecRegexParts
        {
            internal const string BeginningOfLine = "^";
            internal const string WildcardGroupStart = "(?<WILDCARDDIR>";
            internal const string FilenameGroupStart = "(?<FILENAME>";
            internal const string GroupEnd = ")";
            internal const string EndOfLine = "$";

            internal const string AnyNonSeparator = @"[^/\\]*";
            internal const string AnySingleCharacterButDot = @"[^\.].";
            internal const string AnythingButDot = @"[^\.]*";
            internal const string DirSeparator = @"[/\\]+";
            internal const string LeftDirs = @"((.*/)|(.*\\)|())";
            internal const string MiddleDirs = @"((/)|(\\)|(/.*/)|(/.*\\)|(\\.*\\)|(\\.*/))";
            internal const string SingleCharacter = ".";
            internal const string UncSlashSlash = @"\\\\";
        }

        private const int FileSpecRegexMinLength = 31;

        /// <summary>
        /// Given a split file spec consisting of a directory without wildcard characters,
        /// a sub-directory containing wildcard characters,
        /// and a filename which may contain wildcard characters,
        /// create a regular expression that will match that file spec.
        ///
        /// This is the legacy implementation, retained only as a test and benchmark oracle.
        /// </summary>
        /// <param name="fixedDirectoryPart">The fixed directory part.</param>
        /// <param name="wildcardDirectoryPart">The wildcard directory part.</param>
        /// <param name="filenamePart">The filename part.</param>
        /// <returns>The regular expression string.</returns>
        public static string RegularExpressionFromFileSpec(
            string fixedDirectoryPart,
            string wildcardDirectoryPart,
            string filenamePart)
        {
            using (var matchFileExpression = new ReuseableStringBuilder(FileSpecRegexMinLength + NativeMethods.MAX_PATH))
            {
                AppendRegularExpressionFromFixedDirectory(matchFileExpression, fixedDirectoryPart);
                AppendRegularExpressionFromWildcardDirectory(matchFileExpression, wildcardDirectoryPart);
                AppendRegularExpressionFromFilename(matchFileExpression, filenamePart);

                return matchFileExpression.ToString();
            }
        }

        private static void AppendRegularExpressionFromFixedDirectory(ReuseableStringBuilder regex, string fixedDir)
        {
            regex.Append(FileSpecRegexParts.BeginningOfLine);
            bool isUncPath = NativeMethods.IsWindows && fixedDir.Length > 1
                && fixedDir[0] == '\\' && fixedDir[1] == '\\';
            if (isUncPath)
            {
                regex.Append(FileSpecRegexParts.UncSlashSlash);
            }

            int startIndex = LastIndexOfDirectorySequence(fixedDir, 0) + (isUncPath ? 1 : 0);
            for (int i = startIndex; i < fixedDir.Length; i = LastIndexOfDirectorySequence(fixedDir, i + 1))
            {
                AppendRegularExpressionFromChar(regex, fixedDir[i]);
            }
        }

        private static void AppendRegularExpressionFromWildcardDirectory(ReuseableStringBuilder regex, string wildcardDir)
        {
            regex.Append(FileSpecRegexParts.WildcardGroupStart);
            if (wildcardDir.Length > 2 && wildcardDir[0] == '*' && wildcardDir[1] == '*')
            {
                regex.Append(FileSpecRegexParts.LeftDirs);
            }

            int startIndex = LastIndexOfDirectoryOrRecursiveSequence(wildcardDir, 0);
            for (int i = startIndex; i < wildcardDir.Length; i = LastIndexOfDirectoryOrRecursiveSequence(wildcardDir, i + 1))
            {
                bool isRecursiveOperator = i < wildcardDir.Length - 2 && wildcardDir[i + 1] == '*' && wildcardDir[i + 2] == '*';
                if (isRecursiveOperator)
                {
                    regex.Append(FileSpecRegexParts.MiddleDirs);
                }
                else
                {
                    AppendRegularExpressionFromChar(regex, wildcardDir[i]);
                }
            }

            regex.Append(FileSpecRegexParts.GroupEnd);
        }

        // Freeze legacy normalization here so changes to FileMatcher cannot alter the oracle.
        private static int LastIndexOfDirectorySequence(string str, int startIndex)
        {
            if (startIndex >= str.Length || !FileUtilities.IsAnySlash(str[startIndex]))
            {
                return startIndex;
            }
            int i = startIndex;
            bool isSequenceEndFound = false;

            while (!isSequenceEndFound && i < str.Length)
            {
                bool isSeparator = i < str.Length - 1 && FileUtilities.IsAnySlash(str[i + 1]);
                bool isRelativeSeparator = i < str.Length - 2 && str[i + 1] == '.' && FileUtilities.IsAnySlash(str[i + 2]);

                if (isSeparator)
                {
                    i++;
                }
                else if (isRelativeSeparator)
                {
                    i += 2;
                }
                else
                {
                    isSequenceEndFound = true;
                }
            }

            return i;
        }

        private static int LastIndexOfDirectoryOrRecursiveSequence(string str, int startIndex)
        {
            bool isRecursiveSequence = startIndex < str.Length - 1
                                            && str[startIndex] == '*' && str[startIndex + 1] == '*';
            if (!isRecursiveSequence)
            {
                return LastIndexOfDirectorySequence(str, startIndex);
            }

            int i = startIndex + 2;
            bool isSequenceEndFound = false;

            while (!isSequenceEndFound && i < str.Length)
            {
                i = LastIndexOfDirectorySequence(str, i);
                bool isRecursiveOperator = i < str.Length - 2 && str[i + 1] == '*' && str[i + 2] == '*';

                if (isRecursiveOperator)
                {
                    i += 3;
                }
                else
                {
                    isSequenceEndFound = true;
                }
            }

            return i + 1;
        }

        private static void AppendRegularExpressionFromFilename(ReuseableStringBuilder regex, string filename)
        {
            regex.Append(FileSpecRegexParts.FilenameGroupStart);
            bool hasTrailingDot = filename.Length > 0 && filename[filename.Length - 1] == '.';
            int partLength = hasTrailingDot ? filename.Length - 1 : filename.Length;
            for (int i = 0; i < partLength; i++)
            {
                char ch = filename[i];
                if (hasTrailingDot && ch == '*')
                {
                    regex.Append(FileSpecRegexParts.AnythingButDot);
                }
                else if (hasTrailingDot && ch == '?')
                {
                    regex.Append(FileSpecRegexParts.AnySingleCharacterButDot);
                }
                else
                {
                    AppendRegularExpressionFromChar(regex, ch);
                }

                if (!hasTrailingDot && i < partLength - 2 && ch == '*' && filename[i + 1] == '.' && filename[i + 2] == '*')
                {
                    i += 2;
                }
            }

            regex.Append(FileSpecRegexParts.GroupEnd);
            regex.Append(FileSpecRegexParts.EndOfLine);
        }

        private static void AppendRegularExpressionFromChar(ReuseableStringBuilder regex, char ch)
        {
            if (ch == '*')
            {
                regex.Append(FileSpecRegexParts.AnyNonSeparator);
            }
            else if (ch == '?')
            {
                regex.Append(FileSpecRegexParts.SingleCharacter);
            }
            else if (FileUtilities.IsAnySlash(ch))
            {
                regex.Append(FileSpecRegexParts.DirSeparator);
            }
            else
            {
                if (ch is '$' or '(' or ')' or '+' or '.' or '[' or '^' or '{' or '|')
                {
                    regex.Append('\\');
                }

                regex.Append(ch);
            }
        }
    }
}
