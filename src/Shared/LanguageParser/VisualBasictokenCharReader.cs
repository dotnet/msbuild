// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Globalization;

namespace Microsoft.Build.Shared.LanguageParser
{
    /*
     * Class:   VisualBasicTokenCharReader
     *
     * Reads over the contents of a vb source file (in the form of a string). 
     * Provides utility functions for dealing with VB-specific tokens.
     *
     */
    sealed internal class VisualBasicTokenCharReader : TokenCharReader
    {
        /*
         * Method:  VisualBasicTokenCharReader
         * 
         * Construct
         */
        internal VisualBasicTokenCharReader(Stream binaryStream, bool forceANSI)
            : base(binaryStream, forceANSI)
        {
        }

        /*
         * Method:  SinkSeparatorCharacter
         * 
         * Matches a vb separator character.
         */
        internal bool SinkSeparatorCharacter()
        {
            if
            (
                   CurrentCharacter == '('
                || CurrentCharacter == ')'
                || CurrentCharacter == '!'
                || CurrentCharacter == '#'
                || CurrentCharacter == ','
                || CurrentCharacter == '.'
                || CurrentCharacter == ':'
                || CurrentCharacter == '{'
                || CurrentCharacter == '}'
            )
            {
                Skip();
                return true;
            }

            return false;
        }

        /*
         * Method:  SinkLineContinuationCharacter
         * 
         * Matches a vb line continuation character.
         */
        internal bool SinkLineContinuationCharacter()
        {
            if
            (
                CurrentCharacter == '_'
            )
            {
                Skip();
                return true;
            }

            return false;
        }

        /*
         * Method:  SinkLineCommentStart
         * 
         * Matches a vb start of comment indicator
         */
        internal bool SinkLineCommentStart()
        {
            if (Sink("\'"))
            {
                return true;
            }
            else
            {
                int previousPosition = Position;

                if (SinkIgnoreCase("rem"))
                {
                    if (SinkWhiteSpace())
                    {
                        return true;
                    }

                    // We've probably found an Identifier that starts with "rem",
                    // so return to the previous position.
                    Position = previousPosition;
                }
            }
            return false;
        }

        /*
         * Method:  SinkHexIntegerPrefix
         * 
         * Matches a vb hex integer prefix
         */
        internal bool SinkHexIntegerPrefix()
        {
            if (SinkIgnoreCase("&H"))
            {
                return true;
            }

            return false;
        }

        /*
         * Method:  SinkOctalIntegerPrefix
         * 
         * Matches a vb octal integer prefix
         */
        internal bool SinkOctalIntegerPrefix()
        {
            if (SinkIgnoreCase("&O"))
            {
                return true;
            }

            return false;
        }

        /*
         * Method:  SinkWhiteSpace
         * 
         * Sink a single whitespace character.
         * In vb, newlines are not considered whitespace.
         */
        internal bool SinkWhiteSpace()
        {
            if (Char.IsWhiteSpace(CurrentCharacter) && !TokenChar.IsNewLine(CurrentCharacter))
            {
                Skip();
                return true;
            }
            return false;
        }

        /*
         * Method:  SinkIntegerSuffix
         * 
         * Sink a vb integer suffix.
         */
        internal bool SinkIntegerSuffix()
        {
            switch (CurrentCharacter)
            {
                case 'S':
                case 's':
                case 'I':
                case 'i':
                case 'L':
                case 'l':
                    Skip();
                    return true;
            }
            return true; // An integer suffix can be zero characters, so there's always a match.
        }

        /*
         * Method:  SinkDecimalIntegerSuffix
         * 
         * Sink a vb decimal integer suffix.
         * Couldn't find this documented anywhere, but a decimal (as opposed to hex or octal)
         * is also allowed a trailing '@', '!', '#' or '&'
         */
        internal bool SinkDecimalIntegerSuffix()
        {
            switch (CurrentCharacter)
            {
                case 'S':
                case 's':
                case 'I':
                case 'i':
                case 'L':
                case 'l':
                case '@':
                case '!':
                case '#':
                case '&':
                case '%':

                    Skip();
                    return true;
            }
            return true; // An integer suffix can be zero characters, so there's always a match.
        }


        /*
         * Method:  SinkOctalDigits
         * 
         * Sink multiple octal digits.
         */
        internal bool SinkMultipleOctalDigits()
        {
            int count = 0;
            while (TokenChar.IsOctalDigit(CurrentCharacter))
            {
                ++count;
                Skip();
            }
            return count > 0;     // Must match at least one  
        }

        /*
         * Method:  SinkOperator
         * 
         * Determine whether this is a vb operator.
         */
        internal bool SinkOperator()
        {
            const string operators = @"&|*+-/\^<=>";
            if (operators.IndexOf(CurrentCharacter) == -1)
            {
                return false;
            }
            Skip();
            return true;
        }

        /*
         * Method:  SinkTypeCharacter
         * 
         * Identifiers in vb can end with a special character to indicate type:
         *   IntegerTypeCharacter ::= %
         *   LongTypeCharacter ::= &
         *   DecimalTypeCharacter ::= @
         *   SingleTypeCharacter ::= !
         *   DoubleTypeCharacter ::= #
         *   StringTypeCharacter ::= $
         */
        internal bool SinkTypeCharacter()
        {
            const string types = @"%&@!#$";
            if (types.IndexOf(CurrentCharacter) == -1)
            {
                return false;
            }
            Skip();
            return true;
        }
    }
}

