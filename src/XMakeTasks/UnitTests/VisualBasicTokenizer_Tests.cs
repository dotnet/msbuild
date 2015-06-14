// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using System.Text.RegularExpressions;

using Microsoft.Build.Shared.LanguageParser;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    sealed public class VisualBasicTokenizer_Tests
    {
        [Test]
        public void Empty() { AssertTokenize("", "", "", 0); }
        [Test]
        public void OneSpace() { AssertTokenize(" ", " \x0d", ".eol", 1); }
        [Test]
        public void TwoSpace() { AssertTokenize("  ", "  \x0d", ".eol", 1); }
        [Test]
        public void Tab() { AssertTokenize("\t", "\t\x0d", ".eol", 1); }
        [Test]
        public void TwoTab() { AssertTokenize("\t\t", "\t\t\x0d", ".eol", 1); }
        [Test]
        public void SpaceTab() { AssertTokenize(" \t", " \t\x0d", ".eol", 1); }

        // Test line continuation character
        [Test]
        public void SimpleLineContinuation() { AssertTokenize(" _\xd\xa", "."); }
        [Test]
        public void LineContinuationWithspacesAfter() { AssertTokenize(" _ \xd\xa\xd\xa", "."); }

        // Comments
        [Test]
        public void SimpleComment() { AssertTokenize("' This is a comment\xd", "Comment(' This is a comment)eol"); }
        [Test]
        public void RemComment() { AssertTokenize("rEm This is a comment\xd", "Comment(rEm This is a comment)eol"); }

        // Identifiers
        [Test]
        public void SimpleIdentifier() { AssertTokenize("_MyIdentifier3\xd", "Identifier(_MyIdentifier3)eol"); }
        [Test]
        public void IdentifierWithEmbeddedUnderscore() { AssertTokenize("_M_\xd", "Identifier(_M_)eol"); }
        [Test]
        public void IdentifierWithStringTypeCharacter() { AssertTokenize("MyString$\xd", "Identifier(MyString$)eol"); }
        [Test]
        public void IdentifierWithLongTypeCharacter() { AssertTokenize("MyString&\xd", "Identifier(MyString&)eol"); }
        [Test]
        public void IdentifierWithDecimalTypeCharacter() { AssertTokenize("MyString@\xd", "Identifier(MyString@)eol"); }
        [Test]
        public void IdentifierWithSingleTypeCharacter() { AssertTokenize("MyString!\xd", "Identifier(MyString!)eol"); }
        [Test]
        public void IdentifierWithDoubleTypeCharacter() { AssertTokenize("MyString#\xd", "Identifier(MyString#)eol"); }
        [Test]
        public void IdentifierWithIntegerTypeCharacter() { AssertTokenize("MyString%\xd", "Identifier(MyString%)eol"); }
        [Test]
        public void EscapedIdentifier() { AssertTokenize("[Namespace]\xd", "Namespace\xd", "Identifier(Namespace)eol", 1); }
        [Test]
        public void UnfinishedEscapedIdentifier() { AssertTokenize("[Namespace\xd", "ExpectedIdentifier([Namespace)"); }
        [Test]
        public void EscapedIdentifierWithoutGoodStart() { AssertTokenize("[3]\xd", "ExpectedIdentifier([)"); }
        [Test]
        public void EscapedLineContinuation() { AssertTokenize("[_]\xd", "ExpectedIdentifier([_])"); }
        [Test]
        public void EscapedButEmptyIdentifier() { AssertTokenize("[]\xd", "ExpectedIdentifier([)"); }
        [Test]
        public void EscapedIdentifierHasType() { AssertTokenize("[MyString$]\xd", "ExpectedIdentifier([MyString)"); }
        [Test]
        public void EscapedIdentifierHasTypeOnTheOutside() { AssertTokenize("[MyString]$\xd", "MyString$\xd", "Identifier(MyString)Unrecognized($)", 1); }

        // A lone underscore is an invalid identifier.
        [Test]
        public void LoneUnderscore()
        {
            AssertTokenize
            (
                "Sub Foo(ByVal _ As Int16)\xd",
                "Keyword(Sub).Identifier(Foo)Separator(()Keyword(ByVal).ExpectedIdentifier(_)"
            );
        }

        // Boolean literals
        [Test]
        public void BooleanTrue() { AssertTokenize("tRuE\xd", "BooleanLiteral(tRuE)eol"); }
        [Test]
        public void BooleanFalse() { AssertTokenize("falsE\xd", "BooleanLiteral(falsE)eol"); }

        // Integer literals
        [Test]
        public void HexInteger() { AssertTokenize("&H0123456789aBcDeF\xd", "HexIntegerLiteral(&H0123456789aBcDeF)eol"); }
        [Test]
        public void Octalnteger() { AssertTokenize("&O01234567\xd", "OctalIntegerLiteral(&O01234567)eol"); }
        [Test]
        public void HexIntegerLowerCase() { AssertTokenize("&h001\xd", "HexIntegerLiteral(&h001)eol"); }
        [Test]
        public void OctalntegerUpperCase() { AssertTokenize("&o001\xd", "OctalIntegerLiteral(&o001)eol"); }
        [Test]
        public void Decimallnteger() { AssertTokenize("001\xd", "DecimalIntegerLiteral(001)eol"); }
        [Test]
        public void InvalidHexInteger() { AssertTokenize("&H00FG\xd", "HexIntegerLiteral(&H00F)Identifier(G)eol"); }
        [Test]
        public void InvalidOctalnteger() { AssertTokenize("&O0089\xd", "OctalIntegerLiteral(&O00)DecimalIntegerLiteral(89)eol"); }
        [Test]
        public void InvalidHexIntegerWithNoneValid() { AssertTokenize("&HG\xd", "ExpectedValidHexDigit(&H)"); }
        [Test]
        public void InvalidOctalntegerWithNoneValid() { AssertTokenize("&O9\xd", "ExpectedValidOctalDigit(&O)"); }
        [Test]
        public void HexIntegerShort() { AssertTokenize("&HaBcDeFS\xd", "HexIntegerLiteral(&HaBcDeFS)eol"); }
        [Test]
        public void HexIntegerShortLower() { AssertTokenize("&HaBcDeFs\xd", "HexIntegerLiteral(&HaBcDeFs)eol"); }
        [Test]
        public void DecimalIntegerShort() { AssertTokenize("123S\xd", "DecimalIntegerLiteral(123S)eol"); }
        [Test]
        public void DecimalIntegerShortLower() { AssertTokenize("123s\xd", "DecimalIntegerLiteral(123s)eol"); }
        [Test]
        public void OctalntegerShort() { AssertTokenize("&O01234567S\xd", "OctalIntegerLiteral(&O01234567S)eol"); }
        [Test]
        public void OctalntegerShortLower() { AssertTokenize("&O01234567s\xd", "OctalIntegerLiteral(&O01234567s)eol"); }
        [Test]
        public void HexIntegerInteger() { AssertTokenize("&HaBcDeFI\xd", "HexIntegerLiteral(&HaBcDeFI)eol"); }
        [Test]
        public void HexIntegerIntegerLower() { AssertTokenize("&HaBcDeFi\xd", "HexIntegerLiteral(&HaBcDeFi)eol"); }
        [Test]
        public void OctalntegerInteger() { AssertTokenize("&O01234567I\xd", "OctalIntegerLiteral(&O01234567I)eol"); }
        [Test]
        public void OctalntegerIntegerLower() { AssertTokenize("&O01234567i\xd", "OctalIntegerLiteral(&O01234567i)eol"); }
        [Test]
        public void DecimalIntegerInteger() { AssertTokenize("123I\xd", "DecimalIntegerLiteral(123I)eol"); }
        [Test]
        public void DecimalIntegerIntegerLower() { AssertTokenize("123i\xd", "DecimalIntegerLiteral(123i)eol"); }
        [Test]
        public void HexIntegerLong() { AssertTokenize("&HaBcDeFL\xd", "HexIntegerLiteral(&HaBcDeFL)eol"); }
        [Test]
        public void HexIntegerLongLower() { AssertTokenize("&HaBcDeFl\xd", "HexIntegerLiteral(&HaBcDeFl)eol"); }
        [Test]
        public void OctalntegerLong() { AssertTokenize("&O01234567L\xd", "OctalIntegerLiteral(&O01234567L)eol"); }
        [Test]
        public void OctalntegerLongLower() { AssertTokenize("&O01234567l\xd", "OctalIntegerLiteral(&O01234567l)eol"); }
        [Test]
        public void DecimalIntegerLong() { AssertTokenize("123L\xd", "DecimalIntegerLiteral(123L)eol"); }
        [Test]
        public void DecimalIntegerIntegerLong() { AssertTokenize("123l\xd", "DecimalIntegerLiteral(123l)eol"); }
        [Test]
        public void DecimalIntegerWithIntegerTypeChar() { AssertTokenize("1234%\xd", "DecimalIntegerLiteral(1234%)eol"); }
        [Test]
        public void DecimalIntegerWithLongTypeChar() { AssertTokenize("1234&\xd", "DecimalIntegerLiteral(1234&)eol"); }
        [Test]
        public void DecimalIntegerWithDecimalTypeChar() { AssertTokenize("1234@\xd", "DecimalIntegerLiteral(1234@)eol"); }
        [Test]
        public void DecimalIntegerWithSingleTypeChar() { AssertTokenize("1234!\xd", "DecimalIntegerLiteral(1234!)eol"); }
        [Test]
        public void DecimalIntegerWithDoubleTypeChar() { AssertTokenize("1234#\xd", "DecimalIntegerLiteral(1234#)eol"); }
        [Test]
        public void DecimalIntegerWithStringTypeChar() { AssertTokenize("1234$\xd", "DecimalIntegerLiteral(1234)Unrecognized($)"); }

        // String literal
        [Test]
        public void BasicString() { AssertTokenize("\"A string\"\xd", "StringLiteral(\"A string\")eol"); }
        [Test]
        public void StringWithDoubledQuotesAsEscape() { AssertTokenize("\"\"\"\"\x0d", "\"\"\"\"\x0d", "StringLiteral(\"\"\"\")eol", 1); }
        [Test]
        public void StringUnclosed() { AssertTokenize("\"string\x0d", "EndOfFileInsideString(\"string\x0d)"); }

        // Operators
        [Test]
        public void CheckAllOperators()
        {
            AssertTokenize
            (
                "a=1 & 2*3+4-5/6\\7^8<9=10>11\xd",
                @"Identifier(a)Operator(=)DecimalIntegerLiteral(1).Operator(&).DecimalIntegerLiteral(2)Operator(*)DecimalIntegerLiteral(3)Operator(+)DecimalIntegerLiteral(4)Operator(-)DecimalIntegerLiteral(5)Operator(/)DecimalIntegerLiteral(6)Operator(\)DecimalIntegerLiteral(7)Operator(^)DecimalIntegerLiteral(8)Operator(<)DecimalIntegerLiteral(9)Operator(=)DecimalIntegerLiteral(10)Operator(>)DecimalIntegerLiteral(11)eol"
            );
        }

        // Inplace arrays
        [Test]
        public void InplaceArray()
        {
            AssertTokenize
            (
                "Me.Controls.AddRange(New Control() {Me.lblCodebase, Me.lblCopyright})\xd",
                "Keyword(Me)Separator(.)Identifier(Controls)Separator(.)Identifier(AddRange)Separator(()Keyword(New).Identifier(Control)Separator(()Separator()).Separator({)Keyword(Me)Separator(.)Identifier(lblCodebase)Separator(,).Keyword(Me)Separator(.)Identifier(lblCopyright)Separator(})Separator())eol"
            );
        }

        // Keywords
        [Test]
        public void SimpleKeyword() { AssertTokenize("Namespace\xd", "Keyword(Namespace)eol"); }

        // From the real world
        [Test]
        public void WackyBrackettedClassName()
        {
            AssertTokenize
            (
                "Public Class [!output SAFE_ITEM_NAME]\xd",
                "Keyword(Public).Keyword(Class).ExpectedIdentifier([)"
            );
        }
        [Test]
        public void MyClassIsAKeyword()
        {
            AssertTokenize
            (
                "Class MyClass\xd",
                "Keyword(Class).Keyword(MyClass)eol"
            );
        }


        [Test]
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

            Assert.AreEqual(expectedSource, results);
            Assert.AreEqual(expectedTokenKey, tokenKey);
            Assert.AreEqual(expectedLastLineNumber, lastLine);
        }
    }
}



