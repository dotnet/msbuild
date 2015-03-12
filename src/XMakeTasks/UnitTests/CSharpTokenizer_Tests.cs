// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;

using Microsoft.Build.Shared.LanguageParser;

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    sealed public class CSharpTokenizerTests
    {
        // Simple whitespace handling.
        [TestMethod]
        public void Empty() { AssertTokenize("", "", 0); }
        [TestMethod]
        public void OneSpace() { AssertTokenize(" ", " \x0d", ".Whitespace"); }
        [TestMethod]
        public void TwoSpace() { AssertTokenize("  ", "  \x0d", ".Whitespace"); }
        [TestMethod]
        public void Tab() { AssertTokenize("\t", "\t\x0d", ".Whitespace"); }
        [TestMethod]
        public void TwoTab() { AssertTokenize("\t\t", "\t\t\x0d", ".Whitespace"); }
        [TestMethod]
        public void SpaceTab() { AssertTokenize(" \t", " \t\x0d", ".Whitespace"); }
        [TestMethod]
        public void CrLf() { AssertTokenize("\x0d\x0a", ".Whitespace"); }
        [TestMethod]
        public void SpaceCrLfSpace() { AssertTokenize(" \x0d\x0a ", " \x0d\x0a \x0d", ".Whitespace"); }
        // From section 2.3.3 of the C# spec, these are also whitespace.
        [TestMethod]
        public void LineSeparator() { AssertTokenizeUnicode("\x2028", ".Whitespace"); }
        [TestMethod]
        public void ParagraphSeparator() { AssertTokenizeUnicode("\x2029", ".Whitespace"); }

        /*
            Special whitespace handling.
                Horizontal tab character (U+0009)
                Vertical tab character (U+000B)
                Form feed character (U+000C)  
        */
        [TestMethod]
        public void SpecialWhitespace() { AssertTokenize("\x09\x0b\x0c\x0d", ".Whitespace"); }

        // One-line comments (i.e. those starting with //)
        [TestMethod]
        public void OneLineComment() { AssertTokenize("// My one line comment.\x0d", ".Comment.Whitespace"); }
        [TestMethod]
        public void SpaceOneLineComment() { AssertTokenize(" // My one line comment.\x0d", ".Whitespace.Comment.Whitespace"); }
        [TestMethod]
        public void OneLineCommentTab() { AssertTokenize(" //\tMy one line comment.\x0d", ".Whitespace.Comment.Whitespace"); }
        [TestMethod]
        public void OneLineCommentCr() { AssertTokenize("// My one line comment.\x0d", ".Comment.Whitespace"); }
        [TestMethod]
        public void OneLineCommentLf() { AssertTokenize("// My one line comment.\x0a", ".Comment.Whitespace"); }
        [TestMethod]
        public void OneLineCommentLineSeparator() { AssertTokenizeUnicode("// My one line comment.\x2028", ".Comment.Whitespace"); }
        [TestMethod]
        public void OneLineCommentParagraphSeparator() { AssertTokenizeUnicode("// My one line comment.\x2029", ".Comment.Whitespace"); }
        [TestMethod]
        public void OneLineCommentWithEmbeddedMultiLine() { AssertTokenize("// /*  */\x0d", ".Comment.Whitespace"); }

        // Multi-line comments (i.e those like /* */)
        [TestMethod]
        public void OneLineMultilineComment() { AssertTokenize("/* My comment. */\x0d", ".Comment.Whitespace"); }
        [TestMethod]
        public void MultilineComment() { AssertTokenize("/* My comment. \x0d\x0a Second Line*/\x0d", ".Comment.Whitespace", 3); }
        [TestMethod]
        public void MultilineCommentWithEmbeddedSingleLine() { AssertTokenize("/* // */\x0d", ".Comment.Whitespace"); }
        [TestMethod]
        public void LeftHalfOfUnbalanceMultilineComment() { AssertTokenize("/*\x0d", ".EndOfFileInsideComment"); }
        [TestMethod]
        public void LeftHalfOfUnbalanceMultilineCommentWithStuff() { AssertTokenize("/* unbalanced\x0d", ".EndOfFileInsideComment"); }

        // If the last character of the source file is a Control-Z character (U+001A), this character is deleted. 
        [TestMethod]
        public void NothingPlustControlZatEOF() { AssertTokenize("\x1A", "", "", 0); }
        [TestMethod]
        public void SomethingPlusControlZatEOF() { AssertTokenize("// My comment\x1A", "// My comment\x0d", ".Comment.Whitespace"); }

        // A carriage-return character (U+000D) is added to the end of the source file if that source file is non-empty and if the last character 
        // of the source file is not a carriage return (U+000D), a line feed (U+000A), a line separator (U+2028), or a paragraph separator 
        // (U+2029). 
        [TestMethod]
        public void NoEOLatEOF() { AssertTokenize("// My comment", "// My comment\x0d", ".Comment.Whitespace"); }
        [TestMethod]
        public void NoEOLatEOFButFileIsEmpty() { AssertTokenize("", "", "", 0); }

        // An identifier that has a "_" embedded somewhere
        [TestMethod]
        public void IdentifierWithEmbeddedUnderscore() { AssertTokenize("_x_\xd", ".Identifier.Whitespace"); }

        // An identifier with a number
        [TestMethod]
        public void IdentifierWithNumber() { AssertTokenize("x3\xd", ".Identifier.Whitespace"); }

        // An non-identifier with a @ and a number
        [TestMethod]
        public void EscapedIdentifierWithNumber() { AssertTokenize("@3Identifier\xd", ".ExpectedIdentifier"); }

        // A very simple namespace and class.
        [TestMethod]
        public void NamespacePlusClass()
        {
            AssertTokenize
            ("namespace MyNamespace { class MyClass {} }\x0d",
             ".Keyword.Whitespace.Identifier.Whitespace.OpenScope.Whitespace.Keyword.Whitespace.Identifier.Whitespace.OpenScope.CloseScope.Whitespace.CloseScope.Whitespace");
        }

        // If a keyword has '@' in front, then its treated as an identifier. 
        [TestMethod]
        public void EscapedKeywordMakesIdentifier()
        {
            AssertTokenize
            (
                "namespace @namespace { class @class {} }\x0d",
                "namespace namespace { class class {} }\x0d",       // Resulting tokens have '@' stripped.
                ".Keyword.Whitespace.Identifier.Whitespace.OpenScope.Whitespace.Keyword.Whitespace.Identifier.Whitespace.OpenScope.CloseScope.Whitespace.CloseScope.Whitespace"
            );
        }

        // Check boolean literals
        [TestMethod]
        public void LiteralTrue() { AssertTokenize("true\x0d", ".BooleanLiteral.Whitespace"); }
        [TestMethod]
        public void LiteralFalse() { AssertTokenize("false\x0d", ".BooleanLiteral.Whitespace"); }
        [TestMethod]
        public void LiteralNull() { AssertTokenize("null\x0d", ".NullLiteral.Whitespace"); }

        // Check integer literals
        [TestMethod]
        public void HexIntegerLiteral() { AssertTokenize("0x123F\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexUppercaseXIntegerLiteral() { AssertTokenize("0X1f23\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void IntegerLiteral() { AssertTokenize("123\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void InvalidHexIntegerWithNoneValid() { AssertTokenize("0xG\x0d", ".ExpectedValidHexDigit"); }

        // Hex literal long suffix: U u L l UL Ul uL ul LU Lu lU lu 
        [TestMethod]
        public void HexIntegerLiteralUpperU() { AssertTokenize("0x123FU\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralLowerU() { AssertTokenize("0x123Fu\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralUpperL() { AssertTokenize("0x123FL\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralLowerL() { AssertTokenize("0x123Fl\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralUpperUUpperL() { AssertTokenize("0x123FUL\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralUpperULowerL() { AssertTokenize("0x123FUl\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralLowerUUpperL() { AssertTokenize("0x123FuL\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralUpperLUpperU() { AssertTokenize("0x123FLU\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralUpperLLowerU() { AssertTokenize("0x123FLu\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralLowerLUpperU() { AssertTokenize("0x123FlU\x0d", ".HexIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void HexIntegerLiteralLowerLLowerU() { AssertTokenize("0x123Flu\x0d", ".HexIntegerLiteral.Whitespace"); }

        // Decimal literal long suffix: U u L l UL Ul uL ul LU Lu lU lu 
        [TestMethod]
        public void DecimalIntegerLiteralUpperU() { AssertTokenize("1234U\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralLowerU() { AssertTokenize("1234u\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralUpperL() { AssertTokenize("1234L\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralLowerL() { AssertTokenize("1234l\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralUpperUUpperL() { AssertTokenize("1234UL\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralUpperULowerL() { AssertTokenize("1234Ul\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralLowerUUpperL() { AssertTokenize("1234uL\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralUpperLUpperU() { AssertTokenize("1234LU\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralUpperLLowerU() { AssertTokenize("1234Lu\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralLowerLUpperU() { AssertTokenize("1234lU\x0d", ".DecimalIntegerLiteral.Whitespace"); }
        [TestMethod]
        public void DecimalIntegerLiteralLowerLLowerU() { AssertTokenize("1234lu\x0d", ".DecimalIntegerLiteral.Whitespace"); }

        // Reals aren't supported yet.
        // Reals can take many different forms: 1.1, .1, 1.1e6, etc.
        // If you turn this on, please create test for the other forms too.
        [TestMethod]
        [Ignore] // "Reals aren't supported yet."
        public void RealLiteral1() { AssertTokenize("1.1\x0d", ".RealLiteral.Whitespace"); }

        // Char literals aren't supported yet.
        [TestMethod]
        public void CharLiteral1() { AssertTokenize("'c'\x0d", ".CharLiteral.Whitespace"); }

        [TestMethod]
        [Ignore] // "Escape sequences aren't supported"
        public void CharLiteralIllegalEscapeSequence() { AssertTokenize("'\\z'\x0d", ".SyntaxErrorIllegalEscapeSequence"); }

        [TestMethod]
        [Ignore] // "Escape sequences aren't supported"
        public void CharLiteralHexEscapeSequence() { AssertTokenize("'\\x0022a'\x0d", "'\"a'\x0d", ".CharLiteral.Whitespace"); }

        // Check string literals
        [TestMethod]
        public void LiteralStringBasic() { AssertTokenize("\"string\"\x0d", ".StringLiteral.Whitespace"); }
        [TestMethod]
        public void LiteralStringAllEscapes() { AssertTokenize("\"\\'\\\"\\\\\\0\\a\\b\\f\\n\\r\\t\\x0\\v\"\x0d", ".StringLiteral.Whitespace"); }
        [TestMethod]
        public void LiteralStringUnclosed() { AssertTokenize("\"string\x0d", ".NewlineInsideString"); }
        [TestMethod]
        public void LiteralVerbatimStringBasic() { AssertTokenize("@\"string\"\x0d", "\"string\"\x0d", ".StringLiteral.Whitespace"); }
        [TestMethod]
        public void LiteralVerbatimStringAllEscapes() { AssertTokenize("@\"\\a\\b\\c\"\x0d", "\"\\a\\b\\c\"\x0d", ".StringLiteral.Whitespace"); }
        [TestMethod]
        public void LiteralVerbatimStringUnclosed() { AssertTokenize("@\"string\x0d", ".EndOfFileInsideString"); }
        [TestMethod]
        public void LiteralVerbatimStringQuoteEscapeSequence() { AssertTokenize("@\"\"\"\"\x0d", "\"\"\"\"\x0d", ".StringLiteral.Whitespace"); }

        // Single-digit operators and punctuators.
        [TestMethod]
        public void PunctuatorOpenBracket() { AssertTokenize("[\x0d", ".OperatorOrPunctuator.Whitespace"); }
        [TestMethod]
        public void PunctuatorCloseBracket() { AssertTokenize("]\x0d", ".OperatorOrPunctuator.Whitespace"); }
        [TestMethod]
        public void PunctuatorOpenParen() { AssertTokenize("(\x0d", ".OperatorOrPunctuator.Whitespace"); }
        [TestMethod]
        public void PunctuatorCloseParen() { AssertTokenize(")\x0d", ".OperatorOrPunctuator.Whitespace"); }
        [TestMethod]
        public void PunctuatorDot() { AssertTokenize(".\x0d", ".OperatorOrPunctuator.Whitespace"); }
        [TestMethod]
        public void PunctuatorColon() { AssertTokenize(":\x0d", ".OperatorOrPunctuator.Whitespace"); }
        [TestMethod]
        public void PunctuatorSemicolon() { AssertTokenize(";\x0d", ".OperatorOrPunctuator.Whitespace"); }

        // Preprocessor.
        [TestMethod]
        public void Preprocessor() { AssertTokenize("#if\x0d", ".OpenConditionalDirective.Whitespace"); }


        /*
        * Method:  AssertTokenize
        * 
        * Tokenize a string ('source') and compare it to the expected set of tokens.
        * Also, the source must be regenerated exactly when the tokens are concatenated 
        * back together,
        */
        static private void AssertTokenize(string source, string expectedTokenKey)
        {
            // Most of the time, we expect the rebuilt source to be the same as the input source.
            AssertTokenize(source, source, expectedTokenKey);
        }

        /*
        * Method:  AssertTokenizeUnicode
        * 
        * Tokenize a string ('source') and compare it to the expected set of tokens.
        * Also, the source must be regenerated exactly when the tokens are concatenated 
        * back together,
        */
        static private void AssertTokenizeUnicode(string source, string expectedTokenKey)
        {
            // Most of the time, we expect the rebuilt source to be the same as the input source.
            AssertTokenizeUnicode(source, source, expectedTokenKey);
        }

        /*
        * Method:  AssertTokenize
        * 
        * Tokenize a string ('source') and compare it to the expected set of tokens.
        * Also, the source must be regenerated exactly when the tokens are concatenated 
        * back together,
        */
        static private void AssertTokenize
        (
           string source,
           string expectedTokenKey,
           int expectedLastLineNumber
        )
        {
            // Most of the time, we expect the rebuilt source to be the same as the input source.
            AssertTokenize(source, source, expectedTokenKey, expectedLastLineNumber);
        }

        /*
        * Method:  AssertTokenizeUnicode
        * 
        * Tokenize a string ('source') and compare it to the expected set of tokens.
        * Also, the source must be regenerated exactly when the tokens are concatenated 
        * back together,
        */
        static private void AssertTokenizeUnicode
        (
           string source,
           string expectedTokenKey,
           int expectedLastLineNumber
        )
        {
            // Most of the time, we expect the rebuilt source to be the same as the input source.
            AssertTokenizeUnicode(source, source, expectedTokenKey, expectedLastLineNumber);
        }

        /*
        * Method:  AssertTokenize
        * 
        * Tokenize a string ('source') and compare it to the expected set of tokens.
        * Also compare the source that is regenerated by concatenating all of the tokens
        * to 'expectedSource'.
        */
        static private void AssertTokenize
        (
           string source,
           string expectedSource,
           string expectedTokenKey
        )
        {
            // Two lines is the most common test case.
            AssertTokenize(source, expectedSource, expectedTokenKey, 1);
        }

        /*
        * Method:  AssertTokenizeUnicode
        * 
        * Tokenize a string ('source') and compare it to the expected set of tokens.
        * Also compare the source that is regenerated by concatenating all of the tokens
        * to 'expectedSource'.
        */
        static private void AssertTokenizeUnicode
        (
           string source,
           string expectedSource,
           string expectedTokenKey
        )
        {
            // Two lines is the most common test case.
            AssertTokenizeUnicode(source, expectedSource, expectedTokenKey, 1);
        }

        /*
        * Method:  AssertTokenizeUnicode
        * 
        * Tokenize a string ('source') and compare it to the expected set of tokens.
        * Also compare the source that is regenerated by concatenating all of the tokens
        * to 'expectedSource'.
        */
        static private void AssertTokenizeUnicode
        (
           string source,
           string expectedSource,
           string expectedTokenKey,
           int expectedLastLineNumber
        )
        {
            AssertTokenizeStream
            (
                StreamHelpers.StringToStream(source, System.Text.Encoding.Unicode),
                expectedSource,
                expectedTokenKey,
                expectedLastLineNumber
            );
        }

        /*
        * Method:  AssertTokenize
        * 
        * Tokenize a string ('source') and compare it to the expected set of tokens.
        * Also compare the source that is regenerated by concatenating all of the tokens
        * to 'expectedSource'.
        */
        static private void AssertTokenize
        (
           string source,
           string expectedSource,
           string expectedTokenKey,
           int expectedLastLineNumber
        )
        {
            // This version of AssertTokenize tests several different encodings.
            // The reason is that we want to be sure each of these works in the 
            // various encoding formats supported by C#
            AssertTokenizeStream(StreamHelpers.StringToStream(source), expectedSource, expectedTokenKey, expectedLastLineNumber);
            AssertTokenizeStream(StreamHelpers.StringToStream(source, System.Text.Encoding.Unicode), expectedSource, expectedTokenKey, expectedLastLineNumber);
            AssertTokenizeStream(StreamHelpers.StringToStream(source, System.Text.Encoding.UTF8), expectedSource, expectedTokenKey, expectedLastLineNumber);
            AssertTokenizeStream(StreamHelpers.StringToStream(source, System.Text.Encoding.BigEndianUnicode), expectedSource, expectedTokenKey, expectedLastLineNumber);
            AssertTokenizeStream(StreamHelpers.StringToStream(source, System.Text.Encoding.UTF32), expectedSource, expectedTokenKey, expectedLastLineNumber);
            AssertTokenizeStream(StreamHelpers.StringToStream(source, System.Text.Encoding.ASCII), expectedSource, expectedTokenKey, expectedLastLineNumber);
        }

        /*
         * Method:  AssertTokenizeStream
         * 
         * Tokenize a string ('source') and compare it to the expected set of tokens.
         * Also compare the source that is regenerated by concatenating all of the tokens
         * to 'expectedSource'.
         */
        static private void AssertTokenizeStream
        (
           Stream source,
           string expectedSource,
           string expectedTokenKey,
           int expectedLastLineNumber
        )
        {
            CSharpTokenizer tokens = new CSharpTokenizer
            (
                source,
                false
            );
            string results = "";
            string tokenKey = "";
            int lastLine = 0;
            bool syntaxError = false;
            foreach (Token t in tokens)
            {
                results += t.InnerText;
                lastLine = t.Line;

                if (!syntaxError)
                {
                    // Its not really a file name, but GetExtension serves the purpose of getting the class name without
                    // the namespace prepended.
                    string tokenClass = t.ToString();
                    int pos = tokenClass.LastIndexOfAny(new char[] { '+', '.' });

                    tokenKey += ".";
                    tokenKey += tokenClass.Substring(pos + 1);
                }

                if (t is SyntaxErrorToken)
                {
                    // Stop processing after the first syntax error because
                    // the order of tokens after this is an implementation detail and
                    // shouldn't be encoded into the unit tests.
                    syntaxError = true;
                }
            }
            tokenKey = tokenKey.Replace("Token", "");
            Console.WriteLine(tokenKey);

            Assert.AreEqual(expectedSource, results);
            Assert.AreEqual(expectedTokenKey, tokenKey);
            Assert.AreEqual(expectedLastLineNumber, lastLine);
        }
    }
}



