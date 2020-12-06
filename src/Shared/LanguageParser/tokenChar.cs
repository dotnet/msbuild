// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

namespace Microsoft.Build.Shared.LanguageParser
{
    /// <summary>
    /// Utility functions for classifying characters that might be found in a sources file.
    /// </summary>
    internal static class TokenChar
    {
        /// <summary>
        /// Determine whether a given character is a newline character
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static internal bool IsNewLine(char c)
        {
            // From the C# spec and vb specs, newline characters are:
            return c == 0x000d        // Carriage return
                    || c == 0x000a        // Linefeed
                    || c == 0x2028        // Line separator
                    || c == 0x2029        // Paragraph separator                       
                        ;
        }

        /// <summary>
        /// Determine whether a given character is a letter character
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static internal bool IsLetter(char c)
        {
            UnicodeCategory cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);

            // From 2.4.2 of the C# Language Specification
            // letter-character:
            if (
                    cat == UnicodeCategory.UppercaseLetter
                    || cat == UnicodeCategory.LowercaseLetter
                    || cat == UnicodeCategory.TitlecaseLetter
                    || cat == UnicodeCategory.ModifierLetter
                    || cat == UnicodeCategory.OtherLetter
                    || cat == UnicodeCategory.LetterNumber
                )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine whether a given character is a decimal digit character
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static internal bool IsDecimalDigit(char c)
        {
            UnicodeCategory cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);

            // From 2.4.2 of the C# Language Specification
            // decimal-digit-character:
            if (
                    cat == UnicodeCategory.DecimalDigitNumber
                )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine whether a given character is a connecting character
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static internal bool IsConnecting(char c)
        {
            UnicodeCategory cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);

            // From 2.4.2 of the C# Language Specification
            // connecting-character:
            if
            (
                cat == UnicodeCategory.ConnectorPunctuation
            )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine whether a given character is a combining character
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static internal bool IsCombining(char c)
        {
            UnicodeCategory cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);

            // From 2.4.2 of the C# Language Specification
            // combining-character:
            if (
                    cat == UnicodeCategory.NonSpacingMark // Mn
                    || cat == UnicodeCategory.SpacingCombiningMark  // Mc 
                )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine whether a given character is a C# formatting character
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static internal bool IsFormatting(char c)
        {
            UnicodeCategory cat = System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c);

            // From 2.4.2 of the C# Language Specification
            // formatting-character:
            if (
                    cat == UnicodeCategory.Format  // Cf
                )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine whether a given character is a hex digit character
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static internal bool IsHexDigit(char c)
        {
            // From 2.4.4.2 of the C# Language Specification
            // hex-digit:
            if
            (
                (c >= '0' && c <= '9')
                || (c >= 'A' && c <= 'F')
                || (c >= 'a' && c <= 'f')
            )
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Determine whether a given character is an octal digit character
        /// </summary>
        /// <param name="c"></param>
        /// <returns></returns>
        static internal bool IsOctalDigit(char c)
        {
            if
            (
                c >= '0' && c <= '7'
            )
            {
                return true;
            }
            return false;
        }
    }
}

