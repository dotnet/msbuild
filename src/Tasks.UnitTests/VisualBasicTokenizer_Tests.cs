// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Shared.LanguageParser;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class VisualBasicTokenizer_Tests
    {
        [Fact]
        public void Empty() { AssertTokenize("", "", "", 0); }
        [Fact]
        public void OneSpace() { AssertTokenize(" ", " \x0d", ".eol", 1); }
        [Fact]
        public void TwoSpace() { AssertTokenize("  ", "  \x0d", ".eol", 1); }
        [Fact]
        public void Tab() { AssertTokenize("\t", "\t\x0d", ".eol", 1); }
        [Fact]
        public void TwoTab() { AssertTokenize("\t\t", "\t\t\x0d", ".eol", 1); }
        [Fact]
        public void SpaceTab() { AssertTokenize(" \t", " \t\x0d", ".eol", 1); }

        // Test line continuation character
        [Fact]
        public void SimpleLineContinuation() { AssertTokenize(" _\xd\xa", "."); }
        [Fact]
        public void LineContinuationWithspacesAfter() { AssertTokenize(" _ \xd\xa\xd\xa", "."); }

        // Comments
        [Fact]
        public void SimpleComment() { AssertTokenize("' This is a comment\xd", "Comment(' This is a comment)eol"); }
        [Fact]
        public void RemComment() { AssertTokenize("rEm This is a comment\xd", "Comment(rEm This is a comment)eol"); }

        // Identifiers
        [Fact]
        public void SimpleIdentifier() { AssertTokenize("_MyIdentifier3\xd", "Identifier(_MyIdentifier3)eol"); }
        [Fact]
        public void IdentifierWithEmbeddedUnderscore() { AssertTokenize("_M_\xd", "Identifier(_M_)eol"); }
        [Fact]
        public void IdentifierWithStringTypeCharacter() { AssertTokenize("MyString$\xd", "Identifier(MyString$)eol"); }
        [Fact]
        public void IdentifierWithLongTypeCharacter() { AssertTokenize("MyString&\xd", "Identifier(MyString&)eol"); }
        [Fact]
        public void IdentifierWithDecimalTypeCharacter() { AssertTokenize("MyString@\xd", "Identifier(MyString@)eol"); }
        [Fact]
        public void IdentifierWithSingleTypeCharacter() { AssertTokenize("MyString!\xd", "Identifier(MyString!)eol"); }
        [Fact]
        public void IdentifierWithDoubleTypeCharacter() { AssertTokenize("MyString#\xd", "Identifier(MyString#)eol"); }
        [Fact]
        public void IdentifierWithIntegerTypeCharacter() { AssertTokenize("MyString%\xd", "Identifier(MyString%)eol"); }
        [Fact]
        public void EscapedIdentifier() { AssertTokenize("[Namespace]\xd", "Namespace\xd", "Identifier(Namespace)eol", 1); }
        [Fact]
        public void UnfinishedEscapedIdentifier() { AssertTokenize("[Namespace\xd", "ExpectedIdentifier([Namespace)"); }
        [Fact]
        public void EscapedIdentifierWithoutGoodStart() { AssertTokenize("[3]\xd", "ExpectedIdentifier([)"); }
        [Fact]
        public void EscapedLineContinuation() { AssertTokenize("[_]\xd", "ExpectedIdentifier([_])"); }
        [Fact]
        public void EscapedButEmptyIdentifier() { AssertTokenize("[]\xd", "ExpectedIdentifier([)"); }
        [Fact]
        public void EscapedIdentifierHasType() { AssertTokenize("[MyString$]\xd", "ExpectedIdentifier([MyString)"); }
        [Fact]
        public void EscapedIdentifierHasTypeOnTheOutside() { AssertTokenize("[MyString]$\xd", "MyString$\xd", "Identifier(MyString)Unrecognized($)", 1); }

        // A lone underscore is an invalid identifier.
        [Fact]
        public void LoneUnderscore()
        {
            AssertTokenize
            (
                "Sub Foo(ByVal _ As Int16)\xd",
                "Keyword(Sub).Identifier(Foo)Separator(()Keyword(ByVal).ExpectedIdentifier(_)"
            );
        }

        // Boolean literals
        [Fact]
        public void BooleanTrue() { AssertTokenize("tRuE\xd", "BooleanLiteral(tRuE)eol"); }
        [Fact]
        public void BooleanFalse() { AssertTokenize("falsE\xd", "BooleanLiteral(falsE)eol"); }

        // Integer literals
        [Fact]
        public void HexInteger() { AssertTokenize("&H0123456789aBcDeF\xd", "HexIntegerLiteral(&H0123456789aBcDeF)eol"); }
        [Fact]
        public void Octalnteger() { AssertTokenize("&O01234567\xd", "OctalIntegerLiteral(&O01234567)eol"); }
        [Fact]
        public void HexIntegerLowerCase() { AssertTokenize("&h001\xd", "HexIntegerLiteral(&h001)eol"); }
        [Fact]
        public void OctalntegerUpperCase() { AssertTokenize("&o001\xd", "OctalIntegerLiteral(&o001)eol"); }
        [Fact]
        public void Decimallnteger() { AssertTokenize("001\xd", "DecimalIntegerLiteral(001)eol"); }
        [Fact]
        public void InvalidHexInteger() { AssertTokenize("&H00FG\xd", "HexIntegerLiteral(&H00F)Identifier(G)eol"); }
        [Fact]
        public void InvalidOctalnteger() { AssertTokenize("&O0089\xd", "OctalIntegerLiteral(&O00)DecimalIntegerLiteral(89)eol"); }
        [Fact]
        public void InvalidHexIntegerWithNoneValid() { AssertTokenize("&HG\xd", "ExpectedValidHexDigit(&H)"); }
        [Fact]
        public void InvalidOctalntegerWithNoneValid() { AssertTokenize("&O9\xd", "ExpectedValidOctalDigit(&O)"); }
        [Fact]
        public void HexIntegerShort() { AssertTokenize("&HaBcDeFS\xd", "HexIntegerLiteral(&HaBcDeFS)eol"); }
        [Fact]
        public void HexIntegerShortLower() { AssertTokenize("&HaBcDeFs\xd", "HexIntegerLiteral(&HaBcDeFs)eol"); }
        [Fact]
        public void DecimalIntegerShort() { AssertTokenize("123S\xd", "DecimalIntegerLiteral(123S)eol"); }
        [Fact]
        public void DecimalIntegerShortLower() { AssertTokenize("123s\xd", "DecimalIntegerLiteral(123s)eol"); }
        [Fact]
        public void OctalntegerShort() { AssertTokenize("&O01234567S\xd", "OctalIntegerLiteral(&O01234567S)eol"); }
        [Fact]
        public void OctalntegerShortLower() { AssertTokenize("&O01234567s\xd", "OctalIntegerLiteral(&O01234567s)eol"); }
        [Fact]
        public void HexIntegerInteger() { AssertTokenize("&HaBcDeFI\xd", "HexIntegerLiteral(&HaBcDeFI)eol"); }
        [Fact]
        public void HexIntegerIntegerLower() { AssertTokenize("&HaBcDeFi\xd", "HexIntegerLiteral(&HaBcDeFi)eol"); }
        [Fact]
        public void OctalntegerInteger() { AssertTokenize("&O01234567I\xd", "OctalIntegerLiteral(&O01234567I)eol"); }
        [Fact]
        public void OctalntegerIntegerLower() { AssertTokenize("&O01234567i\xd", "OctalIntegerLiteral(&O01234567i)eol"); }
        [Fact]
        public void DecimalIntegerInteger() { AssertTokenize("123I\xd", "DecimalIntegerLiteral(123I)eol"); }
        [Fact]
        public void DecimalIntegerIntegerLower() { AssertTokenize("123i\xd", "DecimalIntegerLiteral(123i)eol"); }
        [Fact]
        public void HexIntegerLong() { AssertTokenize("&HaBcDeFL\xd", "HexIntegerLiteral(&HaBcDeFL)eol"); }
        [Fact]
        public void HexIntegerLongLower() { AssertTokenize("&HaBcDeFl\xd", "HexIntegerLiteral(&HaBcDeFl)eol"); }
        [Fact]
        public void OctalntegerLong() { AssertTokenize("&O01234567L\xd", "OctalIntegerLiteral(&O01234567L)eol"); }
        [Fact]
        public void OctalntegerLongLower() { AssertTokenize("&O01234567l\xd", "OctalIntegerLiteral(&O01234567l)eol"); }
        [Fact]
        public void DecimalIntegerLong() { AssertTokenize("123L\xd", "DecimalIntegerLiteral(123L)eol"); }
        [Fact]
        public void DecimalIntegerIntegerLong() { AssertTokenize("123l\xd", "DecimalIntegerLiteral(123l)eol"); }
        [Fact]
        public void DecimalIntegerWithIntegerTypeChar() { AssertTokenize("1234%\xd", "DecimalIntegerLiteral(1234%)eol"); }
        [Fact]
        public void DecimalIntegerWithLongTypeChar() { AssertTokenize("1234&\xd", "DecimalIntegerLiteral(1234&)eol"); }
        [Fact]
        public void DecimalIntegerWithDecimalTypeChar() { AssertTokenize("1234@\xd", "DecimalIntegerLiteral(1234@)eol"); }
        [Fact]
        public void DecimalIntegerWithSingleTypeChar() { AssertTokenize("1234!\xd", "DecimalIntegerLiteral(1234!)eol"); }
        [Fact]
        public void DecimalIntegerWithDoubleTypeChar() { AssertTokenize("1234#\xd", "DecimalIntegerLiteral(1234#)eol"); }
        [Fact]
        public void DecimalIntegerWithStringTypeChar() { AssertTokenize("1234$\xd", "DecimalIntegerLiteral(1234)Unrecognized($)"); }

        // String literal
        [Fact]
        public void BasicString() { AssertTokenize("\"A string\"\xd", "StringLiteral(\"A string\")eol"); }
        [Fact]
        public void StringWithDoubledQuotesAsEscape() { AssertTokenize("\"\"\"\"\x0d", "\"\"\"\"\x0d", "StringLiteral(\"\"\"\")eol", 1); }
        [Fact]
        public void StringUnclosed() { AssertTokenize("\"string\x0d", "EndOfFileInsideString(\"string\x0d)"); }

        // Operators
        [Fact]
        public void CheckAllOperators()
        {
            AssertTokenize
            (
                "a=1 & 2*3+4-5/6\\7^8<9=10>11\xd",
                @"Identifier(a)Operator(=)DecimalIntegerLiteral(1).Operator(&).DecimalIntegerLiteral(2)Operator(*)DecimalIntegerLiteral(3)Operator(+)DecimalIntegerLiteral(4)Operator(-)DecimalIntegerLiteral(5)Operator(/)DecimalIntegerLiteral(6)Operator(\)DecimalIntegerLiteral(7)Operator(^)DecimalIntegerLiteral(8)Operator(<)DecimalIntegerLiteral(9)Operator(=)DecimalIntegerLiteral(10)Operator(>)DecimalIntegerLiteral(11)eol"
            );
        }

        // Inplace arrays
        [Fact]
        public void InplaceArray()
        {
            AssertTokenize
            (
                "Me.Controls.AddRange(New Control() {Me.lblCodebase, Me.lblCopyright})\xd",
                "Keyword(Me)Separator(.)Identifier(Controls)Separator(.)Identifier(AddRange)Separator(()Keyword(New).Identifier(Control)Separator(()Separator()).Separator({)Keyword(Me)Separator(.)Identifier(lblCodebase)Separator(,).Keyword(Me)Separator(.)Identifier(lblCopyright)Separator(})Separator())eol"
            );
        }

        // Keywords
        [Fact]
        public void SimpleKeyword() { AssertTokenize("Namespace\xd", "Keyword(Namespace)eol"); }

        // From the real world
        [Fact]
        public void WackyBrackettedClassName()
        {
            AssertTokenize
            (
                "Public Class [!output SAFE_ITEM_NAME]\xd",
                "Keyword(Public).Keyword(Class).ExpectedIdentifier([)"
            );
        }
        [Fact]
        public void MyClassIsAKeyword()
        {
            AssertTokenize
            (
                "Class MyClass\xd",
                "Keyword(Class).Keyword(MyClass)eol"
            );
        }


        [Fact]
        public void Regress_Mutation_x0dx0aIsASingleLine()
        {
            AssertTokenize("\x0d\x0a", "\x0d\x0a", "eol", 1);
        }

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
            AssertTokenize(source, source, expectedTokenKey, 1);
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
            VisualBasicTokenizer tokens = new VisualBasicTokenizer
            (
                StreamHelpers.StringToStream(source),
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

                    if (t is VisualBasicTokenizer.LineTerminatorToken)
                    {
                        tokenKey += "eol";
                    }
                    else if (t is WhitespaceToken)
                    {
                        tokenKey += ".";
                    }
                    else
                    {
                        tokenKey += tokenClass.Substring(pos + 1);
                        tokenKey += "(";
                        tokenKey += t.InnerText;
                        tokenKey += ")";
                    }
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

            if (expectedSource != results || expectedTokenKey != tokenKey)
            {
                Console.WriteLine(tokenKey);
            }

            Assert.Equal(expectedSource, results);
            Assert.Equal(expectedTokenKey, tokenKey);
            Assert.Equal(expectedLastLineNumber, lastLine);
        }
    }
}



