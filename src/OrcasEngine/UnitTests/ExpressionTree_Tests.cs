// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.IO;
using System.Xml;
using System.Collections.Specialized;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using System.Collections.Generic;


namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ExpressionTreeTest
    {
        private XmlAttribute dummyAttribute; 
        private XmlAttribute DummyAttribute
        {
            get
            {
                if (dummyAttribute == null)
                {
                    dummyAttribute = (new XmlDocument()).CreateAttribute("foo");
                }

                return dummyAttribute;
            }
        }

        private void AssertParseEvaluate(Parser p, string expression, ConditionEvaluationState state, bool expected)
        {
            state.parsedCondition = expression;
            GenericExpressionNode expressionTree = p.Parse(expression, DummyAttribute, ParserOptions.AllowAll);
            Assertion.AssertEquals(expected, expressionTree.Evaluate(state));
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void SimpleEvaluationTests()
        {
            Parser p = new Parser();
            Expander expander = new Expander(new BuildPropertyGroup());
            Hashtable conditionedProperties = null;
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "true", state, true);
            AssertParseEvaluate(p, "on", state, true);
            AssertParseEvaluate(p, "yes", state, true);
            AssertParseEvaluate(p, "false", state, false);
            AssertParseEvaluate(p, "off", state, false);
            AssertParseEvaluate(p, "no", state, false);
        }

        [Test]
        public void EvaluatedAVarietyOfExpressionsWithProjectPerThreadProjectDirectoryNull()
        {
            string original = null;
            try
            {
                original = Project.PerThreadProjectDirectory;
                Project.PerThreadProjectDirectory = null;
                EvaluateAVarietyOfExpressions();
            }
            finally
            {
                Project.PerThreadProjectDirectory = original;
            }
        }

        [Test]
        public void EvaluatedAVarietyOfExpressionsWithProjectPerThreadProjectDirectoryEmpty()
        {
            string original = null;
            try
            {
                original = Project.PerThreadProjectDirectory;
                Project.PerThreadProjectDirectory = String.Empty;
                EvaluateAVarietyOfExpressions();
            }
            finally
            {
                Project.PerThreadProjectDirectory = original;
            }
        }

        [Test]
        public void EvaluatedAVarietyOfExpressionsWithProjectPerThreadProjectDirectoryNotNull()
        {
            string original = null;
            try
            {
                original = Project.PerThreadProjectDirectory;
                Project.PerThreadProjectDirectory = Directory.GetCurrentDirectory();
                EvaluateAVarietyOfExpressions();
            }
            finally
            {
                Project.PerThreadProjectDirectory = original;
            }
        }

        /// <summary>
        /// A whole bunch of conditionals, that should be true, false, or error 
        /// (many coincidentally like existing QA tests) to give breadth coverage.
        /// Please add more cases as they arise.
        /// </summary>
        /// <owner>danmose</owner>
        private void EvaluateAVarietyOfExpressions()
        {
            string[] files = { "a", "a;b", "a'b", ";", "'" };

            try
            {
                foreach (string file in files)
                {
                    using (StreamWriter sw = File.CreateText(file)) { ; }
                }

                Parser p = new Parser();
                GenericExpressionNode tree;

                BuildItemGroup itemGroupU = new BuildItemGroup();
                BuildItemGroup itemGroupV = new BuildItemGroup();
                BuildItemGroup itemGroupW = new BuildItemGroup();
                BuildItemGroup itemGroupX = new BuildItemGroup();
                BuildItemGroup itemGroupY = new BuildItemGroup();
                BuildItemGroup itemGroupZ = new BuildItemGroup();
                itemGroupU.AddItem(new BuildItem("u", "a'b;c"));
                itemGroupV.AddItem(new BuildItem("w", "a"));
                itemGroupW.AddItem(new BuildItem("w", "1"));
                itemGroupX.AddItem(new BuildItem("x", "true"));
                itemGroupY.AddItem(new BuildItem("y", "xxx"));
                itemGroupZ.AddItem(new BuildItem("z", "xxx"));
                itemGroupZ.AddItem(new BuildItem("z", "yyy"));
                Hashtable itemBag = new Hashtable(StringComparer.OrdinalIgnoreCase);
                itemBag["u"] = itemGroupU;
                itemBag["v"] = itemGroupV;
                itemBag["w"] = itemGroupW;
                itemBag["x"] = itemGroupX;
                itemBag["y"] = itemGroupY;
                itemBag["z"] = itemGroupZ;

                BuildPropertyGroup propertyBag = new BuildPropertyGroup();
                propertyBag.SetProperty("a", "no");
                propertyBag.SetProperty("b", "true");
                propertyBag.SetProperty("c", "1");
                propertyBag.SetProperty("d", "xxx");
                propertyBag.SetProperty("e", "xxx");
                propertyBag.SetProperty("and", "and");
                propertyBag.SetProperty("a_semi_b", "a;b");
                propertyBag.SetProperty("a_apos_b", "a'b");
                propertyBag.SetProperty("foo_apos_foo", "foo'foo");
                propertyBag.SetProperty("a_escapedsemi_b", "a%3bb");
                propertyBag.SetProperty("a_escapedapos_b", "a%27b");
                propertyBag.SetProperty("has_trailing_slash", @"foo\");

                Dictionary<string, string> itemMetadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                itemMetadata["Culture"] = "french";

                Expander expander = new Expander(new ReadOnlyLookup(itemBag, propertyBag), itemMetadata);

                string[] trueTests = {
                    "true or (SHOULDNOTEVALTHIS)", // short circuit
                    "(true and false) or true",
                    "false or true or false",
                    "(true) and (true)",
                    "false or !false",
                    "($(a) or true)",
                    "('$(c)'==1 and (!false))",
                    "@(z -> '%(filename).z', '$')=='xxx.z$yyy.z'",
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
                    @"hastrailingslash('foo')",
                    @"hastrailingslash('')",
                    @"HasTrailingSlash($(nonexistent))",
                    "'59264.59264' == '59264.59265'",
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
                    "HasTrailingSlash(a,'b')",
                    "HasTrailingSlash(,,)"
                };

                for (int i = 0; i < trueTests.GetLength(0); i++)
                {
                    tree = p.Parse(trueTests[i], DummyAttribute, ParserOptions.AllowAll);
                    ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, null, trueTests[i]);
                    Assertion.Assert("expected true from '" + trueTests[i] + "'", tree.Evaluate(state));
                }

                for (int i = 0; i < falseTests.GetLength(0); i++)
                {
                    tree = p.Parse(falseTests[i], DummyAttribute, ParserOptions.AllowAll);
                    ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, null, falseTests[i]);
                    Assertion.Assert("expected false from '" + falseTests[i] + "' and got true", !tree.Evaluate(state));
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
                        tree = p.Parse(errorTests[i], DummyAttribute, ParserOptions.AllowAll);
                        ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, null, errorTests[i]);
                        value = tree.Evaluate(state);
                        if (!success) Console.WriteLine(errorTests[i] + " caused Evaluate to return false");
                    }
                    catch (InvalidProjectFileException ex)
                    {
                        Console.WriteLine(errorTests[i] + " caused '" + ex.Message + "'");
                        caughtException = true;
                    }
                    Assertion.Assert("expected '" + errorTests[i] + "' to not parse or not be evaluated",
                        (success == false || caughtException == true));

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
        /// <owner>DavidLe</owner>
        [Test]
        public void EqualityTests()
        {
            Parser p = new Parser();
            Expander expander = new Expander(new BuildPropertyGroup());
            Hashtable conditionedProperties = null;
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "true == on", state, true);
            AssertParseEvaluate(p, "TrUe == On", state, true);
            AssertParseEvaluate(p, "true != false", state, true);
            AssertParseEvaluate(p, "true==!false", state, true);
            AssertParseEvaluate(p, "4 != 5", state, true);
            AssertParseEvaluate(p, "-4 < 4", state, true);
            AssertParseEvaluate(p, "5 == +5", state, true);
            AssertParseEvaluate(p, "4 == 4.0", state, true);
            AssertParseEvaluate(p, "4 == 4.0", state, true);
            AssertParseEvaluate(p, ".45 == '.45'", state, true);
            AssertParseEvaluate(p, "4 == '4'", state, true);
            AssertParseEvaluate(p, "'0' == '4'", state, false);
            AssertParseEvaluate(p, "4 == 0x0004", state, true);
            AssertParseEvaluate(p, "0.0 == 0", state, true);
            AssertParseEvaluate(p, "simplestring == 'simplestring'", state, true);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void RelationalTests()
        {
            Parser p = new Parser();
            Expander expander = new Expander(new BuildPropertyGroup());
            Hashtable conditionedProperties = null;
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "1234 < 1235", state, true);
            AssertParseEvaluate(p, "1234 <= 1235", state, true);
            AssertParseEvaluate(p, "1235 < 1235", state, false);
            AssertParseEvaluate(p, "1234 <= 1234", state, true);
            AssertParseEvaluate(p, "1235 <= 1234", state, false);
            AssertParseEvaluate(p, "1235 > 1234", state, true);
            AssertParseEvaluate(p, "1235 >= 1235", state, true);
            AssertParseEvaluate(p, "1235 >= 1234", state, true);
            AssertParseEvaluate(p, "0.0==0", state, true);}

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void AndandOrTests()
        {
            Parser p = new Parser();
            Expander expander = new Expander(new BuildPropertyGroup());
            Hashtable conditionedProperties = null;
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "true == on and 1234 < 1235", state, true);}

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void FunctionTests()
        {
            Parser p = new Parser();
            GenericExpressionNode tree;
            Expander expander = new Expander(new BuildPropertyGroup());
            Hashtable conditionedProperties = null;
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);
            bool value;

            string fileThatMustAlwaysExist = Path.GetTempFileName();
            File.WriteAllText(fileThatMustAlwaysExist, "foo");
            string command = "Exists('" + fileThatMustAlwaysExist + "')";
            tree = p.Parse(command, DummyAttribute, ParserOptions.AllowAll);
            value = tree.Evaluate(state);
            Assertion.Assert(value);

            if (File.Exists(fileThatMustAlwaysExist))
            {
                File.Delete(fileThatMustAlwaysExist);
            }

            AssertParseEvaluate(p, "Exists('c:\\IShouldntExist.sys')", state, false);
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void PropertyTests()
        {
            Parser p = new Parser();
            Hashtable conditionedProperties = null;

            BuildPropertyGroup propertyBag = new BuildPropertyGroup();
            propertyBag.SetProperty("foo", "true");
            propertyBag.SetProperty("bar", "yes");
            propertyBag.SetProperty("one", "1");
            propertyBag.SetProperty("onepointzero", "1.0");
            propertyBag.SetProperty("two", "2");
            propertyBag.SetProperty("simple", "simplestring");
            propertyBag.SetProperty("complex", "This is a complex string");
            propertyBag.SetProperty("c1", "Another (complex) one.");
            propertyBag.SetProperty("c2", "Another (complex) one.");
            propertyBag.SetProperty("x86", "x86");
            propertyBag.SetProperty("no", "no");

            Expander expander = new Expander(propertyBag);
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "$(foo)", state, true);
            AssertParseEvaluate(p, "!$(foo)", state, false);
            // Test properties with strings
            AssertParseEvaluate(p, "$(simple) == 'simplestring'", state, true);
            AssertParseEvaluate(p, "'simplestring' == $(simple)", state, true);
            AssertParseEvaluate(p, "'foobar' != $(simple)", state, true);
            AssertParseEvaluate(p, "'simplestring' == '$(simple)'", state, true);
            AssertParseEvaluate(p, "$(simple) == simplestring", state, true);
            AssertParseEvaluate(p, "$(x86) == x86", state, true);
            AssertParseEvaluate(p, "$(x86)==x86", state, true);
            AssertParseEvaluate(p, "x86==$(x86)", state, true);
            AssertParseEvaluate(p, "$(c1) == $(c2)", state, true);
            AssertParseEvaluate(p, "'$(c1)' == $(c2)", state, true);
            AssertParseEvaluate(p, "$(c1) != $(simple)", state, true);
            AssertParseEvaluate(p, "$(c1) == $(c2)", state, true);
            // Test properties with numbers
            AssertParseEvaluate(p, "$(one) == $(onepointzero)", state, true);
            AssertParseEvaluate(p, "$(one) <= $(two)", state, true);
            AssertParseEvaluate(p, "$(two) > $(onepointzero)", state, true);
            AssertParseEvaluate(p, "$(one) != $(two)", state, true);
            AssertParseEvaluate(p, "'$(no)'==false", state, true);}

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void ItemListTests()
        {
            Parser p = new Parser();
            Hashtable conditionedProperties = null;

            BuildItemGroup myCompileItemGroup = new BuildItemGroup();
            myCompileItemGroup.AddItem(new BuildItem("Compile", "foo.cs"));
            myCompileItemGroup.AddItem(new BuildItem("Compile", "bar.cs"));
            myCompileItemGroup.AddItem(new BuildItem("Compile", "baz.cs"));

            BuildItemGroup myBooleanItemGroup = new BuildItemGroup();
            myBooleanItemGroup.AddItem(new BuildItem("Boolean", "true"));

            Hashtable itemsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemsByType["Compile"] = myCompileItemGroup;
            itemsByType["Boolean"] = myBooleanItemGroup;

            Expander expander = new Expander(LookupHelpers.CreateLookup(itemsByType).ReadOnlyLookup);
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "@(Compile) == 'foo.cs;bar.cs;baz.cs'", state, true);
            AssertParseEvaluate(p, "@(Compile,' ') == 'foo.cs bar.cs baz.cs'", state, true);
            AssertParseEvaluate(p, "@(Compile,'') == 'foo.csbar.csbaz.cs'", state, true);
            AssertParseEvaluate(p, "@(Compile->'%(Filename)') == 'foo;bar;baz'", state, true);
            AssertParseEvaluate(p, "@(Compile -> 'temp\\%(Filename).xml', ' ') == 'temp\\foo.xml temp\\bar.xml temp\\baz.xml'", state, true);
            AssertParseEvaluate(p, "@(Compile->'', '') == ''", state, true);
            AssertParseEvaluate(p, "@(Compile->'') == ';;'", state, true);
            AssertParseEvaluate(p, "@(Compile->'%(Nonexistent)', '') == ''", state, true);
            AssertParseEvaluate(p, "@(Compile->'%(Nonexistent)') == ';;'", state, true);
            AssertParseEvaluate(p, "@(Boolean)", state, true);
            AssertParseEvaluate(p, "@(Boolean) == true", state, true);
            AssertParseEvaluate(p, "'@(Empty, ';')' == ''", state, true);}

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void StringExpansionTests()
        {
            Parser p = new Parser();
            BuildPropertyGroup propertyBag = new BuildPropertyGroup();
            Hashtable conditionedProperties = null;

            BuildItemGroup myNewItemGroup = new BuildItemGroup();
            myNewItemGroup.AddItem(new BuildItem("Compile", "foo.cs"));
            myNewItemGroup.AddItem(new BuildItem("Compile", "bar.cs"));
            myNewItemGroup.AddItem(new BuildItem("Compile", "baz.cs"));
            Hashtable itemBag = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemBag["COMPILE"] = myNewItemGroup;

            propertyBag = new BuildPropertyGroup();
            propertyBag.SetProperty("foo", "true");
            propertyBag.SetProperty("bar", "yes");
            propertyBag.SetProperty("one", "1");
            propertyBag.SetProperty("onepointzero", "1.0");
            propertyBag.SetProperty("two", "2");
            propertyBag.SetProperty("simple", "simplestring");
            propertyBag.SetProperty("complex", "This is a complex string");
            propertyBag.SetProperty("c1", "Another (complex) one.");
            propertyBag.SetProperty("c2", "Another (complex) one.");
            propertyBag.SetProperty("TestQuote", "Contains'Quote'");
            propertyBag.SetProperty("AnotherTestQuote", "Here's Johnny!");
            propertyBag.SetProperty("Atsign", "Test the @ replacement");

            Expander expander = new Expander(propertyBag, itemBag);
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "'simplestring: true foo.cs;bar.cs;baz.cs' == '$(simple): $(foo) @(compile)'", state, true);
            AssertParseEvaluate(p, "'$(c1) $(c2)' == 'Another (complex) one. Another (complex) one.'", state, true);
            AssertParseEvaluate(p, "'CONTAINS%27QUOTE%27' == '$(TestQuote)'", state, true);
            AssertParseEvaluate(p, "'Here%27s Johnny!' == '$(AnotherTestQuote)'", state, true);
            AssertParseEvaluate(p, "'Test the %40 replacement' == $(Atsign)", state, true);}

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void ComplexTests()
        {
            Parser p = new Parser();
            BuildPropertyGroup propertyBag = new BuildPropertyGroup();
            Hashtable conditionedProperties = null;

            BuildItemGroup myNewItemGroup = new BuildItemGroup();
            myNewItemGroup.AddItem(new BuildItem("Compile", "foo.cs"));
            myNewItemGroup.AddItem(new BuildItem("Compile", "bar.cs"));
            myNewItemGroup.AddItem(new BuildItem("Compile", "baz.cs"));
            Hashtable itemBag = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemBag["COMPILE"] = myNewItemGroup;

            propertyBag = new BuildPropertyGroup();
            propertyBag.SetProperty("foo", "true");
            propertyBag.SetProperty("bar", "yes");
            propertyBag.SetProperty("one", "1");
            propertyBag.SetProperty("onepointzero", "1.0");
            propertyBag.SetProperty("two", "2");
            propertyBag.SetProperty("simple", "simplestring");
            propertyBag.SetProperty("complex", "This is a complex string");
            propertyBag.SetProperty("c1", "Another (complex) one.");
            propertyBag.SetProperty("c2", "Another (complex) one.");

            Expander expander = new Expander(propertyBag, itemBag);
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", state, true);
            AssertParseEvaluate(p, "(($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1", state, true);
            AssertParseEvaluate(p, "!((($(foo) != 'twoo' or !$(bar)) and 5 >= 1) or $(two) == 1)", state, false);}

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void OldSyntaxTests()
        {
            Parser p = new Parser();
            BuildPropertyGroup propertyBag = new BuildPropertyGroup();
            Hashtable conditionedProperties = null;

            BuildItemGroup myNewItemGroup = new BuildItemGroup();
            myNewItemGroup.AddItem(new BuildItem("Compile", "foo.cs"));
            myNewItemGroup.AddItem(new BuildItem("Compile", "bar.cs"));
            myNewItemGroup.AddItem(new BuildItem("Compile", "baz.cs"));
            Hashtable itemBag = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemBag["COMPILE"] = myNewItemGroup;

            propertyBag = new BuildPropertyGroup();
            propertyBag.SetProperty("foo", "true");
            propertyBag.SetProperty("bar", "yes");
            propertyBag.SetProperty("one", "1");
            propertyBag.SetProperty("onepointzero", "1.0");
            propertyBag.SetProperty("two", "2");
            propertyBag.SetProperty("simple", "simplestring");
            propertyBag.SetProperty("complex", "This is a complex string");
            propertyBag.SetProperty("c1", "Another (complex) one.");
            propertyBag.SetProperty("c2", "Another (complex) one.");

            Expander expander = new Expander(propertyBag, itemBag);
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "(($(foo) != 'two' and $(bar)) and 5 >= 1) or $(one) == 1", state, true);}

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void ConditionedPropertyUpdateTests()
        {
            Parser p = new Parser();
            BuildPropertyGroup propertyBag = new BuildPropertyGroup();
            Hashtable conditionedProperties = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildItemGroup myNewItemGroup = new BuildItemGroup();
            myNewItemGroup.AddItem(new BuildItem("Compile", "foo.cs"));
            myNewItemGroup.AddItem(new BuildItem("Compile", "bar.cs"));
            myNewItemGroup.AddItem(new BuildItem("Compile", "baz.cs"));
            Hashtable itemBag = new Hashtable(StringComparer.OrdinalIgnoreCase);
            itemBag["Compile"] = myNewItemGroup;

            Expander expander = new Expander(LookupHelpers.CreateLookup(itemBag).ReadOnlyLookup);
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            StringCollection sc;
            AssertParseEvaluate(p, "'0' == '1'", state, false);
            Assertion.Assert(conditionedProperties.Count == 0);

            AssertParseEvaluate(p, "$(foo) == foo", state, false);
            Assertion.Assert(conditionedProperties.Count == 1);
            sc = (StringCollection)conditionedProperties["foo"];
            Assertion.Assert(sc.Count == 1);

            AssertParseEvaluate(p, "'$(foo)' != 'bar'", state, true);
            Assertion.Assert(conditionedProperties.Count == 1);
            sc = (StringCollection)conditionedProperties["foo"];
            Assertion.Assert(sc.Count == 2);

            AssertParseEvaluate(p, "'$(branch)|$(build)|$(platform)' == 'lab22dev|debug|x86'", state, false);
            Assertion.Assert(conditionedProperties.Count == 4);
            sc = (StringCollection)conditionedProperties["foo"];
            Assertion.Assert(sc.Count == 2);
            sc = (StringCollection)conditionedProperties["branch"];
            Assertion.Assert(sc.Count == 1);
            sc = (StringCollection)conditionedProperties["build"];
            Assertion.Assert(sc.Count == 1);
            sc = (StringCollection)conditionedProperties["platform"];
            Assertion.Assert(sc.Count == 1);

            AssertParseEvaluate(p, "'$(branch)|$(build)|$(platform)' == 'lab21|debug|x86'", state, false);
            Assertion.Assert(conditionedProperties.Count == 4);
            sc = (StringCollection)conditionedProperties["foo"];
            Assertion.Assert(sc.Count == 2);
            sc = (StringCollection)conditionedProperties["branch"];
            Assertion.Assert(sc.Count == 2);
            sc = (StringCollection)conditionedProperties["build"];
            Assertion.Assert(sc.Count == 1);
            sc = (StringCollection)conditionedProperties["platform"];
            Assertion.Assert(sc.Count == 1);

            AssertParseEvaluate(p, "'$(branch)|$(build)|$(platform)' == 'lab23|retail|ia64'", state, false);
            Assertion.Assert(conditionedProperties.Count == 4);
            sc = (StringCollection)conditionedProperties["foo"];
            Assertion.Assert(sc.Count == 2);
            sc = (StringCollection)conditionedProperties["branch"];
            Assertion.Assert(sc.Count == 3);
            sc = (StringCollection)conditionedProperties["build"];
            Assertion.Assert(sc.Count == 2);
            sc = (StringCollection)conditionedProperties["platform"];
            Assertion.Assert(sc.Count == 2);
            DumpHashtable(conditionedProperties);
        }

        private static void DumpHashtable(Hashtable ht)
        {
            foreach (DictionaryEntry entry in ht)
            {
                Console.Write("  {0}:\t", entry.Key);

                StringCollection sc = (StringCollection)entry.Value;
                StringEnumerator scEnumerator = sc.GetEnumerator();
                while (scEnumerator.MoveNext())
                    Console.Write("{0}, ", scEnumerator.Current);

                Console.WriteLine();
            }
        }

        /// <summary>
        /// </summary>
        /// <owner>DavidLe</owner>
        [Test]
        public void NotTests()
        {
            Console.WriteLine("NegationParseTest()");
            Parser p = new Parser();
            Hashtable conditionedProperties = null;

            BuildPropertyGroup propertyBag = new BuildPropertyGroup();
            propertyBag.SetProperty("foo", "4");
            propertyBag.SetProperty("bar", "32");

            Expander expander = new Expander(propertyBag);
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluate(p, "!true", state, false);
            AssertParseEvaluate(p, "!(true)", state, false);
            AssertParseEvaluate(p, "!($(foo) <= 5)", state, false);
            AssertParseEvaluate(p, "!($(foo) <= 5 and $(bar) >= 15)", state, false);
        }

        private void AssertParseEvaluateThrow(Parser p, string expression, ConditionEvaluationState state)
        {
            bool fExceptionCaught;

            try
            {
                fExceptionCaught = false;
                GenericExpressionNode tree = p.Parse(expression, DummyAttribute, ParserOptions.AllowAll);
                state.parsedCondition = expression;
                tree.Evaluate(state);
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
            Parser p = new Parser();
            Expander expander = new Expander(new BuildPropertyGroup());
            Hashtable conditionedProperties = null;
            ConditionEvaluationState state = new ConditionEvaluationState(DummyAttribute, expander, conditionedProperties, string.Empty);

            AssertParseEvaluateThrow(p, "foobar", state);
            AssertParseEvaluateThrow(p, "0", state);
            AssertParseEvaluateThrow(p, "$(platform) == xx > 1==2", state);
            AssertParseEvaluateThrow(p, "!0", state);
            AssertParseEvaluateThrow(p, ">", state);
            AssertParseEvaluateThrow(p, "true!=false==", state);
            AssertParseEvaluateThrow(p, "()", state);
            AssertParseEvaluateThrow(p, "!1", state);
            AssertParseEvaluateThrow(p, "true!=false==true", state);
            AssertParseEvaluateThrow(p, "'a'>'a'", state);
            AssertParseEvaluateThrow(p, "=='x'", state);
            AssertParseEvaluateThrow(p, "==", state);
            AssertParseEvaluateThrow(p, "1==(2", state);
            AssertParseEvaluateThrow(p, "'a'==('a'=='a')", state);
            AssertParseEvaluateThrow(p, "true == on and ''", state);
            AssertParseEvaluateThrow(p, "'' or 'true'", state);
        }
    }
}


