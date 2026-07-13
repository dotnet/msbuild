// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

using Microsoft.Build.Shared.LanguageParser;

#nullable disable

namespace Microsoft.Build.UnitTests
{
    [TestClass]
    public sealed class VisualBasicTokenizer_Tests
    {
        [MSBuildTestMethod]
        public void Empty() { AssertTokenize("", "", "", 0); }
        [MSBuildTestMethod]
        public void OneSpace() { AssertTokenize(" ", " \x0d", ".eol", 1); }
        [MSBuildTestMethod]
        public void TwoSpace() { AssertTokenize("  ", "  \x0d", ".eol", 1); }
        [MSBuildTestMethod]
        public void Tab() { AssertTokenize("\t", "\t\x0d", ".eol", 1); }
        [MSBuildTestMethod]
        public void TwoTab() { AssertTokenize("\t\t", "\t\t\x0d", ".eol", 1); }
        [MSBuildTestMethod]
        public void SpaceTab() { AssertTokenize(" \t", " \t\x0d", ".eol", 1); }

        // Test line continuation character
        [MSBuildTestMethod]
        public void SimpleLineContinuation() { AssertTokenize(" _\xd\xa", "."); }
        [MSBuildTestMethod]
        public void LineContinuationWithspacesAfter() { AssertTokenize(" _ \xd\xa\xd\xa", "."); }

        // Comments
        [MSBuildTestMethod]
        public void SimpleComment() { AssertTokenize("' This is a comment\xd", "Comment(' This is a comment)eol"); }
        [MSBuildTestMethod]
        public void RemComment() { AssertTokenize("rEm This is a comment\xd", "Comment(rEm This is a comment)eol"); }

        // Identifiers
        [MSBuildTestMethod]
        public void SimpleIdentifier() { AssertTokenize("_MyIdentifier3\xd", "Identifier(_MyIdentifier3)eol"); }
        [MSBuildTestMethod]
        public void IdentifierWithEmbeddedUnderscore() { AssertTokenize("_M_\xd", "Identifier(_M_)eol"); }
        [MSBuildTestMethod]
        public void IdentifierWithStringTypeCharacter() { AssertTokenize("MyString$\xd", "Identifier(MyString$)eol"); }
        [MSBuildTestMethod]
        public void IdentifierWithLongTypeCharacter() { AssertTokenize("MyString&\xd", "Identifier(MyString&)eol"); }
        [MSBuildTestMethod]
        public void IdentifierWithDecimalTypeCharacter() { AssertTokenize("MyString@\xd", "Identifier(MyString@)eol"); }
        [MSBuildTestMethod]
        public void IdentifierWithSingleTypeCharacter() { AssertTokenize("MyString!\xd", "Identifier(MyString!)eol"); }
        [MSBuildTestMethod]
        public void IdentifierWithDoubleTypeCharacter() { AssertTokenize("MyString#\xd", "Identifier(MyString#)eol"); }
        [MSBuildTestMethod]
        public void IdentifierWithIntegerTypeCharacter() { AssertTokenize("MyString%\xd", "Identifier(MyString%)eol"); }
        [MSBuildTestMethod]
        public void EscapedIdentifier() { AssertTokenize("[Namespace]\xd", "Namespace\xd", "Identifier(Namespace)eol", 1); }
        [MSBuildTestMethod]
        public void UnfinishedEscapedIdentifier() { AssertTokenize("[Namespace\xd", "ExpectedIdentifier([Namespace)"); }
        [MSBuildTestMethod]
        public void EscapedIdentifierWithoutGoodStart() { AssertTokenize("[3]\xd", "ExpectedIdentifier([)"); }
        [MSBuildTestMethod]
        public void EscapedLineContinuation() { AssertTokenize("[_]\xd", "ExpectedIdentifier([_])"); }
        [MSBuildTestMethod]
        public void EscapedButEmptyIdentifier() { AssertTokenize("[]\xd", "ExpectedIdentifier([)"); }
        [MSBuildTestMethod]
        public void EscapedIdentifierHasType() { AssertTokenize("[MyString$]\xd", "ExpectedIdentifier([MyString)"); }
        [MSBuildTestMethod]
        public void EscapedIdentifierHasTypeOnTheOutside() { AssertTokenize("[MyString]$\xd", "MyString$\xd", "Identifier(MyString)Unrecognized($)", 1); }

