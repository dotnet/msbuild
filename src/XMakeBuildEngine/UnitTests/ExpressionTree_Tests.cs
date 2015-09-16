// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.IO;
using System.Xml;
using System.Collections.Specialized;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using System.Collections.Generic;
using Microsoft.Build.Shared;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class ExpressionTreeTest
    {
        /// <summary>
        /// </summary>
        [Fact]
        public void SimpleEvaluationTests()
        {
            Parser p = new Parser();
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>());

            AssertParseEvaluate(p, "true", expander, true);
            AssertParseEvaluate(p, "on", expander, true);
            AssertParseEvaluate(p, "yes", expander, true);
            AssertParseEvaluate(p, "false", expander, false);
            AssertParseEvaluate(p, "off", expander, false);
            AssertParseEvaluate(p, "no", expander, false);
        }

        /// <summary>
        /// A whole bunch of conditionals, that should be true, false, or error 
        /// (many coincidentally like existing QA tests) to give breadth coverage.
        /// Please add more cases as they arise.
        /// </summary>
        [Fact]
        public void EvaluateAVarietyOfExpressions()
        {
            string[] files = { "a", "a;b", "a'b", ";", "'" };

            try
            {
                foreach (string file in files)
                {
                    using (StreamWriter sw = File.CreateText(file)) {; }
                }

                Parser p = new Parser();
                GenericExpressionNode tree;

                ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();

                // Dummy project instance to own the items. 
                ProjectRootElement xml = ProjectRootElement.Create();
                xml.FullPath = @"c:\abc\foo.proj";

                ProjectInstance parentProject = new ProjectInstance(xml);

                itemBag.Add(new ProjectItemInstance(parentProject, "u", "a'b;c", parentProject.FullPath));
                itemBag.Add(new ProjectItemInstance(parentProject, "v", "a", parentProject.FullPath));
                itemBag.Add(new ProjectItemInstance(parentProject, "w", "1", parentProject.FullPath));
                itemBag.Add(new ProjectItemInstance(parentProject, "x", "true", parentProject.FullPath));
                itemBag.Add(new ProjectItemInstance(parentProject, "y", "xxx", parentProject.FullPath));
                itemBag.Add(new ProjectItemInstance(parentProject, "z", "xxx", parentProject.FullPath));
                itemBag.Add(new ProjectItemInstance(parentProject, "z", "yyy", parentProject.FullPath));

                PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();

                propertyBag.Set(ProjectPropertyInstance.Create("a", "no"));
                propertyBag.Set(ProjectPropertyInstance.Create("b", "true"));
                propertyBag.Set(ProjectPropertyInstance.Create("c", "1"));
                propertyBag.Set(ProjectPropertyInstance.Create("d", "xxx"));
                propertyBag.Set(ProjectPropertyInstance.Create("e", "xxx"));
                propertyBag.Set(ProjectPropertyInstance.Create("f", "1.9.5"));
                propertyBag.Set(ProjectPropertyInstance.Create("and", "and"));
                propertyBag.Set(ProjectPropertyInstance.Create("a_semi_b", "a;b"));
                propertyBag.Set(ProjectPropertyInstance.Create("a_apos_b", "a'b"));
                propertyBag.Set(ProjectPropertyInstance.Create("foo_apos_foo", "foo'foo"));
                propertyBag.Set(ProjectPropertyInstance.Create("a_escapedsemi_b", "a%3bb"));
                propertyBag.Set(ProjectPropertyInstance.Create("a_escapedapos_b", "a%27b"));
                propertyBag.Set(ProjectPropertyInstance.Create("has_trailing_slash", @"foo\"));

                Dictionary<string, string> metadataDictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                metadataDictionary["Culture"] = "french";
                StringMetadataTable itemMetadata = new StringMetadataTable(metadataDictionary);

                Expander<ProjectPropertyInstance, ProjectItemInstance> expander =
                    new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag, itemMetadata);

                string[] trueTests = {
                    "true or (SHOULDNOTEVALTHIS)", // short circuit
                    "(true and false) or true",
                    "false or true or false",
                    "(true) and (true)",
                    "false or !false",
                    "($(a) or true)",
                    "('$(c)'==1 and (!false))",
                    "@(z -> '%(filename).z', '$')=='xxx.z$yyy.z'",
                    "@(w -> '%(definingprojectname).barproj') == 'foo.barproj'",
                    "false or (false or (false or (false or (false or (true)))))",
                    "!(true and false)",
                    "$(and)=='and'",
                    "0x1==1.0",
                    "0xa==10",
                    "0<0.1",
                    "+4>-4",
                    "'-$(c)'==-1",
                    "$(a)==faLse",
                    "$(a)==oFF",
                    "$(a)==no",
                    "$(a)!=true",
                    "$(b)== True",
                    "$(b)==on",
                    "$(b)==yes",
                    "$(b)!=1",
                    "$(c)==1",
                    "$(d)=='xxx'",
                    "$(d)==$(e)",
                    "$(d)=='$(e)'",
                    "@(y)==$(d)",
                    "'@(z)'=='xxx;yyy'",
                    "$(a)==$(a)",
                    "'1'=='1'",
                    "'1'==1",
                    "1\n==1",
                    "1\t==\t\r\n1",
                    "123=='0123.0'",
                    "123==123",
                    "123==0123",
                    "123==0123.0",
                    "123!=0123.01",
                    "1.2.3<=1.2.3.0",
                    "12.23.34==12.23.34",
                    "0.8.0.0<8.0.0",
                    "1.1.2>1.0.1.2",
                    "8.1>8.0.16.23",
                    "8.0.0>=8",
                    "6<=6.0.0.1",
                    "7>6.8.2",
                    "4<5.9.9135.4",
                    "3!=3.0.0",
                    "1.2.3.4.5.6.7==1.2.3.4.5.6.7",
                    "00==0",
                    "0==0.0",
                    "1\n\t==1",
                    "+4==4",
                    "44==+44.0 and -44==-44.0",
                    "false==no",
                    "true==yes",
                    "true==!false",
                    "yes!=no",
                    "false!=1",
                    "$(c)>0",
                    "!$(a)",
                    "$(b)",
                    "($(d)==$(e))",
                    "!true==false",
                    "a_a==a_a",
                    "a_a=='a_a'",
                    "_a== _a",
                    "@(y -> '%(filename)')=='xxx'",
                    "@(z -> '%(filename)', '!')=='xxx!yyy'",
                    "'xxx!yyy'==@(z -> '%(filename)', '!')",
                    "'$(a)'==(false)",
                    "('$(a)'==(false))",
                    "1>0",
                    "2<=2",
                    "2<=3",
                    "1>=1",
                    "1>=-1",
                    "-1==-1",
                    "-1  <  0",
                    "(1==1)and('a'=='a')",
                    "(true) and ($(a)==off)",
                    "(true) and ($(d)==xxx)",
                    "(false)     or($(d)==xxx)",
                    "!(false)and!(false)",
                    "'and'=='AND'",
                    "$(d)=='XxX'",
                    "true or true or false",
                    "false or true or !true or'1'",
                    "$(a) or $(b)",
                    "$(a) or true",
                    "!!true",
                    "'$(e)1@(y)'=='xxx1xxx'",
                    "0x11==17",
                    "0x01a==26",
                    "0xa==0x0A",
                    "@(x)",
                    "'%77'=='w'",
                    "'%zz'=='%zz'",
                    "true or 1",
                    "true==!false",
                    "(!(true))=='off'",
                    "@(w)>0",
                    "1<=@(w)",
                    "%(culture)=='FRENCH'",
                    "'%(culture) fries' == 'FRENCH FRIES' ",
                    @"'%(HintPath)' == ''",
                    @"%(HintPath) != 'c:\myassemblies\foo.dll'",
                    "exists('a')",
                    "exists(a)",
                    "exists('a%3bb')", /* semicolon */
                    "exists('a%27b')", /* apostrophe */
                    "exists($(a_escapedsemi_b))",
                    "exists('$(a_escapedsemi_b)')",
                    "exists($(a_escapedapos_b))",
                    "exists('$(a_escapedapos_b)')",
                    "exists($(a_apos_b))",
                    "exists('$(a_apos_b)')",
                    "exists(@(v))",
                    "exists('@(v)')",
                    "exists('%3b')",
                    "exists('%27')",
                    "exists('@(v);@(nonexistent)')",
                    @"HASTRAILINGSLASH('foo\')",
                    @"!HasTrailingSlash('foo')",
                    @"HasTrailingSlash('foo/')",
                    @"HasTrailingSlash($(has_trailing_slash))",
                    "'59264.59264' == '59264.59264'",
                    "1" + new String('0', 500) + "==" + "1" + new String('0', 500), /* too big for double, eval as string */
                    "'1" + new String('0', 500) + "'=='" + "1" + new String('0', 500) + "'" /* too big for double, eval as string */
                };

                string[] falseTests = {
                    "false and SHOULDNOTEVALTHIS", // short circuit
                    "$(a)!=no",
                    "$(b)==1.1",
                    "$(c)==$(a)",
                    "$(d)!=$(e)",
                    "!$(b)",
                    "false or false or false",
                    "false and !((true and false))",
                    "on and off",
                    "(true) and (false)",
                    "false or (false or (false or (false or (false or (false)))))",
                    "!$(b)and true",
                    "1==a",
                    "!($(d)==$(e))",
                    "$(a) and true",
                    "true==1",
                    "false==0",
                    "(!(true))=='x'",
                    "oops==false",
                    "oops==!false",
                    "%(culture) == 'english'",
                    "'%(culture) fries' == 'english fries' ",
                    @"'%(HintPath)' == 'c:\myassemblies\foo.dll'",
                    @"%(HintPath) == 'c:\myassemblies\foo.dll'",
                    "exists('')",
                    "exists(' ')",
                    "exists($(nonexistent))",  // DDB #141195
                    "exists('$(nonexistent)')",  // DDB #141195
                    "exists(@(nonexistent))",  // DDB #141195
                    "exists('@(nonexistent)')",  // DDB #141195
                    "exists('\t')",
                    "exists('@(u)')",
                    "exists('$(foo_apos_foo)')",
                    "!exists('a')",
                    "!!!exists(a)",
                    "exists('|||||')",
                    @"hastrailingslash('foo')",
                    @"hastrailingslash('')",
                    @"HasTrailingSlash($(nonexistent))",
                    "'59264.59264' == '59264.59265'",
                    "1.2.0==1.2",
                    "$(f)!=$(f)",
                    "1.3.5.8>1.3.6.8",
                    "0.8.0.0>=1.0",
                    "8.0.0<=8.0",
                    "8.1.2<8",
                    "1" + new String('0', 500) + "==2", /* too big for double, eval as string */
                    "'1" + new String('0', 500) + "'=='2'", /* too big for double, eval as string */
                    "'1" + new String('0', 500) + "'=='01" + new String('0', 500) + "'" /* too big for double, eval as string */
                };

                string[] errorTests = {
                    "$",
                    "$(",
                    "$()",
                    "@",
                    "@(",
                    "@()",
                    "%",
                    "%(",
                    "%()",
                    "exists",
                    "exists(",
                    "exists()",
                    "exists( )",
                    "exists(,)",
                    "@(x->'",
                    "@(x->''",
                    "@(x-",
                    "@(x->'x','",
                    "@(x->'x',''",
                    "@(x->'x','')",
                    "-1>x",
                    "%00",
                    "\n",
                    "\t",
                    "+-1==1",
                    "1==-+1",
                    "1==+0xa",
                    "!$(c)",
                    "'a'==('a'=='a')",
                    "'a'!=('a'=='a')",
                    "('a'=='a')!=a",
                    "('a'=='a')==a",
                    "!'x'",
                    "!'$(d)'",
                    "ab#==ab#",
                    "#!=#",
                    "$(d)$(e)=='xxxxxx'",
                    "1=1=1",
                    "'a'=='a'=='a'",
                    "1 > 'x'",
                    "x1<=1",
                    "1<=x",
                    "1>x",
                    "x<x",
                    "@(x)<x",
                    "x>x",
                    "x>=x",
                    "x<=x",
                    "x>1",
                    "x>=1",
                    "1>=x",
                    "@(y)<=1",
                    "1<=@(z)",
                    "1>$(d)",
                    "$(c)@(y)>1",
                    "'$(c)@(y)'>1",
                    "$(d)>=1",
                    "1>=$(b)",
                    "1> =0",
                    "or true",
                    "1 and",
                    "and",
                    "or",
                    "not",
                    "not true",
                    "()",
                    "(a)",
                    "!",
                    "or=or",
                    "1==",
                    "1= =1",
                    "=",
                    "'true",
                    "'false''",
                    "'a'=='a",
                    "('a'=='a'",
                    "('a'=='a'))",
                    "'a'=='a')",
                    "!and",
                    "@(a)@(x)!=1",
                    "@(a) @(x)!=1",
                    "$(a==off",
                    "=='x'",
                    "==",
                    "!0",
                    ">",
                    "true!=false==",
                    "true!=false==true",
                    "()",
                    "!1",
                    "1==(2",
                    "$(a)==x>1==2",
                    "'a'>'a'",
                    "0",
                    "$(a)>0",
                    "!$(e)",
                    "1<=1<=1",
                    "true $(and) true",
                    "--1==1",
                    "$(and)==and",
                    "!@#$%^&*",
                    "-($(c))==-1",
                    "a==b or $(d)",
                    "false or $()",
                    "$(d) or true",
                    "%(Culture) or true",
                    "@(nonexistent) and true",
                    "$(nonexistent) and true",
                    "@(nonexistent)",
                    "$(nonexistent)",
                    "@(z) and true",
                    "@() and true",
                    "@()",
                    "$()",
                    "1",
                    "1 or true",
                    "false or 1",
                    "1 and true",
                    "true and 1",
                    "!1",
                    "false or !1",
                    "false or 'aa'",
                    "true blah",
                    "existsX",
                    "!",
                    "nonexistentfunction('xyz')",
                    "exists('a;b')", /* non scalar */
                    "exists(@(z))",
                    "exists('@(z)')",
                    "exists($(a_semi_b))",
                    "exists('$(a_semi_b)')",
                    "exists(@(v)x)",
                    "exists(@(v)$(nonexistent))",
                    "exists('@(v)$(a)')",
                    "exists(|||||)",
                    "HasTrailingSlash(a,'b')",
                    "HasTrailingSlash(,,)",
                    "1.2.3==1,2,3"
                };

                for (int i = 0; i < trueTests.GetLength(0); i++)
                {
                    tree = p.Parse(trueTests[i], ParserOptions.AllowAll, ElementLocation.EmptyLocation);
                    ConditionEvaluator.IConditionEvaluationState state =
                        new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>
                            (
                                trueTests[i],
                                expander,
                                ExpanderOptions.ExpandAll,
                                null,
                                Directory.GetCurrentDirectory(),
                                ElementLocation.EmptyLocation
                            );

                    Assert.True(tree.Evaluate(state), "expected true from '" + trueTests[i] + "'");
                }

                for (int i = 0; i < falseTests.GetLength(0); i++)
                {
                    tree = p.Parse(falseTests[i], ParserOptions.AllowAll, ElementLocation.EmptyLocation);
                    ConditionEvaluator.IConditionEvaluationState state =
                        new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>
                            (
                                falseTests[i],
                                expander,
                                ExpanderOptions.ExpandAll,
                                null,
                                Directory.GetCurrentDirectory(),
                                ElementLocation.EmptyLocation
                            );

                    Assert.False(tree.Evaluate(state), "expected false from '" + falseTests[i] + "' and got true");
                }

                for (int i = 0; i < errorTests.GetLength(0); i++)
                {
                    // It seems that if an expression is invalid,
                    //      - Parse may throw, or
                    //      - Evaluate may throw, or
                    //      - Evaluate may return false causing its caller EvaluateCondition to throw
                    bool success = true;
                    bool caughtException = false;
                    bool value;
                    try
                    {
                        tree = p.Parse(errorTests[i], ParserOptions.AllowAll, ElementLocation.EmptyLocation);
                        ConditionEvaluator.IConditionEvaluationState state =
                            new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>
                                (
                                    errorTests[i],
                                    expander,
                                    ExpanderOptions.ExpandAll,
                                    null,
                                    Directory.GetCurrentDirectory(),
                                    ElementLocation.EmptyLocation
                                );

                        value = tree.Evaluate(state);
                        if (!success) Console.WriteLine(errorTests[i] + " caused Evaluate to return false");
                    }
                    catch (InvalidProjectFileException ex)
                    {
                        Console.WriteLine(errorTests[i] + " caused '" + ex.Message + "'");
                        caughtException = true;
                    }
                    Assert.True((success == false || caughtException == true), "expected '" + errorTests[i] + "' to not parse or not be evaluated");
                }
            }
            finally
            {
                foreach (string file in files)
                {
                    if (File.Exists(file)) File.Delete(file);
                }
            }
        }


        /// <summary>
        /// </summary>
        [Fact]
        public void EqualityTests()
        {
            Parser p = new Parser();
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>());

            AssertParseEvaluate(p, "true == on", expander, true);
            AssertParseEvaluate(p, "TrUe == On", expander, true);
            AssertParseEvaluate(p, "true != false", expander, true);
            AssertParseEvaluate(p, "true==!false", expander, true);
            AssertParseEvaluate(p, "4 != 5", expander, true);
            AssertParseEvaluate(p, "-4 < 4", expander, true);
            AssertParseEvaluate(p, "5 == +5", expander, true);
            AssertParseEvaluate(p, "4 == 4.0", expander, true);
            AssertParseEvaluate(p, "4 == 4.0", expander, true);
            AssertParseEvaluate(p, ".45 == '.45'", expander, true);
            AssertParseEvaluate(p, "4 == '4'", expander, true);
            AssertParseEvaluate(p, "'0' == '4'", expander, false);
            AssertParseEvaluate(p, "4 == 0x0004", expander, true);
            AssertParseEvaluate(p, "0.0 == 0", expander, true);
            AssertParseEvaluate(p, "simplestring == 'simplestring'", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void RelationalTests()
        {
            Parser p = new Parser();
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>());

            AssertParseEvaluate(p, "1234 < 1235", expander, true);
            AssertParseEvaluate(p, "1234 <= 1235", expander, true);
            AssertParseEvaluate(p, "1235 < 1235", expander, false);
            AssertParseEvaluate(p, "1234 <= 1234", expander, true);
            AssertParseEvaluate(p, "1235 <= 1234", expander, false);
            AssertParseEvaluate(p, "1235 > 1234", expander, true);
            AssertParseEvaluate(p, "1235 >= 1235", expander, true);
            AssertParseEvaluate(p, "1235 >= 1234", expander, true);
            AssertParseEvaluate(p, "0.0==0", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void AndandOrTests()
        {
            Parser p = new Parser();
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>());

            AssertParseEvaluate(p, "true == on and 1234 < 1235", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void FunctionTests()
        {
            Parser p = new Parser();
            GenericExpressionNode tree;
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), new ItemDictionary<ProjectItemInstance>());
            expander.Metadata = new StringMetadataTable(null);
            bool value;

            string fileThatMustAlwaysExist = FileUtilities.GetTemporaryFile();
            File.WriteAllText(fileThatMustAlwaysExist, "foo");
            string command = "Exists('" + fileThatMustAlwaysExist + "')";
            tree = p.Parse(command, ParserOptions.AllowAll, ElementLocation.EmptyLocation);

            ConditionEvaluator.IConditionEvaluationState state =
                            new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>
                                (
                                    command,
                                    expander,
                                    ExpanderOptions.ExpandAll,
                                    null,
                                    Directory.GetCurrentDirectory(),
                                    ElementLocation.EmptyLocation
                                );

            value = tree.Evaluate(state);
            Assert.True(value);

            if (File.Exists(fileThatMustAlwaysExist))
            {
                File.Delete(fileThatMustAlwaysExist);
            }

            AssertParseEvaluate(p, "Exists('c:\\IShouldntExist.sys')", expander, false);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void PropertyTests()
        {
            Parser p = new Parser();

            var propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
            propertyBag.Set(ProjectPropertyInstance.Create("foo", "true"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "yes"));
            propertyBag.Set(ProjectPropertyInstance.Create("one", "1"));
            propertyBag.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
            propertyBag.Set(ProjectPropertyInstance.Create("two", "2"));
            propertyBag.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
            propertyBag.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
            propertyBag.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("x86", "x86"));
            propertyBag.Set(ProjectPropertyInstance.Create("no", "no"));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, new ItemDictionary<ProjectItemInstance>());
            AssertParseEvaluate(p, "$(foo)", expander, true);
            AssertParseEvaluate(p, "!$(foo)", expander, false);
            // Test properties with strings
            AssertParseEvaluate(p, "$(simple) == 'simplestring'", expander, true);
            AssertParseEvaluate(p, "'simplestring' == $(simple)", expander, true);
            AssertParseEvaluate(p, "'foo' != $(simple)", expander, true);
            AssertParseEvaluate(p, "'simplestring' == '$(simple)'", expander, true);
            AssertParseEvaluate(p, "$(simple) == simplestring", expander, true);
            AssertParseEvaluate(p, "$(x86) == x86", expander, true);
            AssertParseEvaluate(p, "$(x86)==x86", expander, true);
            AssertParseEvaluate(p, "x86==$(x86)", expander, true);
            AssertParseEvaluate(p, "$(c1) == $(c2)", expander, true);
            AssertParseEvaluate(p, "'$(c1)' == $(c2)", expander, true);
            AssertParseEvaluate(p, "$(c1) != $(simple)", expander, true);
            AssertParseEvaluate(p, "$(c1) == $(c2)", expander, true);
            // Test properties with numbers
            AssertParseEvaluate(p, "$(one) == $(onepointzero)", expander, true);
            AssertParseEvaluate(p, "$(one) <= $(two)", expander, true);
            AssertParseEvaluate(p, "$(two) > $(onepointzero)", expander, true);
            AssertParseEvaluate(p, "$(one) != $(two)", expander, true);
            AssertParseEvaluate(p, "'$(no)'==false", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ItemListTests()
        {
            Parser p = new Parser();

            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Boolean", "true", parentProject.FullPath));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), itemBag);

            AssertParseEvaluate(p, "@(Compile) == 'foo.cs;bar.cs;baz.cs'", expander, true);
            AssertParseEvaluate(p, "@(Compile,' ') == 'foo.cs bar.cs baz.cs'", expander, true);
            AssertParseEvaluate(p, "@(Compile,'') == 'foo.csbar.csbaz.cs'", expander, true);
            AssertParseEvaluate(p, "@(Compile->'%(Filename)') == 'foo;bar;baz'", expander, true);
            AssertParseEvaluate(p, "@(Compile -> 'temp\\%(Filename).xml', ' ') == 'temp\\foo.xml temp\\bar.xml temp\\baz.xml'", expander, true);
            AssertParseEvaluate(p, "@(Compile->'', '') == ''", expander, true);
            AssertParseEvaluate(p, "@(Compile->'') == ';;'", expander, true);
            AssertParseEvaluate(p, "@(Compile->'%(Nonexistent)', '') == ''", expander, true);
            AssertParseEvaluate(p, "@(Compile->'%(Nonexistent)') == ';;'", expander, true);
            AssertParseEvaluate(p, "@(Boolean)", expander, true);
            AssertParseEvaluate(p, "@(Boolean) == true", expander, true);
            AssertParseEvaluate(p, "'@(Empty, ';')' == ''", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void StringExpansionTests()
        {
            Parser p = new Parser();

            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
            propertyBag.Set(ProjectPropertyInstance.Create("foo", "true"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "yes"));
            propertyBag.Set(ProjectPropertyInstance.Create("one", "1"));
            propertyBag.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
            propertyBag.Set(ProjectPropertyInstance.Create("two", "2"));
            propertyBag.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
            propertyBag.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
            propertyBag.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("TestQuote", "Contains'Quote'"));
            propertyBag.Set(ProjectPropertyInstance.Create("AnotherTestQuote", "Here's Johnny!"));
            propertyBag.Set(ProjectPropertyInstance.Create("Atsign", "Test the @ replacement"));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag);

            AssertParseEvaluate(p, "'simplestring: true foo.cs;bar.cs;baz.cs' == '$(simple): $(foo) @(compile)'", expander, true);
            AssertParseEvaluate(p, "'$(c1) $(c2)' == 'Another (complex) one. Another (complex) one.'", expander, true);
            AssertParseEvaluate(p, "'CONTAINS%27QUOTE%27' == '$(TestQuote)'", expander, true);
            AssertParseEvaluate(p, "'Here%27s Johnny!' == '$(AnotherTestQuote)'", expander, true);
            AssertParseEvaluate(p, "'Test the %40 replacement' == $(Atsign)", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ComplexTests()
        {
            Parser p = new Parser();
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
            propertyBag.Set(ProjectPropertyInstance.Create("foo", "true"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "yes"));
            propertyBag.Set(ProjectPropertyInstance.Create("one", "1"));
            propertyBag.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
            propertyBag.Set(ProjectPropertyInstance.Create("two", "2"));
            propertyBag.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
            propertyBag.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
            propertyBag.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag);

            AssertParseEvaluate(p, "(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", expander, true);
            AssertParseEvaluate(p, "(($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1", expander, true);
            AssertParseEvaluate(p, "!((($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1)", expander, false);
        }


        /// <summary>
        /// Make sure when a non number is used in an expression which expects a numeric value that a error is emitted.
        /// </summary>
        [Fact]
        public void InvalidItemInConditionEvaluation()
        {
            Parser p = new Parser();
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "a", parentProject.FullPath));

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag);

            AssertParseEvaluateThrow(p, "@(Compile) > 0", expander, null);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void OldSyntaxTests()
        {
            Parser p = new Parser();
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();

            propertyBag.Set(ProjectPropertyInstance.Create("foo", "true"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "yes"));
            propertyBag.Set(ProjectPropertyInstance.Create("one", "1"));
            propertyBag.Set(ProjectPropertyInstance.Create("onepointzero", "1.0"));
            propertyBag.Set(ProjectPropertyInstance.Create("two", "2"));
            propertyBag.Set(ProjectPropertyInstance.Create("simple", "simplestring"));
            propertyBag.Set(ProjectPropertyInstance.Create("complex", "This is a complex string"));
            propertyBag.Set(ProjectPropertyInstance.Create("c1", "Another (complex) one."));
            propertyBag.Set(ProjectPropertyInstance.Create("c2", "Another (complex) one."));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, itemBag);

            AssertParseEvaluate(p, "(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", expander, true);
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void ConditionedPropertyUpdateTests()
        {
            Parser p = new Parser();
            ProjectInstance parentProject = new ProjectInstance(ProjectRootElement.Create());
            ItemDictionary<ProjectItemInstance> itemBag = new ItemDictionary<ProjectItemInstance>();
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "foo.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "bar.cs", parentProject.FullPath));
            itemBag.Add(new ProjectItemInstance(parentProject, "Compile", "baz.cs", parentProject.FullPath));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>(), itemBag);
            Dictionary<string, List<string>> conditionedProperties = new Dictionary<string, List<string>>();
            ConditionEvaluator.IConditionEvaluationState state =
                               new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>
                                   (
                                       String.Empty,
                                       expander,
                                       ExpanderOptions.ExpandAll,
                                       conditionedProperties,
                                       Directory.GetCurrentDirectory(),
                                       ElementLocation.EmptyLocation
                                   );

            List<string> properties = null;

            AssertParseEvaluate(p, "'0' == '1'", expander, false, state);
            Assert.Equal(0, conditionedProperties.Count);

            AssertParseEvaluate(p, "$(foo) == foo", expander, false, state);
            Assert.Equal(1, conditionedProperties.Count);
            properties = conditionedProperties["foo"];
            Assert.Equal(1, properties.Count);

            AssertParseEvaluate(p, "'$(foo)' != 'bar'", expander, true, state);
            Assert.Equal(1, conditionedProperties.Count);
            properties = conditionedProperties["foo"];
            Assert.Equal(2, properties.Count);

            AssertParseEvaluate(p, "'$(branch)|$(build)|$(platform)' == 'lab22dev|debug|x86'", expander, false, state);
            Assert.Equal(4, conditionedProperties.Count);
            properties = conditionedProperties["foo"];
            Assert.Equal(2, properties.Count);
            properties = conditionedProperties["branch"];
            Assert.Equal(1, properties.Count);
            properties = conditionedProperties["build"];
            Assert.Equal(1, properties.Count);
            properties = conditionedProperties["platform"];
            Assert.Equal(1, properties.Count);

            AssertParseEvaluate(p, "'$(branch)|$(build)|$(platform)' == 'lab21|debug|x86'", expander, false, state);
            Assert.Equal(4, conditionedProperties.Count);
            properties = conditionedProperties["foo"];
            Assert.Equal(2, properties.Count);
            properties = conditionedProperties["branch"];
            Assert.Equal(2, properties.Count);
            properties = conditionedProperties["build"];
            Assert.Equal(1, properties.Count);
            properties = conditionedProperties["platform"];
            Assert.Equal(1, properties.Count);

            AssertParseEvaluate(p, "'$(branch)|$(build)|$(platform)' == 'lab23|retail|ia64'", expander, false, state);
            Assert.Equal(4, conditionedProperties.Count);
            properties = conditionedProperties["foo"];
            Assert.Equal(2, properties.Count);
            properties = conditionedProperties["branch"];
            Assert.Equal(3, properties.Count);
            properties = conditionedProperties["build"];
            Assert.Equal(2, properties.Count);
            properties = conditionedProperties["platform"];
            Assert.Equal(2, properties.Count);
            DumpDictionary(conditionedProperties);
        }

        private static void DumpDictionary(Dictionary<string, List<string>> propertyDictionary)
        {
            foreach (KeyValuePair<string, List<String>> entry in propertyDictionary)
            {
                Console.Write("  {0}:\t", entry.Key);

                List<String> properties = entry.Value;

                foreach (string property in properties)
                {
                    Console.Write("{0}, ", property);
                }
                Console.WriteLine();
            }
        }

        /// <summary>
        /// </summary>
        [Fact]
        public void NotTests()
        {
            Console.WriteLine("NegationParseTest()");
            Parser p = new Parser();

            PropertyDictionary<ProjectPropertyInstance> propertyBag = new PropertyDictionary<ProjectPropertyInstance>();
            propertyBag.Set(ProjectPropertyInstance.Create("foo", "4"));
            propertyBag.Set(ProjectPropertyInstance.Create("bar", "32"));

            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(propertyBag, new ItemDictionary<ProjectItemInstance>());

            AssertParseEvaluate(p, "!true", expander, false);
            AssertParseEvaluate(p, "!(true)", expander, false);
            AssertParseEvaluate(p, "!($(foo) <= 5)", expander, false);
            AssertParseEvaluate(p, "!($(foo) <= 5 and $(bar) >= 15)", expander, false);
        }

        private void AssertParseEvaluate(Parser p, string expression, Expander<ProjectPropertyInstance, ProjectItemInstance> expander, bool expected)
        {
            AssertParseEvaluate(p, expression, expander, expected, null);
        }

        private void AssertParseEvaluate(Parser p, string expression, Expander<ProjectPropertyInstance, ProjectItemInstance> expander, bool expected, ConditionEvaluator.IConditionEvaluationState state)
        {
            if (expander.Metadata == null)
            {
                expander.Metadata = new StringMetadataTable(null);
            }

            GenericExpressionNode tree = p.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);

            if (state == null)
            {
                state =
                new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>
                    (
                        String.Empty,
                        expander,
                        ExpanderOptions.ExpandAll,
                        null,
                        Directory.GetCurrentDirectory(),
                        ElementLocation.EmptyLocation
                    );
            }

            bool result = tree.Evaluate(state);
            Assert.Equal(expected, result);
        }


        private void AssertParseEvaluateThrow(Parser p, string expression, Expander<ProjectPropertyInstance, ProjectItemInstance> expander)
        {
            AssertParseEvaluateThrow(p, expression, expander, null);
        }

        private void AssertParseEvaluateThrow(Parser p, string expression, Expander<ProjectPropertyInstance, ProjectItemInstance> expander, ConditionEvaluator.IConditionEvaluationState state)
        {
            bool fExceptionCaught;

            if (expander.Metadata == null)
            {
                expander.Metadata = new StringMetadataTable(null);
            }

            try
            {
                fExceptionCaught = false;
                GenericExpressionNode tree = p.Parse(expression, ParserOptions.AllowAll, MockElementLocation.Instance);
                if (state == null)
                {
                    state =
                    new ConditionEvaluator.ConditionEvaluationState<ProjectPropertyInstance, ProjectItemInstance>
                        (
                            String.Empty,
                            expander,
                            ExpanderOptions.ExpandAll,
                            null,
                            Directory.GetCurrentDirectory(),
                            ElementLocation.EmptyLocation
                        );
                }
                tree.Evaluate(state);
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
            Parser p = new Parser();
            Expander<ProjectPropertyInstance, ProjectItemInstance> expander = new Expander<ProjectPropertyInstance, ProjectItemInstance>(new PropertyDictionary<ProjectPropertyInstance>());

            AssertParseEvaluateThrow(p, "foo", expander);
            AssertParseEvaluateThrow(p, "0", expander);
            AssertParseEvaluateThrow(p, "$(platform) == xx > 1==2", expander);
            AssertParseEvaluateThrow(p, "!0", expander);
            AssertParseEvaluateThrow(p, ">", expander);
            AssertParseEvaluateThrow(p, "true!=false==", expander);
            AssertParseEvaluateThrow(p, "()", expander);
            AssertParseEvaluateThrow(p, "!1", expander);
            AssertParseEvaluateThrow(p, "true!=false==true", expander);
            AssertParseEvaluateThrow(p, "'a'>'a'", expander);
            AssertParseEvaluateThrow(p, "=='x'", expander);
            AssertParseEvaluateThrow(p, "==", expander);
            AssertParseEvaluateThrow(p, "1==(2", expander);
            AssertParseEvaluateThrow(p, "'a'==('a'=='a')", expander);
            AssertParseEvaluateThrow(p, "true == on and ''", expander);
            AssertParseEvaluateThrow(p, "'' or 'true'", expander);
        }
    }
}



