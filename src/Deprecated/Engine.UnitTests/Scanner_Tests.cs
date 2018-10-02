// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Reflection;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;


namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ScannerTest
    {
        /// <summary>
        /// Tests that we give a useful error position (not 0 for example)
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ErrorPosition()
        {
            string[,] tests = {
                { "1==1.1.",                "7",    "AllowAll"},              // Position of second '.'
                { "1==0xFG",                "7",    "AllowAll"},              // Position of G
                { "1==-0xF",                "6",    "AllowAll"},              // Position of x
                { "1234=5678",              "6",    "AllowAll"},              // Position of '5'
                { " ",                      "2",    "AllowAll"},              // Position of End of Input
                { " (",                     "3",    "AllowAll"},              // Position of End of Input
                { " false or  ",            "12",   "AllowAll"},              // Position of End of Input
                { " \"foo",                 "2",    "AllowAll"},              // Position of open quote
                { " @(foo",                 "2",    "AllowAll"},              // Position of @
                { " @(",                    "2",    "AllowAll"},              // Position of @
                { " $",                     "2",    "AllowAll"},              // Position of $
                { " $(foo",                 "2",    "AllowAll"},              // Position of $
                { " $(",                    "2",    "AllowAll"},              // Position of $
                { " $",                     "2",    "AllowAll"},              // Position of $
                { " @(foo)",                "2",    "AllowProperties"},       // Position of @
                { " '@(foo)'",              "3",    "AllowProperties"},       // Position of @    
                /* test escaped chars: message shows them escaped so count should include them */
                { "'%24%28x' == '%24(x''",   "21",  "AllowAll"}               // Position of extra quote 
            };

            // Some errors are caught by the Parser, not merely by the Lexer/Scanner. So we have to do a full Parse,
            // rather than just calling AdvanceToScannerError(). (The error location is still supplied by the Scanner.)
            for (int i = 0; i < tests.GetLength(0); i++)
            {
                Parser parser = null;
                try
                {
                    parser = new Parser();
                    ParserOptions options = (ParserOptions)Enum.Parse(typeof(ParserOptions), tests[i, 2], true /* case-insensitive */);
                    GenericExpressionNode parsedExpression = parser.Parse(tests[i, 0], null, options);
                }
                catch (InvalidProjectFileException ex)
                {
                    Console.WriteLine(ex.Message);
                    Assertion.Assert("Expression '" + tests[i, 0] + "' should have an error at " + tests[i, 1] + " but it was at " + parser.errorPosition,
                            Convert.ToInt32(tests[i, 1]) == parser.errorPosition);
                }
            }
        }
 
        /// <summary>
        /// Advance to the point of the lexer error. If the error is only caught by the parser, this isn't useful.
        /// </summary>
        /// <param name="lexer"></param>
        private void AdvanceToScannerError(Scanner lexer)
        {
            while (true)
            {
                if (!lexer.Advance()) break;
                if (lexer.IsNext(Token.TokenType.EndOfInput)) break;
            }
        }

        /// <summary>
        /// Tests the special error for "=".
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void SingleEquals()
        {
            Scanner lexer;

            lexer = new Scanner("a=b", ParserOptions.AllowProperties);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedEqualsInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == "b");
        }

        /// <summary>
        /// Tests the special errors for "$(" and "$x" and similar cases
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void IllFormedProperty()
        {
            Scanner lexer;

            lexer = new Scanner("$(", ParserOptions.AllowProperties);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedPropertyCloseParenthesisInCondition");

            lexer = new Scanner("$x", ParserOptions.AllowProperties);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedPropertyOpenParenthesisInCondition");
        }

        /// <summary>
        /// Tests the special errors for "@(" and "@x" and similar cases.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void IllFormedItemList()
        {
            Scanner lexer;

            lexer = new Scanner("@(", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedItemListCloseParenthesisInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == null);

            lexer = new Scanner("@x", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedItemListOpenParenthesisInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == null);

            lexer = new Scanner("@(x", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedItemListCloseParenthesisInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == null);

            lexer = new Scanner("@(x->'%(y)", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedItemListQuoteInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == null);

            lexer = new Scanner("@(x->'%(y)', 'x", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedItemListQuoteInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == null);

            lexer = new Scanner("@(x->'%(y)', 'x'", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedItemListCloseParenthesisInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == null);
        }

        /// <summary>
        /// Tests the special error for unterminated quotes.
        /// Note, scanner only understands single quotes.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void IllFormedQuotedString()
        {
            Scanner lexer;

            lexer = new Scanner("false or 'abc", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedQuotedStringInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == null);

            lexer = new Scanner("\'", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assertion.Assert(lexer.GetErrorResource() == "IllFormedQuotedStringInCondition");
            Assertion.Assert(lexer.UnexpectedlyFound == null);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void NumericSingleTokenTests()
        {
            Scanner lexer;
            
            lexer = new Scanner("1234", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare("1234", lexer.IsNextString()), 0);

            lexer = new Scanner("-1234", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare("-1234", lexer.IsNextString()), 0);

            lexer = new Scanner("+1234", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare("+1234", lexer.IsNextString()), 0);

            lexer = new Scanner("1234.1234", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare("1234.1234", lexer.IsNextString()), 0);

            lexer = new Scanner(".1234", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare(".1234", lexer.IsNextString()), 0);

            lexer = new Scanner("1234.", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare("1234.", lexer.IsNextString()), 0);
            lexer = new Scanner("0x1234", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare("0x1234", lexer.IsNextString()), 0);
            lexer = new Scanner("0X1234abcd", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare("0X1234abcd", lexer.IsNextString()), 0);
            lexer = new Scanner("0x1234ABCD", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            Assertion.AssertEquals(String.Compare("0x1234ABCD", lexer.IsNextString()), 0);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void PropsStringsAndBooleanSingleTokenTests()
        {
            Scanner lexer = new Scanner("$(foo)", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Property), true);
            lexer = new Scanner("@(foo)", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.ItemList), true);
            lexer = new Scanner("abcde", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.String), true);
            Assertion.AssertEquals(String.Compare("abcde", lexer.IsNextString()), 0);

            lexer = new Scanner("'abc-efg'", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.String), true);
            Assertion.AssertEquals(String.Compare("abc-efg", lexer.IsNextString()), 0);

            lexer = new Scanner("and", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.And), true);
            Assertion.AssertEquals(String.Compare("and", lexer.IsNextString()), 0);
            lexer = new Scanner("or", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Or), true);
            Assertion.AssertEquals(String.Compare("or", lexer.IsNextString()), 0);
            lexer = new Scanner("AnD", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.And), true);
            Assertion.AssertEquals(String.Compare("AnD", lexer.IsNextString()), 0);
            lexer = new Scanner("Or", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Or), true);
            Assertion.AssertEquals(String.Compare("Or", lexer.IsNextString()), 0);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void SimpleSingleTokenTests ()
        {
            Scanner lexer = new Scanner("(", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.LeftParenthesis), true);
            lexer = new Scanner(")", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.RightParenthesis), true);
            lexer = new Scanner(",", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Comma), true);
            lexer = new Scanner("==", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.EqualTo), true);
            lexer = new Scanner("!=", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.NotEqualTo), true);
            lexer = new Scanner("<", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.LessThan), true);
            lexer = new Scanner(">", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.GreaterThan), true);
            lexer = new Scanner("<=", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.LessThanOrEqualTo), true);
            lexer = new Scanner(">=", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.GreaterThanOrEqualTo), true);
            lexer = new Scanner("!", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Not), true);
        }


        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void StringEdgeTests()
        {
            Scanner lexer;

            lexer = new Scanner("@(Foo, ' ')", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("'@(Foo, ' ')'", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("'%40(( '", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("'@(Complex_ItemType-123, ';')' == ''", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void FunctionTests()
        {
            Scanner lexer;

            lexer = new Scanner("Foobar()", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foobar( 1 )", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foobar( $(Property) )", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foobar( @(ItemList) )", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foobar( simplestring )", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foobar( 'Not a Simple String' )", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foobar( 'Not a Simple String', 1234 )", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foobar( $(Property), 'Not a Simple String', 1234 )", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foobar( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assertion.AssertEquals(String.Compare("Foobar", lexer.IsNextString()), 0);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void ComplexTests1 ()
        {
            Scanner lexer;

            lexer = new Scanner("'String with a $(Property) inside'", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.AssertEquals(String.Compare("String with a $(Property) inside", lexer.IsNextString()), 0);

            lexer = new Scanner("'String with an embedded \\' in it'", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
//          Assertion.AssertEquals(String.Compare("String with an embedded ' in it", lexer.IsNextString()), 0);

            lexer = new Scanner("'String with a $(Property) inside'", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.AssertEquals(String.Compare("String with a $(Property) inside", lexer.IsNextString()), 0);

            lexer = new Scanner("@(list, ' ')", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assertion.AssertEquals(String.Compare("@(list, ' ')", lexer.IsNextString()), 0);

            lexer = new Scanner("@(files->'%(Filename)')", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assertion.AssertEquals(String.Compare("@(files->'%(Filename)')", lexer.IsNextString()), 0);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void ComplexTests2()
        {
            Scanner lexer = new Scanner("1234", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());

            lexer = new Scanner("'abc-efg'==$(foo)", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.String), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.EqualTo), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Property), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.EndOfInput), true);

            lexer = new Scanner("$(debug)!=true", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Property), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.NotEqualTo), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.String), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.EndOfInput), true);

            lexer = new Scanner("$(VERSION)<5", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance());
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Property), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.LessThan), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.Numeric), true);
            lexer.Advance();
            Assertion.AssertEquals(lexer.IsNext(Token.TokenType.EndOfInput), true);
        }

        /// <summary>
        /// Tests all tokens with no whitespace and whitespace.
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void WhitespaceTests()
        {
            Scanner lexer;
            Console.WriteLine("here");
            lexer = new Scanner("$(DEBUG) and $(FOO)", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.And));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));

            lexer = new Scanner("1234$(DEBUG)0xabcd@(foo)asdf<>'foobar'<=false>=true==1234!=", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LessThan));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.GreaterThan));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LessThanOrEqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.GreaterThanOrEqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.NotEqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("   1234    $(DEBUG)    0xabcd  \n@(foo)    \nasdf  \n<     \n>     \n'foobar'  \n<=    \nfalse     \n>=    \ntrue  \n== \n 1234    \n!=     ", ParserOptions.AllowAll);
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LessThan));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.GreaterThan));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.LessThanOrEqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.GreaterThanOrEqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.NotEqualTo));
            Assertion.Assert(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));
        }

        /// <summary>
        /// Tests the parsing of item lists.
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void ItemListTests()
        {
            Scanner lexer;

            lexer = new Scanner("@(foo)", ParserOptions.AllowProperties);
            Assertion.Assert(!lexer.Advance());
            Assertion.Assert(String.Compare(lexer.GetErrorResource(), "ItemListNotAllowedInThisConditional") == 0);

            lexer = new Scanner("1234 '@(foo)'", ParserOptions.AllowProperties);
            Assertion.Assert(lexer.Advance());
            Assertion.Assert(!lexer.Advance());
            Assertion.Assert(String.Compare(lexer.GetErrorResource(), "ItemListNotAllowedInThisConditional") == 0);

            lexer = new Scanner("'1234 @(foo)'", ParserOptions.AllowProperties);
            Assertion.Assert(!lexer.Advance());
            Assertion.Assert(String.Compare(lexer.GetErrorResource(), "ItemListNotAllowedInThisConditional") == 0);
        }

        /// <summary>
        /// Tests that shouldn't work.
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void NegativeTests()
        {
            Scanner lexer;

            lexer = new Scanner("'$(DEBUG) == true", ParserOptions.AllowAll);
            Assertion.Assert(!lexer.Advance());
        }
    }
}
