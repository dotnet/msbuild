// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Text;
using System.Globalization;

namespace Microsoft.Build.BuildEngine.Shared
{
    /// <summary>
    /// This class implements static methods to assist with unescaping of %XX codes
    /// in the MSBuild file format.
    /// </summary>
    /// <owner>RGoel</owner>
    static internal class EscapingUtilities
    {
        /// <summary>
        /// Replaces all instances of %XX in the input string with the character represented
        /// by the hexadecimal number XX. 
        /// </summary>
        /// <param name="escapedString"></param>
        /// <returns>unescaped string</returns>
        internal static string UnescapeAll
        (
            string escapedString
        )
        {
            bool throwAwayBool;
            return UnescapeAll(escapedString, out throwAwayBool);
        }

        /// <summary>
        /// Replaces all instances of %XX in the input string with the character represented
        /// by the hexadecimal number XX. 
        /// </summary>
        /// <param name="escapedString"></param>
        /// <param name="escapingWasNecessary"></param>
        /// <returns>unescaped string</returns>
        internal static string UnescapeAll
        (
            string escapedString,
            out bool escapingWasNecessary
        )
        {
            ErrorUtilities.VerifyThrow(escapedString != null, "Null strings not allowed.");

            escapingWasNecessary = false;

            // If there are no percent signs, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            int indexOfPercent = escapedString.IndexOf('%');
            if (indexOfPercent == -1)
            {
                return escapedString;
            }
            
            // This is where we're going to build up the final string to return to the caller.
            StringBuilder unescapedString = new StringBuilder();

            int currentPosition = 0;

            // Loop until there are no more percent signs in the input string.
            while (indexOfPercent != -1)
            {
                // There must be two hex characters following the percent sign
                // for us to even consider doing anything with this.
                if  (
                        (indexOfPercent <= (escapedString.Length - 3)) &&
                        Uri.IsHexDigit(escapedString[indexOfPercent + 1]) &&
                        Uri.IsHexDigit(escapedString[indexOfPercent + 2])
                    )
                {
                    // First copy all the characters up to the current percent sign into
                    // the destination.
                    unescapedString.Append(escapedString, currentPosition, indexOfPercent - currentPosition);

                    // Convert the %XX to an actual real character.
                    string hexString = escapedString.Substring(indexOfPercent + 1, 2);
                    char unescapedCharacter = (char) int.Parse(hexString, System.Globalization.NumberStyles.HexNumber, 
                        CultureInfo.InvariantCulture);

                    // if the unescaped character is not on the exception list, append it
                    unescapedString.Append(unescapedCharacter);

                    // Advance the current pointer to reflect the fact that the destination string
                    // is up to date with everything up to and including this escape code we just found.
                    currentPosition = indexOfPercent + 3;

                    escapingWasNecessary = true;
                }

                // Find the next percent sign.
                indexOfPercent = escapedString.IndexOf('%', indexOfPercent + 1);
            }

            // Okay, there are no more percent signs in the input string, so just copy the remaining
            // characters into the destination.
            unescapedString.Append(escapedString, currentPosition, escapedString.Length - currentPosition);

            return unescapedString.ToString();
        }

        /// <summary>
        /// Adds instances of %XX in the input string where the char char to be escaped appears
        /// XX is the hex value of the ASCII code for the char.
        /// </summary>
        /// <param name="unescapedString"></param>
        /// <returns>escaped string</returns>
        internal static string Escape
        (
            string unescapedString
        )
        {
            ErrorUtilities.VerifyThrow(unescapedString != null, "Null strings not allowed.");

            // If there are no special chars, just return the original string immediately.
            // Don't even instantiate the StringBuilder.
            if (!ContainsReservedCharacters(unescapedString))
            {
                return unescapedString;
            }

            // This is where we're going to build up the final string to return to the caller.
            StringBuilder escapedString = new StringBuilder(unescapedString);

            // Replace each unescaped special character with an escape sequence one            
            foreach (char unescapedChar in charsToEscape)
            {
                int unescapedCharCode = Convert.ToInt32(unescapedChar);
                string escapedCharacterCode = string.Format(CultureInfo.InvariantCulture, "%{0:x00}", unescapedCharCode);
                escapedString.Replace(unescapedChar.ToString(CultureInfo.InvariantCulture), escapedCharacterCode);
            }

            return escapedString.ToString();
        }

        /// <summary>
        /// Before trying to actually escape the string, it can be useful to call this method to determine
        /// if escaping is necessary at all.  This can save lots of calls to copy around item metadata
        /// that is really the same whether escaped or not.
        /// </summary>
        /// <param name="unescapedString"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private static bool ContainsReservedCharacters
            (
            string unescapedString
            )
        {
            return (-1 != unescapedString.IndexOfAny(charsToEscape));
        }

        /// <summary>
        /// Determines whether the string contains the escaped form of '*' or '?'.
        /// </summary>
        /// <param name="escapedString"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        internal static bool ContainsEscapedWildcards
            (
            string escapedString
            )
        {
            if (-1 != escapedString.IndexOf('%'))
            {
                // It has a '%' sign.  We have promise.
                if  (
                        (-1 != escapedString.IndexOf("%2", StringComparison.Ordinal)) ||
                        (-1 != escapedString.IndexOf("%3", StringComparison.Ordinal))
                    )
                {
                    // It has either a '%2' or a '%3'.  This is looking very promising.
                    return 
                        (
                            (-1 != escapedString.IndexOf("%2a", StringComparison.Ordinal)) ||
                            (-1 != escapedString.IndexOf("%2A", StringComparison.Ordinal)) ||
                            (-1 != escapedString.IndexOf("%3f", StringComparison.Ordinal)) ||
                            (-1 != escapedString.IndexOf("%3F", StringComparison.Ordinal))
                        );
                }
            }
            return false;
        }

        /// <summary>
        /// Special characters that need escaping.
        /// It's VERY important that the percent character is the FIRST on the list - since it's both a character 
        /// we escape and use in escape sequences, we can unintentionally escape other escape sequences if we 
        /// don't process it first. Of course we'll have a similar problem if we ever decide to escape hex digits 
        /// (that would require rewriting the algorithm) but since it seems unlikely that we ever do, this should
        /// be good enough to avoid complicating the algorithm at this point.
        /// </summary>
        private static char[] charsToEscape = { '%', '*', '?', '@', '$', '(', ')', ';', '\'' };
    }
}
