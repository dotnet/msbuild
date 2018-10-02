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
    * Class:   VisualBasicTokenEnumerator
    *
    * Given vb sources, enumerate over all tokens.
    *
    */
    sealed internal class VisualBasicTokenEnumerator : TokenEnumerator
    {
        // Reader over the sources.
        private VisualBasicTokenCharReader _reader = null;

        /*
        * Method:  TokenEnumerator
        * 
        * Construct
        */
        internal VisualBasicTokenEnumerator(Stream binaryStream, bool forceANSI)
        {
            _reader = new VisualBasicTokenCharReader(binaryStream, forceANSI);
        }

        /*
        * Method:  FindNextToken
        * 
        * Find the next token. Return 'true' if one was found. False, otherwise.
        */
        override internal bool FindNextToken()
        {
            int startPosition = _reader.Position;

            // VB docs claim whitespace is Unicode category Zs. However,
            // this category does not contain tabs. Assuming a less restrictive
            // definition for whitespace...
            if (_reader.SinkWhiteSpace())
            {
                while (_reader.SinkWhiteSpace())
                {
                }

                // Now, we need to check for the line continuation character.
                if (_reader.SinkLineContinuationCharacter())    // Line continuation is '_'
                {
                    // Save the current position because we may need to come back here.
                    int savePosition = _reader.Position - 1;

                    // Skip all whitespace after the '_'
                    while (_reader.SinkWhiteSpace())
                    {
                    }

                    // Now, skip all the newlines.
                    // Need at least one newline for this to count as line continuation.
                    int count = 0;
                    while (_reader.SinkNewLine())
                    {
                        ++count;
                    }

                    if (count > 0)
                    {
                        current = new VisualBasicTokenizer.LineContinuationToken();
                        return true;
                    }

                    // Otherwise, fall back to plain old whitespace.
                    _reader.Position = savePosition;
                }

                current = new WhitespaceToken();
                return true;
            }
            // Line terminators are separate from whitespace and are significant.
            else if (_reader.SinkNewLine())
            {
                // We want one token per line terminator.
                current = new VisualBasicTokenizer.LineTerminatorToken();
                return true;
            }
            // Check for a comment--either those that start with ' or rem.
            else if (_reader.SinkLineCommentStart())
            {
                // Skip to the first EOL.
                _reader.SinkToEndOfLine();

                current = new CommentToken();
                return true;
            }
            // Identifier or keyword?
            else if
            (
                // VB allows escaping of identifiers by surrounding them with []
                // In other words,
                //      Date is a keyword but,
                //      [Date] is an identifier.
                _reader.CurrentCharacter == '[' ||
                _reader.MatchNextIdentifierStart()
            )
            {
                bool escapedIdentifier = false;
                if (_reader.CurrentCharacter == '[')
                {
                    escapedIdentifier = true;
                    _reader.SinkCharacter();

                    // Now, the next character must be an identifier start.
                    if (!_reader.SinkIdentifierStart())
                    {
                        current = new ExpectedIdentifierToken();
                        return true;
                    }
                }

                // Sink the rest of the identifier.
                while (_reader.SinkIdentifierPart())
                {
                }

                // If this was an escaped identifier the we need to get the terminating ']'.
                if (escapedIdentifier)
                {
                    if (!_reader.Sink("]"))
                    {
                        current = new ExpectedIdentifierToken();
                        return true;
                    }
                }
                else
                {
                    // Escaped identifiers are not allowed to have trailing type character.
                    _reader.SinkTypeCharacter(); // Type character is optional.
                }

                // An identifier that is only a '_' is illegal because it is
                // ambiguous with line continuation
                string identifierOrKeyword = _reader.GetCurrentMatchedString(startPosition);
                if (identifierOrKeyword == "_" || identifierOrKeyword == "[_]" || identifierOrKeyword == "[]")
                {
                    current = new ExpectedIdentifierToken();
                    return true;
                }

                // Make an upper-case version in order to check whether this may be a keyword.
                string upper = identifierOrKeyword.ToUpperInvariant();

                switch (upper)
                {
                    default:

                        if (Array.IndexOf(s_keywordList, upper) >= 0)
                        {
                            current = new KeywordToken();
                            return true;
                        }

                        // Create the token.
                        current = new IdentifierToken();

                        // Trim off the [] if this is an escaped identifier.
                        if (escapedIdentifier)
                        {
                            current.InnerText = identifierOrKeyword.Substring(1, identifierOrKeyword.Length - 2);
                        }
                        return true;
                    case "FALSE":
                    case "TRUE":
                        current = new BooleanLiteralToken();
                        return true;
                }
            }
            // Is it a hex integer?
            else if (_reader.SinkHexIntegerPrefix())
            {
                if (!_reader.SinkMultipleHexDigits())
                {
                    current = new ExpectedValidHexDigitToken();
                    return true;
                }

                // Sink a suffix if there is one.                    
                _reader.SinkIntegerSuffix();

                current = new HexIntegerLiteralToken();
                return true;
            }
            // Is it an octal integer?
            else if (_reader.SinkOctalIntegerPrefix())
            {
                if (!_reader.SinkMultipleOctalDigits())
                {
                    current = new VisualBasicTokenizer.ExpectedValidOctalDigitToken();
                    return true;
                }

                // Sink a suffix if there is one.                    
                _reader.SinkIntegerSuffix();

                current = new VisualBasicTokenizer.OctalIntegerLiteralToken();
                return true;
            }
            // Is it a decimal integer?
            else if (_reader.SinkMultipleDecimalDigits())
            {
                // Sink a suffix if there is one.                    
                _reader.SinkDecimalIntegerSuffix();

                current = new DecimalIntegerLiteralToken();
                return true;
            }
            // Preprocessor line
            else if (_reader.CurrentCharacter == '#')
            {
                if (_reader.SinkIgnoreCase("#if"))
                {
                    current = new OpenConditionalDirectiveToken();
                }
                else if (_reader.SinkIgnoreCase("#end if"))
                {
                    current = new CloseConditionalDirectiveToken();
                }
                else
                {
                    current = new PreprocessorToken();
                }

                _reader.SinkToEndOfLine();

                return true;
            }
            // Is it a separator?
            else if (_reader.SinkSeparatorCharacter())
            {
                current = new VisualBasicTokenizer.SeparatorToken();
                return true;
            }
            // Is it an operator?
            else if (_reader.SinkOperator())
            {
                current = new OperatorToken();
                return true;
            }
            // A string?
            else if (_reader.Sink("\""))
            {
                do
                {
                    // Inside a verbatim string "" is treated as a special character
                    while (_reader.Sink("\"\""))
                    {
                    }
                }
                while (!_reader.EndOfLines && _reader.SinkCharacter() != '\"');

                // Can't end a file inside a string 
                if (_reader.EndOfLines)
                {
                    current = new EndOfFileInsideStringToken();
                    return true;
                }

                current = new StringLiteralToken();
                return true;
            }


            // We didn't recognize the token, so this is a syntax error. 
            _reader.SinkCharacter();
            current = new UnrecognizedToken();
            return true;
        }

        private static readonly string[] s_keywordList =
                                              { "ADDHANDLER", "ADDRESSOF", "ANDALSO", "ALIAS",
                                                "AND",  "ANSI",  "AS",  "ASSEMBLY",
                                                "AUTO",  "BOOLEAN",  "BYREF",  "BYTE",
                                                "BYVAL",  "CALL",  "CASE",  "CATCH",
                                                "CBOOL",  "CBYTE",  "CCHAR",  "CDATE",
                                                "CDEC",  "CDBL",  "CHAR",  "CINT",
                                                "CLASS",  "CLNG",  "COBJ",  "CONST", "CONTINUE", "CSBYTE",
                                                "CSHORT",  "CSNG",  "CSTR",  "CTYPE", "CUINT", "CULNG", "CUSHORT",
                                                "DATE",  "DECIMAL",  "DECLARE",  "DEFAULT",
                                                "DELEGATE",  "DIM",  "DIRECTCAST",  "DO",
                                                "DOUBLE",  "EACH",  "ELSE",  "ELSEIF",
                                                "END", "ENDIF", "ENUM",  "ERASE",  "ERROR",
                                                "EVENT",  "EXIT",  "FALSE", "FINALLY",
                                                "FOR",  "FRIEND",  "FUNCTION",  "GET",
                                                "GETTYPE",  "GLOBAL", "GOSUB",  "GOTO",  "HANDLES",
                                                "IF",  "IMPLEMENTS",  "IMPORTS",  "IN",
                                                "INHERITS",  "INTEGER",  "INTERFACE",  "IS", "ISNOT",
                                                "LET",  "LIB",  "LIKE",  "LONG",
                                                "LOOP",  "ME",  "MOD",  "MODULE",
                                                "MUSTINHERIT",  "MUSTOVERRIDE",  "MYBASE",  "MYCLASS",
                                                "NAMESPACE",  "NARROWING", "NEW",  "NEXT",  "NOT",
                                                "NOTHING",  "NOTINHERITABLE",  "NOTOVERRIDABLE",  "OBJECT",
                                                "OF", "ON",  "OPERATOR", "OPTION",  "OPTIONAL",  "OR",
                                                "ORELSE",  "OVERLOADS",  "OVERRIDABLE",  "OVERRIDES",
                                                "PARAMARRAY",  "PARTIAL", "PRESERVE",  "PRIVATE",  "PROPERTY",
                                                "PROTECTED",  "PUBLIC",  "RAISEEVENT",  "READONLY",
                                                "REDIM",  "REM",  "REMOVEHANDLER",  "RESUME",
                                                "RETURN",  "SBYTE", "SELECT",  "SET",  "SHADOWS",
                                                "SHARED",  "SHORT",  "SINGLE",  "STATIC",
                                                "STEP",  "STOP",  "STRING",  "STRUCTURE",
                                                "SUB",  "SYNCLOCK",  "THEN",  "THROW",
                                                "TO",  "TRUE", "TRY",  "TRYCAST", "TYPEOF",
                                                "UNICODE",  "UINTEGER", "ULONG", "UNTIL",  "USHORT", "USING", "VARIANT",  "WEND", "WHEN",
                                                "WHILE",  "WIDENING", "WITH",  "WITHEVENTS",  "WRITEONLY",
                                                "XOR" };


        /*
        * Method:  Reader
        * 
        * Return the token char reader.
        */
        override internal TokenCharReader Reader
        {
            get
            {
                return _reader;
            }
        }
    }
}
