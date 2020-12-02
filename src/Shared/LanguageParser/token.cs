// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace Microsoft.Build.Shared.LanguageParser
{
    /*
     * Class:   Token
     *
     * Base class for all token classes.
     *
     */
    internal abstract class Token
    {
        // The text from the originating source file that caused this token.
        private string _innerText = null;
        // The line number that the token fell on.
        private int _line = 0;

        /*
         * Method:  InnerText
         * 
         * Get or set the InnerText for this token
         */
        internal string InnerText
        {
            get { return _innerText; }
            set { _innerText = value; }
        }

        /*
         * Method:  Line
         * 
         * Get or set the Line for this token
         */
        internal int Line
        {
            get
            {
                return _line;
            }
            set
            {
                _line = value;
            }
        }

        /*
         * Method:  EqualsIgnoreCase
         * 
         * Return true if the given string equals the content of this token
         */
        internal bool EqualsIgnoreCase(string compareTo)
        {
            return String.Equals(_innerText, compareTo, StringComparison.OrdinalIgnoreCase);
        }
    }

    /*
        Table of tokens shared by the parsers.
        Tokens that are specific to a particular parser are nested within the given
        parser class.
    */
    internal class WhitespaceToken : Token { }
    internal abstract class LiteralToken : Token { }
    internal class BooleanLiteralToken : Token { } // i.e. true or false
    internal abstract class IntegerLiteralToken : Token { } // i.e. a literal integer
    internal class HexIntegerLiteralToken : IntegerLiteralToken { } // i.e. a hex literal integer
    internal class DecimalIntegerLiteralToken : IntegerLiteralToken { } // i.e. a hex literal integer
    internal class StringLiteralToken : Token { } // i.e. A string value.
    internal abstract class SyntaxErrorToken : Token { } // A syntax error.
    internal class ExpectedIdentifierToken : SyntaxErrorToken { }
    internal class ExpectedValidHexDigitToken : SyntaxErrorToken { } // Got a non-hex digit when we expected to have one.
    internal class EndOfFileInsideStringToken : SyntaxErrorToken { } // The file ended inside a string.
    internal class UnrecognizedToken : SyntaxErrorToken { } // An unrecognized token was spotted.
    internal class CommentToken : Token { }
    internal class IdentifierToken : Token { } // An identifier
    internal class KeywordToken : Token { } // An keyword
    internal class PreprocessorToken : Token { } // #if, #region, etc.
    internal class OpenConditionalDirectiveToken : PreprocessorToken { }
    internal class CloseConditionalDirectiveToken : PreprocessorToken { }
    internal class OperatorOrPunctuatorToken : Token { } // One of the predefined operators or punctuators
    internal class OperatorToken : OperatorOrPunctuatorToken { }
}
