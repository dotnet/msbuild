// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Xunit;



namespace Microsoft.Build.UnitTests
{
    public class ScannerTest
    {
        private MockElementLocation _elementLocation = MockElementLocation.Instance;
        /// <summary>
        /// Tests that we give a useful error position (not 0 for example)
        /// </summary>
        [Fact]
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
                    parser.Parse(tests[i, 0], options, _elementLocation);
                }
                catch (InvalidProjectFileException ex)
                {
                    Console.WriteLine(ex.Message);
                    Assert.Equal(Convert.ToInt32(tests[i, 1]), parser.errorPosition);
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
        [Fact]
        public void SingleEquals()
        {
            Scanner lexer;

            lexer = new Scanner("a=b", ParserOptions.AllowProperties);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedEqualsInCondition", lexer.GetErrorResource());
            Assert.Equal("b", lexer.UnexpectedlyFound);
        }

        /// <summary>
        /// Tests the special errors for "$(" and "$x" and similar cases
        /// </summary>
        [Fact]
        public void IllFormedProperty()
        {
            Scanner lexer;

            lexer = new Scanner("$(", ParserOptions.AllowProperties);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedPropertyCloseParenthesisInCondition", lexer.GetErrorResource());

            lexer = new Scanner("$x", ParserOptions.AllowProperties);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedPropertyOpenParenthesisInCondition", lexer.GetErrorResource());
        }

        /// <summary>
        /// Tests the special errors for "@(" and "@x" and similar cases.
        /// </summary>
        [Fact]
        public void IllFormedItemList()
        {
            Scanner lexer;

            lexer = new Scanner("@(", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedItemListCloseParenthesisInCondition", lexer.GetErrorResource());
            Assert.Null(lexer.UnexpectedlyFound);

            lexer = new Scanner("@x", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedItemListOpenParenthesisInCondition", lexer.GetErrorResource());
            Assert.Null(lexer.UnexpectedlyFound);

            lexer = new Scanner("@(x", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedItemListCloseParenthesisInCondition", lexer.GetErrorResource());
            Assert.Null(lexer.UnexpectedlyFound);

            lexer = new Scanner("@(x->'%(y)", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedItemListQuoteInCondition", lexer.GetErrorResource());
            Assert.Null(lexer.UnexpectedlyFound);

            lexer = new Scanner("@(x->'%(y)', 'x", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedItemListQuoteInCondition", lexer.GetErrorResource());
            Assert.Null(lexer.UnexpectedlyFound);

            lexer = new Scanner("@(x->'%(y)', 'x'", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedItemListCloseParenthesisInCondition", lexer.GetErrorResource());
            Assert.Null(lexer.UnexpectedlyFound);
        }

        /// <summary>
        /// Tests the special error for unterminated quotes.
        /// Note, scanner only understands single quotes.
        /// </summary>
        [Fact]
        public void IllFormedQuotedString()
        {
            Scanner lexer;

            lexer = new Scanner("false or 'abc", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedQuotedStringInCondition", lexer.GetErrorResource());
            Assert.Null(lexer.UnexpectedlyFound);

            lexer = new Scanner("\'", ParserOptions.AllowAll);
            AdvanceToScannerError(lexer);
            Assert.Equal("IllFormedQuotedStringInCondition", lexer.GetErrorResource());
            Assert.Null(lexer.UnexpectedlyFound);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void NumericSingleTokenTests()
        {
            Scanner lexer;

            lexer = new Scanner("1234", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare("1234", lexer.IsNextString()));

            lexer = new Scanner("-1234", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare("-1234", lexer.IsNextString()));

            lexer = new Scanner("+1234", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare("+1234", lexer.IsNextString()));

            lexer = new Scanner("1234.1234", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare("1234.1234", lexer.IsNextString()));

            lexer = new Scanner(".1234", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare(".1234", lexer.IsNextString()));

            lexer = new Scanner("1234.", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare("1234.", lexer.IsNextString()));
            lexer = new Scanner("0x1234", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare("0x1234", lexer.IsNextString()));
            lexer = new Scanner("0X1234abcd", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare("0X1234abcd", lexer.IsNextString()));
            lexer = new Scanner("0x1234ABCD", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            Assert.Equal(0, String.Compare("0x1234ABCD", lexer.IsNextString()));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void PropsStringsAndBooleanSingleTokenTests()
        {
            Scanner lexer = new Scanner("$(foo)", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Property));
            lexer = new Scanner("@(foo)", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.ItemList));
            lexer = new Scanner("abcde", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.String));
            Assert.Equal(0, String.Compare("abcde", lexer.IsNextString()));

            lexer = new Scanner("'abc-efg'", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.String));
            Assert.Equal(0, String.Compare("abc-efg", lexer.IsNextString()));

            lexer = new Scanner("and", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.And));
            Assert.Equal(0, String.Compare("and", lexer.IsNextString()));
            lexer = new Scanner("or", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Or));
            Assert.Equal(0, String.Compare("or", lexer.IsNextString()));
            lexer = new Scanner("AnD", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.And));
            Assert.Equal(0, String.Compare(Token.And.String, lexer.IsNextString()));
            lexer = new Scanner("Or", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Or));
            Assert.Equal(0, String.Compare(Token.Or.String, lexer.IsNextString()));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void SimpleSingleTokenTests()
        {
            Scanner lexer = new Scanner("(", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.LeftParenthesis));
            lexer = new Scanner(")", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.RightParenthesis));
            lexer = new Scanner(",", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Comma));
            lexer = new Scanner("==", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.EqualTo));
            lexer = new Scanner("!=", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.NotEqualTo));
            lexer = new Scanner("<", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.LessThan));
            lexer = new Scanner(">", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.GreaterThan));
            lexer = new Scanner("<=", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.LessThanOrEqualTo));
            lexer = new Scanner(">=", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.GreaterThanOrEqualTo));
            lexer = new Scanner("!", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Not));
        }


        /// <summary>
        /// </summary>
        [Fact]
        public void StringEdgeTests()
        {
            Scanner lexer;

            lexer = new Scanner("@(Foo, ' ')", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("'@(Foo, ' ')'", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("'%40(( '", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("'@(Complex_ItemType-123, ';')' == ''", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void FunctionTests()
        {
            Scanner lexer;

            lexer = new Scanner("Foo()", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foo( 1 )", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foo( $(Property) )", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foo( @(ItemList) )", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foo( simplestring )", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foo( 'Not a Simple String' )", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foo( 'Not a Simple String', 1234 )", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foo( $(Property), 'Not a Simple String', 1234 )", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));

            lexer = new Scanner("Foo( @(ItemList), $(Property), simplestring, 'Not a Simple String', 1234 )", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Function));
            Assert.Equal(0, String.Compare("Foo", lexer.IsNextString()));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LeftParenthesis));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Comma));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.RightParenthesis));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ComplexTests1()
        {
            Scanner lexer;

            lexer = new Scanner("'String with a $(Property) inside'", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.Equal(0, String.Compare("String with a $(Property) inside", lexer.IsNextString()));

            lexer = new Scanner("'String with an embedded \\' in it'", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            //          Assert.AreEqual(String.Compare("String with an embedded ' in it", lexer.IsNextString()), 0);

            lexer = new Scanner("'String with a $(Property) inside'", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.Equal(0, String.Compare("String with a $(Property) inside", lexer.IsNextString()));

            lexer = new Scanner("@(list, ' ')", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assert.Equal(0, String.Compare("@(list, ' ')", lexer.IsNextString()));

            lexer = new Scanner("@(files->'%(Filename)')", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assert.Equal(0, String.Compare("@(files->'%(Filename)')", lexer.IsNextString()));
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ComplexTests2()
        {
            Scanner lexer = new Scanner("1234", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());

            lexer = new Scanner("'abc-efg'==$(foo)", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.String));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.EqualTo));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.Property));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("$(debug)!=true", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Property));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.NotEqualTo));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.String));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("$(VERSION)<5", ParserOptions.AllowAll);
            Assert.True(lexer.Advance());
            Assert.True(lexer.IsNext(Token.TokenType.Property));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.LessThan));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.Numeric));
            lexer.Advance();
            Assert.True(lexer.IsNext(Token.TokenType.EndOfInput));
        }

        /// <summary>
        /// Tests all tokens with no whitespace and whitespace.
        /// </summary>
        [Fact]
        public void WhitespaceTests()
        {
            Scanner lexer;
            Console.WriteLine("here");
            lexer = new Scanner("$(DEBUG) and $(FOO)", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.And));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));

            lexer = new Scanner("1234$(DEBUG)0xabcd@(foo)asdf<>'foo'<=false>=true==1234!=", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LessThan));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.GreaterThan));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LessThanOrEqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.GreaterThanOrEqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.NotEqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));

            lexer = new Scanner("   1234    $(DEBUG)    0xabcd  \n@(foo)    \nasdf  \n<     \n>     \n'foo'  \n<=    \nfalse     \n>=    \ntrue  \n== \n 1234    \n!=     ", ParserOptions.AllowAll);
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Property));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.ItemList));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LessThan));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.GreaterThan));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.LessThanOrEqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.GreaterThanOrEqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.String));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.Numeric));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.NotEqualTo));
            Assert.True(lexer.Advance() && lexer.IsNext(Token.TokenType.EndOfInput));
        }

        /// <summary>
        /// Tests the parsing of item lists.
        /// </summary>
        [Fact]
        public void ItemListTests()
        {
            Scanner lexer;

            lexer = new Scanner("@(foo)", ParserOptions.AllowProperties);
            Assert.False(lexer.Advance());
            Assert.Equal(0, String.Compare(lexer.GetErrorResource(), "ItemListNotAllowedInThisConditional"));

            lexer = new Scanner("1234 '@(foo)'", ParserOptions.AllowProperties);
            Assert.True(lexer.Advance());
            Assert.False(lexer.Advance());
            Assert.Equal(0, String.Compare(lexer.GetErrorResource(), "ItemListNotAllowedInThisConditional"));

            lexer = new Scanner("'1234 @(foo)'", ParserOptions.AllowProperties);
            Assert.False(lexer.Advance());
            Assert.Equal(0, String.Compare(lexer.GetErrorResource(), "ItemListNotAllowedInThisConditional"));
        }

        /// <summary>
        /// Tests that shouldn't work.
        /// </summary>
        [Fact]
        public void NegativeTests()
        {
            Scanner lexer;

            lexer = new Scanner("'$(DEBUG) == true", ParserOptions.AllowAll);
            Assert.False(lexer.Advance());
        }
    }
}
