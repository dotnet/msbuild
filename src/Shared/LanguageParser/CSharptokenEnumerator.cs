// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;

namespace Microsoft.Build.Shared.LanguageParser
{
    /*
    * Class:   CSharpTokenEnumerator
    *
    * Given C# sources, enumerate over all tokens.
    *
    */
    sealed internal class CSharpTokenEnumerator : TokenEnumerator
    {
        // Reader over the sources.
        private CSharpTokenCharReader _reader = null;

        /*
        * Method:  TokenEnumerator
        * 
        * Construct
        */
        internal CSharpTokenEnumerator(Stream binaryStream, bool forceANSI)
        {
            _reader = new CSharpTokenCharReader(binaryStream, forceANSI);
        }

        /*
        * Method:  FindNextToken
        * 
        * Find the next token. Return 'true' if one was found. False, otherwise.
        */
        override internal bool FindNextToken()
        {
            int startPosition = _reader.Position;

            // Dealing with whitespace?
            if (_reader.SinkMultipleWhiteSpace())
            {
                current = new WhitespaceToken();
                return true;
            }
            // Check for one-line comment
            else if (_reader.Sink("//"))
            {
                // Looks like a one-line comment. Follow it to the End-of-line
                _reader.SinkToEndOfLine();

                current = new CommentToken();
                return true;
            }
            // Check for multi-line comment
            else if (_reader.Sink("/*"))
            {
                _reader.SinkUntil("*/");

                // Was the ending */ found?
                if (_reader.EndOfLines)
                {
                    // No. There was a /* without a */. Return this a syntax error token.
                    current = new CSharpTokenizer.EndOfFileInsideCommentToken();
                    return true;
                }

                current = new CommentToken();
                return true;
            }
            // Handle chars
            else if (_reader.Sink("\'"))
            {
                while (_reader.CurrentCharacter != '\'')
                {
                    if (_reader.Sink("\\"))
                    {
                        /* reader.Skip the escape sequence. 
                            This isn't exactly right. We should detect:
                            
                            simple-escape-sequence: one of 
                            \' \" \\ \0 \a \b \f \n \r \t \v 
                            
                            hexadecimal-escape-sequence: 
                            \x   hex-digit   hex-digit[opt]   hex-digit[opt]  hex-digit[opt]                                
                        */
                    }

                    _reader.SinkCharacter();
                }

                if (_reader.SinkCharacter() != '\'')
                {
                    Debug.Assert(false, "Code defect in tokenizer: Should have yielded a closing tick.");
                }
                current = new CSharpTokenizer.CharLiteralToken();
                return true;
            }
            // Check for verbatim string
            else if (_reader.Sink("@\""))
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

                // reader.Skip the ending quote.
                current = new StringLiteralToken();
                current.InnerText = _reader.GetCurrentMatchedString(startPosition).Substring(1);
                return true;
            }
            // Check for a quoted string.
            else if (_reader.Sink("\""))
            {
                while (_reader.CurrentCharacter == '\\' || _reader.MatchRegularStringLiteral())
                {
                    // See if we have an escape sequence.
                    if (_reader.SinkCharacter() == '\\')
                    {
                        // This is probably an escape character.
                        if (_reader.SinkStringEscape())
                        {
                            // This isn't nearly right. We just do barely enough to make a string
                            // with an embedded escape sequence return _some_ string whose start and 
                            // end match the real bounds of the string.
                        }
                        else
                        {
                            // This is a compiler error. 
                            _reader.SinkCharacter();
                            current = new CSharpTokenizer.UnrecognizedStringEscapeToken();
                            return true;
                        }
                    }
                }

                // Is it a newline?
                if (TokenChar.IsNewLine(_reader.CurrentCharacter))
                {
                    current = new CSharpTokenizer.NewlineInsideStringToken();
                    return true;
                }

                // Create the token.
                if (_reader.SinkCharacter() != '\"')
                {
                    Debug.Assert(false, "Defect in tokenizer: Should have yielded a terminating quote.");
                }
                current = new StringLiteralToken();
                return true;
            }
            // Identifier or keyword?
            else if
            (
                // From 2.4.2 Identifiers: A '@' can be used to prefix an identifier so that a keyword can be used as an identifier.
                _reader.CurrentCharacter == '@' ||
                _reader.MatchNextIdentifierStart()
            )
            {
                if (_reader.CurrentCharacter == '@')
                {
                    _reader.SinkCharacter();
                }

                // Now, the next character must be an identifier start.
                if (!_reader.SinkIdentifierStart())
                {
                    current = new ExpectedIdentifierToken();
                    return true;
                }

                // Sink the rest of the identifier.                     
                while (_reader.SinkIdentifierPart())
                {
                }
                string identifierOrKeyword = _reader.GetCurrentMatchedString(startPosition);

                switch (identifierOrKeyword)
                {
                    default:

                        if (Array.IndexOf(s_keywordList, identifierOrKeyword) >= 0)
                        {
                            current = new KeywordToken();
                            return true;
                        }

                        // If the identifier starts with '@' then we need to strip it off.
                        // The '@' is for escaping so that we can have an identifier called
                        // the same thing as a reserved keyword (i.e. class, if, foreach, etc)
                        string identifier = _reader.GetCurrentMatchedString(startPosition);
                        if (identifier.StartsWith("@", StringComparison.Ordinal))
                        {
                            identifier = identifier.Substring(1);
                        }

                        // Create the token.
                        current = new IdentifierToken();
                        current.InnerText = identifier;
                        return true;
                    case "false":
                    case "true":
                        current = new BooleanLiteralToken();
                        return true;
                    case "null":
                        current = new CSharpTokenizer.NullLiteralToken();
                        return true;
                }
            }
            // Open scope
            else if (_reader.Sink("{"))
            {
                current = new CSharpTokenizer.OpenScopeToken();
                return true;
            }
            // Close scope
            else if (_reader.Sink("}"))
            {
                current = new CSharpTokenizer.CloseScopeToken();
                return true;
            }
            // Hexidecimal integer literal
            else if (_reader.SinkIgnoreCase("0x"))
            {
                // Sink the hex digits.
                if (!_reader.SinkMultipleHexDigits())
                {
                    current = new ExpectedValidHexDigitToken();
                    return true;
                }

                // Skip the L, U, l, u, ul, etc.                    
                _reader.SinkLongIntegerSuffix();

                current = new HexIntegerLiteralToken();
                return true;
            }
            // Decimal integer literal
            else if (_reader.SinkMultipleDecimalDigits())
            {
                // reader.Skip the L, U, l, u, ul, etc.                    
                _reader.SinkLongIntegerSuffix();

                current = new DecimalIntegerLiteralToken();
                return true;
            }
            // Check for single-digit operators and punctuators
            else if (_reader.SinkOperatorOrPunctuator())
            {
                current = new OperatorOrPunctuatorToken();
                return true;
            }
            // Preprocessor line
            else if (_reader.CurrentCharacter == '#')
            {
                if (_reader.Sink("#if"))
                {
                    current = new OpenConditionalDirectiveToken();
                }
                else if (_reader.Sink("#endif"))
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

            // We didn't recognize the token, so this is a syntax error. 
            _reader.SinkCharacter();
            current = new UnrecognizedToken();
            return true;
        }

        private static readonly string[] s_keywordList =
                                              { "abstract",  "as",  "base",  "bool",  "break",
                                                "byte",  "case",  "catch",  "char",  "checked",
                                                "class",  "const",  "continue",  "decimal",  "default",
                                                "delegate",  "do",  "double",  "else",  "enum",
                                                "event",  "explicit",  "extern",  "finally",  "fixed",
                                                "float",  "for",  "foreach",  "goto",  "if",
                                                "implicit",  "in",  "int",  "interface",  "internal",
                                                "is",  "lock",  "long",  "namespace",  "new",
                                                "object",  "operator",  "out",  "override",  "params",
                                                "private",  "protected",  "public",  "readonly",
                                                "ref",  "return",  "sbyte",  "sealed",  "short",
                                                "sizeof",  "stackalloc",  "static",  "string",
                                                "struct",  "switch",  "this",  "throw",  "try",
                                                "typeof",  "uint",  "ulong",  "unchecked",  "unsafe",
                                                "ushort",  "using",  "virtual",  "void",  "volatile",
                                                "while" };


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
