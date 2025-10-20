// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Xunit;



#nullable disable

namespace Microsoft.Build.UnitTests
{
    public class ParserTest
    {
        /// <summary>
        ///  Make a fake element location for methods who need one.
        /// </summary>
        private MockElementLocation _elementLocation = MockElementLocation.Instance;

        /// <summary>
        /// </summary>
        [Fact]
        public void SimpleParseTest()
        {
            Console.WriteLine("SimpleParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree = p.Parse("$(foo)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(foo)=='hello'", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(foo)==''", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(debug) and $(buildlab) and $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(debug) or $(buildlab) or $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(debug) and $(buildlab) or $(full)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(full) or $(debug) and $(buildlab)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("%(culture)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("%(culture)=='french'", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("'foo_%(culture)'=='foo_french'", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("true", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("false", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("0", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("0.0 == 0", ParserOptions.AllowAll, _elementLocation);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ComplexParseTest()
        {
            Console.WriteLine("ComplexParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree = p.Parse("$(foo)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("($(foo) or $(bar)) and $(baz)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("$(foo) <= 5 and $(bar) >= 15", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == simplestring) and 'a more complex string' != $(quux)", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("(($(foo) or $(bar) == false) and !($(baz) == simplestring))", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("(($(foo) or Exists('c:\\foo.txt')) and !(($(baz) == simplestring)))", ParserOptions.AllowAll, _elementLocation);


            tree = p.Parse("'CONTAINS%27QUOTE%27' == '$(TestQuote)'", ParserOptions.AllowAll, _elementLocation);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void NotParseTest()
        {
            Console.WriteLine("NegationParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree = p.Parse("!true", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("!(true)", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("!($(foo) <= 5)", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("!(%(foo) <= 5)", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("!($(foo) <= 5 and $(bar) >= 15)", ParserOptions.AllowAll, _elementLocation);
        }
        /// <summary>
        /// </summary>
        [Fact]
        public void FunctionCallParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree = p.Parse("SimpleFunctionCall()", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("SimpleFunctionCall( 1234 )", ParserOptions.AllowAll, _elementLocation);
            tree = p.Parse("SimpleFunctionCall( true )", ParserOptions.AllowAll, _elementLocation);
            tree = p.Parse("SimpleFunctionCall( $(property) )", ParserOptions.AllowAll, _elementLocation);

            tree = p.Parse("SimpleFunctionCall( $(property), 1234, abcd, 'abcd efgh' )", ParserOptions.AllowAll, _elementLocation);
        }

        [Fact]
        public void ItemListParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            bool fExceptionCaught = false;
            try
            {
                tree = p.Parse("@(foo) == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'a.cs;b.cs' == @(foo)", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'@(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'otherstuff@(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'@(foo)otherstuff' == 'a.cs;b.cs'", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("somefunction(@(foo), 'otherstuff')", ParserOptions.AllowProperties, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);
        }

        [Fact]
        public void ItemFuncParseTest()
        {
            Console.WriteLine("ItemFuncParseTest()");

            Parser p = new Parser();
            GenericExpressionNode tree = p.Parse("@(item->foo('ab'))",
                ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            Assert.IsType<StringExpressionNode>(tree);
            Assert.Equal("@(item->foo('ab'))", tree.GetUnexpandedValue(null));

            tree = p.Parse("!@(item->foo())",
                ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            Assert.IsType<NotExpressionNode>(tree);

            tree = p.Parse("(@(item->foo('ab')) and @(item->foo('bc')))",
                ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            Assert.IsType<AndExpressionNode>(tree);
        }

        [Fact]
        public void MetadataParseTest()
        {
            Console.WriteLine("FunctionCallParseTest()");
            Parser p = new Parser();
            GenericExpressionNode tree;
            bool fExceptionCaught = false;
            try
            {
                tree = p.Parse("%(foo) == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'a.cs;b.cs' == %(foo)", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'%(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'otherstuff%(foo)' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("'%(foo)otherstuff' == 'a.cs;b.cs'", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            fExceptionCaught = false;
            try
            {
                tree = p.Parse("somefunction(%(foo), 'otherstuff')", ParserOptions.AllowProperties | ParserOptions.AllowItemLists, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);
        }

        /// <summary>
        /// </summary>
        [Fact]
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
                tree = p.Parse("'a more complex' == 'asdf", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Note no close quote ----------------------------------------------------V
                tree = p.Parse("(($(foo) <= 5 and $(bar) >= 15) and $(baz) == 'simple string) and 'a more complex string' != $(quux)", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("($(foo) == 'simple string') $(bar)", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("=='x'", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("==", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse(">", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("true!=false==", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);

            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("true!=false==true", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);
            try
            {
                fExceptionCaught = false;
                // Correct tokens, but bad parse -----------V
                tree = p.Parse("1==(2", ParserOptions.AllowAll, _elementLocation);
            }
            catch (InvalidProjectFileException e)
            {
                Console.WriteLine(e.BaseMessage);
                fExceptionCaught = true;
            }
            Assert.True(fExceptionCaught);
        }

        /// <summary>
        /// This test verifies that we trigger warnings for expressions that
        /// could be incorrectly evaluated
        /// </summary>
        [Fact]
        public void VerifyWarningForOrder()
        {
            // Create a project file that has an expression
            MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 and $(b) == 2 or $(c) == 3`/>
                        </Target>
                    </Project>
                ");

            // Make sure the log contains the correct strings.
            Assert.Contains("MSB4130:", ml.FullLog); // "Need to warn for this expression - (a) == 1 and $(b) == 2 or $(c) == 3."

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 or $(b) == 2 and $(c) == 3`/>
                        </Target>
                    </Project>
                ");

            // Make sure the log contains the correct strings.
            Assert.Contains("MSB4130:", ml.FullLog); // "Need to warn for this expression - (a) == 1 or $(b) == 2 and $(c) == 3."

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4`/>
                        </Target>
                    </Project>
                ");

            // Make sure the log contains the correct strings.
            Assert.Contains("MSB4130:", ml.FullLog); // "Need to warn for this expression - ($(a) == 1 or $(b) == 2 and $(c) == 3) or $(d) == 4."
        }

        /// <summary>
        /// This test verifies that we don't trigger warnings for expressions that
        /// couldn't be incorrectly evaluated
        /// </summary>
        [Fact]
        public void VerifyNoWarningForOrder()
        {
            // Create a project file that has an expression
            MockLogger ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 and $(b) == 2 and $(c) == 3`/>
                        </Target>
                    </Project>
                ");

            // Make sure the log contains the correct strings.
            Assert.DoesNotContain("MSB4130:", ml.FullLog); // "No need to warn for this expression - (a) == 1 and $(b) == 2 and $(c) == 3."

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`$(a) == 1 or $(b) == 2 or $(c) == 3`/>
                        </Target>
                    </Project>
                ");

            // Make sure the log contains the correct strings.
            Assert.DoesNotContain("MSB4130:", ml.FullLog); // "No need to warn for this expression - (a) == 1 or $(b) == 2 or $(c) == 3."

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 and $(b) == 2) or $(c) == 3`/>
                        </Target>
                    </Project>
                ");

            // Make sure the log contains the correct strings.
            Assert.DoesNotContain("MSB4130:", ml.FullLog); // "No need to warn for this expression - ($(a) == 1 and $(b) == 2) or $(c) == 3."

            ml = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <Message Text=`expression 1 is true ` Condition=`($(a) == 1 or $(b) == 2) and $(c) == 3`/>
                        </Target>
                    </Project>
                ");

            // Make sure the log contains the correct strings.
            Assert.DoesNotContain("MSB4130:", ml.FullLog); // "No need to warn for this expression - ($(a) == 1 or $(b) == 2) and $(c) == 3."
        }

        // see https://github.com/dotnet/msbuild/issues/5436
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void SupportItemDefinationGroupInWhenOtherwise(bool context)
        {
            var projectContent = $@"
                <Project ToolsVersion= `msbuilddefaulttoolsversion` xmlns= `msbuildnamespace`>
                    <Choose>
                        <When Condition= `{context}`>
                            <PropertyGroup>
                                <Foo>bar</Foo>
                            </PropertyGroup>
                            <ItemGroup>
                                <A Include= `$(Foo)`>
                                    <n>n1</n>
                                </A>
                            </ItemGroup>
                            <ItemDefinitionGroup>
                                <A>
                                    <m>m1</m>
                                    <n>n2</n>
                                </A>
                            </ItemDefinitionGroup>
                        </When>
                        <Otherwise>
                            <PropertyGroup>
                                <Foo>bar</Foo>
                            </PropertyGroup>
                            <ItemGroup>
                                <A Include= `$(Foo)`>
                                    <n>n1</n>
                                </A>
                            </ItemGroup>
                            <ItemDefinitionGroup>
                                <A>
                                    <m>m2</m>
                                    <n>n2</n>
                                </A>
                            </ItemDefinitionGroup>
                        </Otherwise>
                    </Choose>
                </Project>
                ".Cleanup();


            var project = ObjectModelHelpers.CreateInMemoryProject(projectContent);

            var projectItem = project.GetItems("A").FirstOrDefault();
            Assert.Equal("bar", projectItem.EvaluatedInclude);

            var metadatam = projectItem.GetMetadata("m");
            if (context)
            {
                // Go to when
                Assert.Equal("m1", metadatam.EvaluatedValue);
            }
            else
            {
                // Go to Otherwise
                Assert.Equal("m2", metadatam.EvaluatedValue);
            }

            var metadatan = projectItem.GetMetadata("n");
            Assert.Equal("n1", metadatan.EvaluatedValue);
            Assert.Equal("n2", metadatan.Predecessor.EvaluatedValue);
        }
    }
}
