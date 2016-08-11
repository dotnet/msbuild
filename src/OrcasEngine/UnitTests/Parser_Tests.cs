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
    public class ParserTest
    {
        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void SimpleParseTest()
        {
            Console.WriteLine("SimpleParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;

            tree = p.Parse("$(foo)", null, ParserOptions.AllowAll);


            tree = p.Parse("$(foo)=='hello'", null, ParserOptions.AllowAll);


            tree = p.Parse("$(foo)==''", null, ParserOptions.AllowAll);


            tree = p.Parse("$(debug) and $(buildlab) and $(full)", null, ParserOptions.AllowAll);


            tree = p.Parse("$(debug) or $(buildlab) or $(full)", null, ParserOptions.AllowAll);


            tree = p.Parse("$(debug) and $(buildlab) or $(full)", null, ParserOptions.AllowAll);


            tree = p.Parse("$(full) or $(debug) and $(buildlab)", null, ParserOptions.AllowAll);


            tree = p.Parse("%(culture)", null, ParserOptions.AllowAll);


            tree = p.Parse("%(culture)=='french'", null, ParserOptions.AllowAll);


            tree = p.Parse("'foo_%(culture)'=='foo_french'", null, ParserOptions.AllowAll);


            tree = p.Parse("true", null, ParserOptions.AllowAll);


            tree = p.Parse("false", null, ParserOptions.AllowAll);


            tree = p.Parse("0", null, ParserOptions.AllowAll);


            tree = p.Parse("0.0 == 0", null, ParserOptions.AllowAll);

        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void ComplexParseTest()
        {
            Console.WriteLine("ComplexParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;

            tree = p.Parse("$(foo)", null, ParserOptions.AllowAll);


            tree = p.Parse("($(foo) or $(bar)) and $(baz)", null, ParserOptions.AllowAll);


            tree = p.Parse("$(foo) <= 5 and $(bar) >= 15", null, ParserOptions.AllowAll);


            tree = p.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == simplestring) and 'a more complex string' != $(quux)", null, ParserOptions.AllowAll);


            tree = p.Parse("(($(foo) or $(bar) == false) and !($(baz) == simplestring))", null, ParserOptions.AllowAll);


            tree = p.Parse("(($(foo) or Exists('c:\\foobar.txt')) and !(($(baz) == simplestring)))", null, ParserOptions.AllowAll);


            tree = p.Parse("'CONTAINS%27QUOTE%27' == '$(TestQuote)'", null, ParserOptions.AllowAll);

        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void NotParseTest()
        {
            Console.WriteLine("NegationParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            tree = p.Parse("!true", null, ParserOptions.AllowAll);

            tree = p.Parse("!(true)", null, ParserOptions.AllowAll);

            tree = p.Parse("!($(foo) <= 5)", null, ParserOptions.AllowAll);

            tree = p.Parse("!(%(foo) <= 5)", null, ParserOptions.AllowAll);

            tree = p.Parse("!($(foo) <= 5 and $(bar) >= 15)", null, ParserOptions.AllowAll);

        }
        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void FunctionCallParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            tree = p.Parse("SimpleFunctionCall()", null, ParserOptions.AllowAll);
            
            tree = p.Parse("SimpleFunctionCall( 1234 )", null, ParserOptions.AllowAll);
            tree = p.Parse("SimpleFunctionCall( true )", null, ParserOptions.AllowAll);
            tree = p.Parse("SimpleFunctionCall( $(property) )", null, ParserOptions.AllowAll);
            
            tree = p.Parse("SimpleFunctionCall( $(property), 1234, abcd, 'abcd efgh' )", null, ParserOptions.AllowAll);
            
        }

        /// <owner>DavidLe</owner>
        [Test]
        public void ItemListParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            bool fExceptionCaught;

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("@(foo) == 'a.cs;b.cs'", null, ParserOptions.AllowProperties);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'a.cs;b.cs' == @(foo)", null, ParserOptions.AllowProperties);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'@(foo)' == 'a.cs;b.cs'", null, ParserOptions.AllowProperties);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'otherstuff@(foo)' == 'a.cs;b.cs'", null, ParserOptions.AllowProperties);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'@(foo)otherstuff' == 'a.cs;b.cs'", null, ParserOptions.AllowProperties);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("somefunction(@(foo), 'otherstuff')", null, ParserOptions.AllowProperties);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <owner>RGoel</owner>
        [Test]
        public void MetadataParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            bool fExceptionCaught;

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("%(foo) == 'a.cs;b.cs'", null, ParserOptions.AllowProperties | ParserOptions.AllowItemLists);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'a.cs;b.cs' == %(foo)", null, ParserOptions.AllowProperties | ParserOptions.AllowItemLists);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'%(foo)' == 'a.cs;b.cs'", null, ParserOptions.AllowProperties | ParserOptions.AllowItemLists);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'otherstuff%(foo)' == 'a.cs;b.cs'", null, ParserOptions.AllowProperties | ParserOptions.AllowItemLists);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'%(foo)otherstuff' == 'a.cs;b.cs'", null, ParserOptions.AllowProperties | ParserOptions.AllowItemLists);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("somefunction(%(foo), 'otherstuff')", null, ParserOptions.AllowProperties | ParserOptions.AllowItemLists);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void NegativeTests()
        {
            Console.WriteLine("NegativeTests()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            bool fExceptionCaught;

            try
            {
                fExceptionCaught = false;
                // Note no close quote ----------------------------------------------------V
                tree = p.Parse("'a more complex' == 'asdf", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Note no close quote ----------------------------------------------------V
                tree = p.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == 'simple string) and 'a more complex string' != $(quux)", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("($(foo) == 'simple string') $(bar)", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("=='x'", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("==", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse(">", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("true!=false==", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("true!=false==true", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("1==(2", null, ParserOptions.AllowAll);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assertion.Assert(fExceptionCaught);
        }

        /// <summary>
        /// This test verifies that we trigger warnings for expressions that
        /// could be incorrectly evaluated
        /// </summary>
        /// <owner>VladF</owner>
        [Test]
        public void VerifyWarningForOrder()
        {
            // Create a project file that has an expression
            MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 and $(b) == 2 or $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assertion.Assert("Need to warn for this expression - (a) == 1 and $(b) == 2 or $(c) == 3.", 
                ml.FullLog.Contains("MSB4130:"));

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 or $(b) == 2 and $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assertion.Assert("Need to warn for this expression - (a) == 1 or $(b) == 2 and $(c) == 3.",
                ml.FullLog.Contains("MSB4130:"));

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assertion.Assert("Need to warn for this expression - ($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4.",
                ml.FullLog.Contains("MSB4130:"));
        }

        /// <summary>
        /// This test verifies that we don't trigger warnings for expressions that
        /// couldn't be incorrectly evaluated
        /// </summary>
        /// <owner>VladF</owner>
        [Test]
        public void VerifyNoWarningForOrder()
        {
            // Create a project file that has an expression
            MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 and $(b) == 2 and $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assertion.Assert("No need to warn for this expression - (a) == 1 and $(b) == 2 and $(c) == 3.",
                !ml.FullLog.Contains("MSB4130:"));

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 or $(b) == 2 or $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assertion.Assert("No need to warn for this expression - (a) == 1 or $(b) == 2 or $(c) == 3.",
                !ml.FullLog.Contains("MSB4130:"));

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 and $(b) == 2) or $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assertion.Assert("No need to warn for this expression - ($(a) == 1 and $(b) == 2) or $(c) == 3.",
                !ml.FullLog.Contains("MSB4130:"));

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(String.Format(@"
                    <Project ToolsVersion=`3.5` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 or $(b) == 2) and $(c) == 3`/>
                        </Target>
                    </Project>
                ", new Uri(Assembly.GetExecutingAssembly().EscapedCodeBase).LocalPath));

            // Make sure the log contains the correct strings.
            Assertion.Assert("No need to warn for this expression - ($(a) == 1 or $(b) == 2) and $(c) == 3.",
                !ml.FullLog.Contains("MSB4130:"));
        }

    }
}
