// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Diagnostics;
using System.Text;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// This class implements some static methods to assist with command-line parsing of 
    /// parameters that could be quoted, and thus could contain nested escaped quotes.
    /// </summary>
    internal static class QuotingUtilities
    {
        /*
         * Quoting Rules:
         * 
         * A string is considered quoted if it is enclosed in double-quotes. A double-quote can be escaped with a backslash, or it
         * is automatically escaped if it is the last character in an explicitly terminated quoted string. A backslash itself can
         * be escaped with another backslash IFF it precedes a double-quote, otherwise it is interpreted literally.
         * 
         * e.g.
         *      abc"cde"xyz         --> "cde" is quoted
         *      abc"xyz             --> "xyz" is quoted (the terminal double-quote is assumed)
         *      abc"xyz"            --> "xyz" is quoted (the terminal double-quote is explicit)
         * 
         *      abc\"cde"xyz        --> "xyz" is quoted (the terminal double-quote is assumed)
         *      abc\\"cde"xyz       --> "cde" is quoted
         *      abc\\\"cde"xyz      --> "xyz" is quoted (the terminal double-quote is assumed)
         * 
         *      abc"""xyz           --> """ is quoted
         *      abc""""xyz          --> """ and "xyz" are quoted (the terminal double-quote is assumed)
         *      abc"""""xyz         --> """ is quoted
         *      abc""""""xyz        --> """ and """ are quoted
         *      abc"cde""xyz        --> "cde"" is quoted
         *      abc"xyz""           --> "xyz"" is quoted (the terminal double-quote is explicit)
         * 
         *      abc""xyz            --> nothing is quoted
         *      abc""cde""xyz       --> nothing is quoted
         */

        // the null character is used to mark a string for splitting
        private static readonly char[] s_splitMarker = { '\0' };

        /// <summary>
        /// Splits the given string on every instance of a separator character, as long as the separator is not quoted. Each split
        /// piece is then unquoted if requested.
        /// </summary>
        /// <remarks>
        /// 1) Unless requested to keep empty splits, a block of contiguous (unquoted) separators is treated as one separator.
        /// 2) If no separators are given, the string is split on whitespace.
        /// </remarks>
        /// <param name="input"></param>
        /// <param name="maxSplits"></param>
        /// <param name="keepEmptySplits"></param>
        /// <param name="unquote"></param>
        /// <param name="emptySplits">[out] a count of all pieces that were empty, and thus discarded, per remark (1) above</param>
        /// <param name="separator"></param>
        /// <returns>ArrayList of all the pieces the string was split into.</returns>
        internal static ArrayList SplitUnquoted
        (
            string input,
            int maxSplits,
            bool keepEmptySplits,
            bool unquote,
            out int emptySplits,
            params char[] separator
        )
        {
            ErrorUtilities.VerifyThrow(maxSplits >= 2, "There is no point calling this method for less than two splits.");

            string separators = new StringBuilder().Append(separator).ToString();

            ErrorUtilities.VerifyThrow(separators.IndexOf('"') == -1, "The double-quote character is not supported as a separator.");

            StringBuilder splitString = new StringBuilder();
            splitString.EnsureCapacity(input.Length);

            bool isQuoted = false;
            int precedingBackslashes = 0;
            int splits = 1;

            for (int i = 0; (i < input.Length) && (splits < maxSplits); i++)
            {
                switch (input[i])
                {
                    case '\0':
                        // Pretend null characters just aren't there.  Ignore them.
                        Debug.Assert(false, "Null character in parameter");
                        break;

                    case '\\':
                        splitString.Append('\\');
                        precedingBackslashes++;
                        break;

                    case '"':
                        splitString.Append('"');
                        if ((precedingBackslashes % 2) == 0)
                        {
                            if (isQuoted &&
                                (i < (input.Length - 1)) &&
                                (input[i + 1] == '"'))
                            {
                                splitString.Append('"');
                                i++;
                            }
                            isQuoted = !isQuoted;
                        }
                        precedingBackslashes = 0;
                        break;

                    default:
                        if (!isQuoted &&
                            (((separators.Length == 0) && char.IsWhiteSpace(input[i])) ||
                            (separators.IndexOf(input[i]) != -1)))
                        {
                            splitString.Append('\0');
                            if (++splits == maxSplits)
                            {
                                splitString.Append(input, i + 1, input.Length - (i + 1));
                            }
                        }
                        else
                        {
                            splitString.Append(input[i]);
                        }
                        precedingBackslashes = 0;
                        break;
                }
            }

            ArrayList pieces = new ArrayList();
            emptySplits = 0;

            foreach (string splitPiece in splitString.ToString().Split(s_splitMarker, maxSplits))
            {
                string piece = (unquote
                    ? Unquote(splitPiece)
                    : splitPiece);

                if ((piece.Length > 0) || keepEmptySplits)
                {
                    pieces.Add(piece);
                }
                else
                {
                    emptySplits++;
                }
            }

            return pieces;
        }

        /// <summary>
        /// Splits the given string on every instance of a separator character, as long as the separator is not quoted.
        /// </summary>
        /// <remarks>
        /// 1) A block of contiguous (unquoted) separators is considered as one separator.
        /// 2) If no separators are given, the string is split on blocks of contiguous (unquoted) whitespace.
        /// </remarks>
        /// <param name="input"></param>
        /// <param name="separator"></param>
        /// <returns>ArrayList of all the pieces the string was split into.</returns>
        internal static ArrayList SplitUnquoted(string input, params char[] separator)
        {
            int emptySplits;
            return SplitUnquoted(input, int.MaxValue, false /* discard empty splits */, false /* don't unquote the split pieces */, out emptySplits, separator);
        }

        /// <summary>
        /// Removes unescaped (i.e. non-literal) double-quotes, and escaping backslashes, from the given string.
        /// </summary>
        /// <param name="input"></param>
        /// <param name="doubleQuotesRemoved">[out] the number of double-quotes removed from the string</param>
        /// <returns>The given string in unquoted form.</returns>
        internal static string Unquote(string input, out int doubleQuotesRemoved)
        {
            StringBuilder unquotedString = new StringBuilder();
            unquotedString.EnsureCapacity(input.Length);

            bool isQuoted = false;
            int precedingBackslashes = 0;
            doubleQuotesRemoved = 0;

            for (int i = 0; i < input.Length; i++)
            {
                switch (input[i])
                {
                    case '\\':
                        precedingBackslashes++;
                        break;

                    case '"':
                        unquotedString.Append('\\', precedingBackslashes / 2);
                        if ((precedingBackslashes % 2) == 0)
                        {
                            if (isQuoted &&
                                (i < (input.Length - 1)) &&
                                (input[i + 1] == '"'))
                            {
                                unquotedString.Append('"');
                                i++;
                            }
                            isQuoted = !isQuoted;
                            doubleQuotesRemoved++;
                        }
                        else
                        {
                            unquotedString.Append('"');
                        }
                        precedingBackslashes = 0;
                        break;

                    default:
                        unquotedString.Append('\\', precedingBackslashes);
                        unquotedString.Append(input[i]);
                        precedingBackslashes = 0;
                        break;
                }
            }

            return unquotedString.Append('\\', precedingBackslashes).ToString();
        }

        /// <summary>
        /// Removes unescaped (i.e. non-literal) double-quotes, and escaping backslashes, from the given string.
        /// </summary>
        /// <param name="input"></param>
        /// <returns>The given string in unquoted form.</returns>
        internal static string Unquote(string input)
        {
            int doubleQuotesRemoved;
            return Unquote(input, out doubleQuotesRemoved);
        }
    }
}
