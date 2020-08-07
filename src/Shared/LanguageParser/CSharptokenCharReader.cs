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
     * Class:   CSharpTokenCharReader
     *
     * Reads over the contents of a C# source file (in the form of a string). 
     * Provides utility functions for dealing with C#-specific tokens.
     *
     */
    sealed internal class CSharpTokenCharReader : TokenCharReader
    {
        /*
         * Method:  CSharpTokenCharReader
         * 
         * Construct
         */
        internal CSharpTokenCharReader(Stream binaryStream, bool forceANSI)
            : base(binaryStream, forceANSI)
        {
        }

        /*
         * Method:  SinkLongIntegerSuffix
         * 
         * Skip C# integer literal long suffix: L, U, l, u, ul, etc.                    
         */
        internal bool SinkLongIntegerSuffix()
        {
            // Skip the long integer suffix if there is one.
            if (CurrentCharacter == 'U' || CurrentCharacter == 'u')
            {
                Skip();
                if (CurrentCharacter == 'L' || CurrentCharacter == 'l')
                {
                    Skip();
                }
            }
            else if (CurrentCharacter == 'L' || CurrentCharacter == 'l')
            {
                Skip();
                if (CurrentCharacter == 'U' || CurrentCharacter == 'u')
                {
                    Skip();
                }
            }

            return true; // An integer suffix can be zero characters, so there's always a match.
        }

        /*
         * Method:  SinkOperatorOrPunctuator
         * 
         * Determine whether this is a C# operator or punctuator
         */
        internal bool SinkOperatorOrPunctuator()
        {
            const string operatorsAndPunctuators = "{}[]().,:;+-*/%&|^!~=<>?";
            if (operatorsAndPunctuators.IndexOf(CurrentCharacter) == -1)
            {
                return false;
            }
            Skip();
            return true;
        }

        /*
         * Method:  SinkStringEscape
         * 
         * Determine whether this is a valid escape character for strings?
         */
        internal bool SinkStringEscape()
        {
            switch (CurrentCharacter)
            {
                case '\'':
                case '\"':
                case '0':
                case 'a':
                case 'b':
                case 'f':
                case 'n':
                case 'r':
                case 't':
                case 'u':
                case 'U':
                case 'x':
                case 'v':
                case '\x005c' /* backslash */:
                    Skip();
                    return true;
            }
            return false;
        }

        /*
         * Method:  MatchRegularStringLiteral
         * 
         * Determine whether this is a regular C# string literal character
         */
        internal bool MatchRegularStringLiteral()
        {
            if (CurrentCharacter == '\"' || CurrentCharacter == '\\' || TokenChar.IsNewLine(CurrentCharacter))
            {
                return false;
            }

            return true;
        }

        /*
         * Method:  SinkMultipleWhiteSpace
         * 
         * Sink some C# whitespace
         */
        internal bool SinkMultipleWhiteSpace()
        {
            int count = 0;
            while (!EndOfLines && Char.IsWhiteSpace(CurrentCharacter))
            {
                Skip();
                ++count;
            }

            return count > 0;
        }
    }
}