        // A lone underscore is an invalid identifier.
        [MSBuildTestMethod]
        public void LoneUnderscore()
        {
            AssertTokenize(
                "Sub Foo(ByVal _ As Int16)\xd",
                "Keyword(Sub).Identifier(Foo)Separator(()Keyword(ByVal).ExpectedIdentifier(_)");
        }

        // Boolean literals
        [MSBuildTestMethod]
        public void BooleanTrue() { AssertTokenize("tRuE\xd", "BooleanLiteral(tRuE)eol"); }
        [MSBuildTestMethod]
        public void BooleanFalse() { AssertTokenize("falsE\xd", "BooleanLiteral(falsE)eol"); }

        // Integer literals
        [MSBuildTestMethod]
        public void HexInteger() { AssertTokenize("&H0123456789aBcDeF\xd", "HexIntegerLiteral(&H0123456789aBcDeF)eol"); }
        [MSBuildTestMethod]
        public void Octalnteger() { AssertTokenize("&O01234567\xd", "OctalIntegerLiteral(&O01234567)eol"); }
        [MSBuildTestMethod]
        public void HexIntegerLowerCase() { AssertTokenize("&h001\xd", "HexIntegerLiteral(&h001)eol"); }
        [MSBuildTestMethod]
        public void OctalntegerUpperCase() { AssertTokenize("&o001\xd", "OctalIntegerLiteral(&o001)eol"); }
        [MSBuildTestMethod]
        public void Decimallnteger() { AssertTokenize("001\xd", "DecimalIntegerLiteral(001)eol"); }
        [MSBuildTestMethod]
        public void InvalidHexInteger() { AssertTokenize("&H00FG\xd", "HexIntegerLiteral(&H00F)Identifier(G)eol"); }
        [MSBuildTestMethod]
        public void InvalidOctalnteger() { AssertTokenize("&O0089\xd", "OctalIntegerLiteral(&O00)DecimalIntegerLiteral(89)eol"); }
        [MSBuildTestMethod]
        public void InvalidHexIntegerWithNoneValid() { AssertTokenize("&HG\xd", "ExpectedValidHexDigit(&H)"); }
        [MSBuildTestMethod]
        public void InvalidOctalntegerWithNoneValid() { AssertTokenize("&O9\xd", "ExpectedValidOctalDigit(&O)"); }
        [MSBuildTestMethod]
        public void HexIntegerShort() { AssertTokenize("&HaBcDeFS\xd", "HexIntegerLiteral(&HaBcDeFS)eol"); }
        [MSBuildTestMethod]
        public void HexIntegerShortLower() { AssertTokenize("&HaBcDeFs\xd", "HexIntegerLiteral(&HaBcDeFs)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerShort() { AssertTokenize("123S\xd", "DecimalIntegerLiteral(123S)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerShortLower() { AssertTokenize("123s\xd", "DecimalIntegerLiteral(123s)eol"); }
        [MSBuildTestMethod]
        public void OctalntegerShort() { AssertTokenize("&O01234567S\xd", "OctalIntegerLiteral(&O01234567S)eol"); }
        [MSBuildTestMethod]
        public void OctalntegerShortLower() { AssertTokenize("&O01234567s\xd", "OctalIntegerLiteral(&O01234567s)eol"); }
        [MSBuildTestMethod]
        public void HexIntegerInteger() { AssertTokenize("&HaBcDeFI\xd", "HexIntegerLiteral(&HaBcDeFI)eol"); }
        [MSBuildTestMethod]
        public void HexIntegerIntegerLower() { AssertTokenize("&HaBcDeFi\xd", "HexIntegerLiteral(&HaBcDeFi)eol"); }
        [MSBuildTestMethod]
        public void OctalntegerInteger() { AssertTokenize("&O01234567I\xd", "OctalIntegerLiteral(&O01234567I)eol"); }
        [MSBuildTestMethod]
        public void OctalntegerIntegerLower() { AssertTokenize("&O01234567i\xd", "OctalIntegerLiteral(&O01234567i)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerInteger() { AssertTokenize("123I\xd", "DecimalIntegerLiteral(123I)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerIntegerLower() { AssertTokenize("123i\xd", "DecimalIntegerLiteral(123i)eol"); }
        [MSBuildTestMethod]
        public void HexIntegerLong() { AssertTokenize("&HaBcDeFL\xd", "HexIntegerLiteral(&HaBcDeFL)eol"); }
        [MSBuildTestMethod]
        public void HexIntegerLongLower() { AssertTokenize("&HaBcDeFl\xd", "HexIntegerLiteral(&HaBcDeFl)eol"); }
        [MSBuildTestMethod]
        public void OctalntegerLong() { AssertTokenize("&O01234567L\xd", "OctalIntegerLiteral(&O01234567L)eol"); }
        [MSBuildTestMethod]
        public void OctalntegerLongLower() { AssertTokenize("&O01234567l\xd", "OctalIntegerLiteral(&O01234567l)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerLong() { AssertTokenize("123L\xd", "DecimalIntegerLiteral(123L)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerIntegerLong() { AssertTokenize("123l\xd", "DecimalIntegerLiteral(123l)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerWithIntegerTypeChar() { AssertTokenize("1234%\xd", "DecimalIntegerLiteral(1234%)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerWithLongTypeChar() { AssertTokenize("1234&\xd", "DecimalIntegerLiteral(1234&)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerWithDecimalTypeChar() { AssertTokenize("1234@\xd", "DecimalIntegerLiteral(1234@)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerWithSingleTypeChar() { AssertTokenize("1234!\xd", "DecimalIntegerLiteral(1234!)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerWithDoubleTypeChar() { AssertTokenize("1234#\xd", "DecimalIntegerLiteral(1234#)eol"); }
        [MSBuildTestMethod]
        public void DecimalIntegerWithStringTypeChar() { AssertTokenize("1234$\xd", "DecimalIntegerLiteral(1234)Unrecognized($)"); }

        // String literal
        [MSBuildTestMethod]
        public void BasicString() { AssertTokenize("\"A string\"\xd", "StringLiteral(\"A string\")eol"); }
        [MSBuildTestMethod]
        public void StringWithDoubledQuotesAsEscape() { AssertTokenize("\"\"\"\"\x0d", "\"\"\"\"\x0d", "StringLiteral(\"\"\"\")eol", 1); }
        [MSBuildTestMethod]
        public void StringUnclosed() { AssertTokenize("\"string\x0d", "EndOfFileInsideString(\"string\x0d)"); }

        // Operators
        [MSBuildTestMethod]
        public void CheckAllOperators()
        {
            AssertTokenize(
                "a=1 & 2*3+4-5/6\\7^8<9=10>11\xd",
                @"Identifier(a)Operator(=)DecimalIntegerLiteral(1).Operator(&).DecimalIntegerLiteral(2)Operator(*)DecimalIntegerLiteral(3)Operator(+)DecimalIntegerLiteral(4)Operator(-)DecimalIntegerLiteral(5)Operator(/)DecimalIntegerLiteral(6)Operator(\)DecimalIntegerLiteral(7)Operator(^)DecimalIntegerLiteral(8)Operator(<)DecimalIntegerLiteral(9)Operator(=)DecimalIntegerLiteral(10)Operator(>)DecimalIntegerLiteral(11)eol");
        }

        // Inplace arrays
        [MSBuildTestMethod]
        public void InplaceArray()
        {
            AssertTokenize(
                "Me.Controls.AddRange(New Control() {Me.lblCodebase, Me.lblCopyright})\xd",
                "Keyword(Me)Separator(.)Identifier(Controls)Separator(.)Identifier(AddRange)Separator(()Keyword(New).Identifier(Control)Separator(()Separator()).Separator({)Keyword(Me)Separator(.)Identifier(lblCodebase)Separator(,).Keyword(Me)Separator(.)Identifier(lblCopyright)Separator(})Separator())eol");
        }

        // Keywords
        [MSBuildTestMethod]
        public void SimpleKeyword() { AssertTokenize("Namespace\xd", "Keyword(Namespace)eol"); }

        // From the real world
        [MSBuildTestMethod]
        public void WackyBrackettedClassName()
        {
            AssertTokenize(
                "Public Class [!output SAFE_ITEM_NAME]\xd",
                "Keyword(Public).Keyword(Class).ExpectedIdentifier([)");
        }
        [MSBuildTestMethod]
        public void MyClassIsAKeyword()
        {
            AssertTokenize(
                "Class MyClass\xd",
                "Keyword(Class).Keyword(MyClass)eol");
        }


        [MSBuildTestMethod]
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
        private static void AssertTokenize(string source, string expectedTokenKey)
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
        private static void AssertTokenize(
           string source,
           string expectedSource,
           string expectedTokenKey,
           int expectedLastLineNumber)
        {
            VisualBasicTokenizer tokens = new VisualBasicTokenizer(
                StreamHelpers.StringToStream(source),
                false);
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
