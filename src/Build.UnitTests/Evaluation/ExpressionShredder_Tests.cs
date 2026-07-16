// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;
using System.Linq;

#nullable disable

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Compares the items and metadata that ExpressionShredder finds
    /// with the results from the old regexes to make sure they're identical
    /// in every case.
    /// </summary>
    [TestClass]
    public class ExpressionShredder_Tests
    {
        private string[] _medleyTests = new string[]
        {
            "a;@(foo,');');b",
            "x@(z);@(zz)y",
            "exists('@(u)')",
            "a;b",
            "a;;",
            "a",
            "@A->'%(x)'",
            "@@(",
            "@@",
            "@(z1234567890_-AZaz->'z1234567890_-AZaz','a1234567890_-AZaz')",
            "@(z1234567890_-AZaz,'a1234567890_-AZaz')",
            "@(z1234567890_-AZaz)",
            "@(z1234567890_-AXZaxz  -> '%(a1234567890_-AXZaxz).%(adfas)'   )",
            "@(z123456.7890_-AXZaxz  -> '%(a1234567890_-AXZaxz).%(adfas)'  )",
            "@(z->'%(x)",
            "@(z->%(x)",
            "@(z,'%(x)",
            "@(z,%(x)",
            "@(z) and true",
            "@(z%(x)",
            "@(z -> '%(filename).z', '$')=='xxx.z$yyy.z'",
            "@(z -> '%(filename)', '!')=='xxx!yyy'",
            "@(y)==$(d)",
            "@(y)<=1",
            "@(y -> '%(filename)')=='xxx'",
            "@(x\u00DF)",
            "@(x1234567890_-AZaz->'x1234567890_-AZaz')",
            "@(x1234567890_-AZaz)",
            "@(x123 4567890_-AZaz->'x1234567890_-AZaz')",
            "@(x->)",
            "@(x->)",
            "@(x->'x','')",
            "@(x->'x',''",
            "@(x->'x','",
            "@(x->')",
            "@(x->''",
            "@(x->''",
            "@(x->'",
            "@(x->",
            "@(x-",
            "@(x,')",
            "@(x)@(x)",
            "@(x)<x",
            "@(x);@(x)",
            "@(x)",
            "@(x''';",
            "@(x",
            "@(x!)",
            "@(w)>0",
            "@(nonexistent)",
            "@(nonexistent) and true",
            "@(foo->'x')",
            "@(foo->'abc;def', 'ghi;jkl')",
            "@(foo->';());', ';@();')",
            "@(foo->';');def;@ghi;",
            "@(foo->';')",
            "@(foo-->'x')", // "foo-" is a legit item type
            "@(foo, ';')",
            "@(a1234:567890_-AZaz->'z1234567890_-AZaz')",
            "@(a1234567890_-AZaz->'z1234567890_-AZaz')",
            "@(a1234567890_-AXZaxz  -> 'a1234567890_-AXZaxz'   ,  'z1234567890_-AXZaxz'   )",
            "@(a1234567890_-AXZaxz  , 'z123%%4567890_-AXZaxz'   )",
            "@(a->'a')",
            "@(a->'a'  ,  'a')",
            "@(a)@(x)!=1",
            "@(a)",
            "@(a) @(x)!=1",
            "@(a  ,  'a')",
            "@(_X->'_X','X')",
            "@(_X->'_X')",
            "@(_X,'X')",
            "@(_X)",
            "@(_->'@#$%$%^&*&*)','@#$%$%^&*&*)')",
            "@(_->'@#$%$%^&*&*)')",
            "@(_,'@#$%$%^&*&*)')",
            "@(_)",
            "@(\u1234%(x)",
            "@(\u00DF)",
            "@(Z1234567890_-AZaz)",
            "@(Z1234567890_-AZaz -> 'Z1234567890_-AZaz')",
            "@(Com:pile)",
            "@(Com.pile)",
            "@(Com%pile)",
            "@(Com pile)",
            "@(A1234567890_-AZaz,'!@#$%^&*)(_+'))",
            "@(A1234567890_-AZaz)",
            "@(A1234567890_-AZaz ->'A1234567890_-AZaz')",
            "@(A1234567890_-AZaz ->'A1234567890_-AZaz' , '!@#$%^&*)(_+'))",
            "@(A->'foo%(x)bar',',')",
            "@(A->'%(x))",
            "@(A->'%(x)')@(B->'%(x);%(y)')@(C->'%(z)')",
            "@(A->'%(x)');@(B->'%(x);%(y)');;@(C->'%(z)')",
            "@(A->'%(x)')",
            "@(A->%(x))",
            "@(A,'%(x)')",
            "@(A, '%(x)->%(y)')",
            "@(A, '%(x)%(y)')",
            "@(A > '%(x)','+')",
            "@(:Z1234567890_-AZaz -> 'Z1234567890_-AZaz')",
            "@(:Compile)",
            "@(1x->'@#$%$%^&*&*)')",
            "@(1Compile)",
            "@(1->'a')",
            "@(.Compile)",
            "@(.A1234567890_-AZaz ->'A1234567890_-AZaz')",
            "@(-x->'_X')",
            "@(-Compile)",
            "@()",
            "@() and true",
            "@(%Compile)",
            "@(%(x)",
            "@(", "@()", "@",
            "@(",
            "@( foo -> ';);' , ';);' )",
            "@( foo -> ');' )",
            "@( A -> '%(Directory)%(Filename)%(Extension)', ' ** ')",
            "@( )",
            "@(   foo  )",
            "@(   foo  ",
            "@(   a1234567890_-AXZaxz   )",
            "@",
            "@ (x)",
            "@(x,'@(y)%(x)@(z->')",
            "@(x,'@(y)')",   // verify items inside separators aren't found
            "@(x,'@(y, '%(z)')')",
            "@(x,'@(y)%(z)')",
            "@(x,'@(y)%(x')",
            "@(x,'')",
            "@(x->'','')",
            "@(x->'%(z)','')",
            ";a;bbb;;c;;",
            ";;a",
            ";;;@(A->'%(x)');@(B)@(C->'%(y)');%(x)@(D->'%(y)');;",
            ";;",
            ";",
            ";  ",
            "1<=@(z)",
            "1<=@(w)",
            "'xxx!yyy'==@(z -> '%(filename)', '!')",
            "'@(z)'=='xxx;yyy'",
            "'$(e)1@(y)'=='xxx1xxx'",
            "'$(c)@(y)'>1",
            "%x)",
            "%x",
            "%(z1234567890_-AZaz.z1234567890_-AZaz)",
            "%(z1234567890_-AZaz)",
            "%(x1234567890_-AZaz.x1234567890_-AZaz)",
            "%(x1234567890_-AZaz)",
            "%(x._)",
            "%(x)",
            "%(x",
            "%(x )",
            "%(foo.goo.baz)",
            "%(foo.goo baz)",
            "%(foo goo.rhu barb)",
            "%(abc._X)",
            "%(a@(z)",
            "%(a1234567890_-AXZaxz)",
            "%(a12.a)",
            "%(a.x)",
            "%(a.x )",
            "%(a.a@(z)",
            "%(a.@(z)",
            "%(a. x)",
            "%(a)",
            "%(a . x)",
            "%(_X)",
            "%(_)",
            "%(Z1234567890_-AZaz.Z1234567890_-AZaz)",
            "%(Z1234567890_-AZaz)",
            "%(MyType.attr)",
            "%(InvalidAttrWithA Space)",
            "%(Foo.Bar.)",
            "%(Compile.)",
            "%(Com:pile.Com:pile)",
            "%(Com:pile)",
            "%(Com.pile.Com.pile)",
            "%(Com%pile.Com%pile)",
            "%(Com%pile)",
            "%(Com pile.Com pile)",
            "%(Com pile)",
            "%(A1234567890_-AZaz.A1234567890_-AZaz)",
            "%(A1234567890_-AZaz)",
            "%(A.x)%(b.x)",
            "%(A.x)",
            "%(A.x)  %( x )",
            "%(A.)",
            "%(A. )",
            "%(A .x)",
            "%(A .)",
            "%(A . )",
            "%(@(z)",
            "%(:Compile.:Compile)",
            "%(:Compile)",
            "%(1Compile.1Compile)",
            "%(1Compile)",
            "%(.x)",
            "%(.x )",
            "%(.foo.bar)",
            "%(.Compile)",
            "%(.)",
            "%(. x)",
            "%(. x )",
            "%(-Compile.-Compile)",
            "%(-Compile)",
            "%()",
            "%(%Compile.%Compile)",
            "%(%Compile)",
            "%( x)",
            "%( MyType . attr  )",
            "%( A.x)",
            "%( A.x )",
            "%( A.)",
            "%( A .)",
            "%( A . x )",
            "%( .x)",
            "%( . x)",
            "%( . x )",
            "%( )",
            "%(  foo  )",
            "%(  Invalid AttrWithASpace  )",
            "%(  A  .  )",
            "%(   x   )",
            "%(   a1234567890_-AXZaxz.a1234567890_-AXZaxz   )",
            "% x",
            "% (x)",
            "$(c)@(y)>1",
            "",
            "",
            "!@#$%^&*",
            " @(foo->'', '')",
            " ->       ';abc;def;'   ,     'ghi;jkl'   )",
            " %(A . x)%%%%%%%%(b . x) ",
            "  ;  a   ;b   ;   ;c",
            "                $(AssemblyOriginatorKeyFile);\n\t                @(Compile);",
                            "@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);"
        };

        [MSBuildTestMethod]
        public void Medley()
        {
            foreach (string test in _medleyTests)
            {
                VerifyExpression(test);
            }
        }

        [MSBuildTestMethod]
        public void NoOpSplit()
        {
            VerifySplitSemiColonSeparatedList("a", "a");
        }

        [MSBuildTestMethod]
        public void BasicSplit()
        {
            VerifySplitSemiColonSeparatedList("a;b", "a", "b");
        }

        [MSBuildTestMethod]
        public void Empty()
        {
            VerifySplitSemiColonSeparatedList("", null);
        }

        [MSBuildTestMethod]
        public void SemicolonOnly()
        {
            VerifySplitSemiColonSeparatedList(";", null);
        }

        [MSBuildTestMethod]
        public void TwoSemicolons()
        {
            VerifySplitSemiColonSeparatedList(";;", null);
        }

        [MSBuildTestMethod]
        public void TwoSemicolonsAndOneEntryAtStart()
        {
            VerifySplitSemiColonSeparatedList("a;;", "a");
        }

        [MSBuildTestMethod]
        public void TwoSemicolonsAndOneEntryAtEnd()
        {
            VerifySplitSemiColonSeparatedList(";;a", "a");
        }

        [MSBuildTestMethod]
        public void AtSignAtEnd()
        {
            VerifySplitSemiColonSeparatedList("@", "@");
        }

        [MSBuildTestMethod]
        public void AtSignParenAtEnd()
        {
            VerifySplitSemiColonSeparatedList("foo@(", "foo@(");
        }

        [MSBuildTestMethod]
        public void EmptyEntriesRemoved()
        {
            VerifySplitSemiColonSeparatedList(";a;bbb;;c;;", "a", "bbb", "c");
        }

        [MSBuildTestMethod]
        public void EntriesTrimmed()
        {
            VerifySplitSemiColonSeparatedList("  ;  a   ;b   ;   ;c\n;  \r;  ", "a", "b", "c");
        }

        [MSBuildTestMethod]
        public void NoSplittingOnMacros()
        {
            VerifySplitSemiColonSeparatedList("@(foo->';')", "@(foo->';')");
        }

        [MSBuildTestMethod]
        public void NoSplittingOnSeparators()
        {
            VerifySplitSemiColonSeparatedList("@(foo, ';')", "@(foo, ';')");
        }

        [MSBuildTestMethod]
        public void NoSplittingOnSeparatorsAndMacros()
        {
            VerifySplitSemiColonSeparatedList("@(foo->'abc;def', 'ghi;jkl')", "@(foo->'abc;def', 'ghi;jkl')");
        }

        [MSBuildTestMethod]
        public void CloseParensInMacro()
        {
            VerifySplitSemiColonSeparatedList("@(foo->');')", "@(foo->');')");
        }

        [MSBuildTestMethod]
        public void CloseParensInSeparator()
        {
            VerifySplitSemiColonSeparatedList("a;@(foo,');');b", "a", "@(foo,');')", "b");
        }

        [MSBuildTestMethod]
        public void CloseParensInMacroAndSeparator()
        {
            VerifySplitSemiColonSeparatedList("@(foo->';);', ';);')", "@(foo->';);', ';);')");
        }

        [MSBuildTestMethod]
        public void EmptyQuotesInMacroAndSeparator()
        {
            VerifySplitSemiColonSeparatedList(" @(foo->'', '')", "@(foo->'', '')");
        }

        [MSBuildTestMethod]
        public void MoreParensAndAtSigns()
        {
            VerifySplitSemiColonSeparatedList("@(foo->';());', ';@();')", "@(foo->';());', ';@();')");
        }

        [MSBuildTestMethod]
        public void SplittingExceptForMacros()
        {
            VerifySplitSemiColonSeparatedList("@(foo->';');def;@ghi;", "@(foo->';')", "def", "@ghi");
        }

        // Invalid item expressions shouldn't cause an error in the splitting function.
        // The caller will emit an error later when it tries to parse the results.
        [MSBuildTestMethod]
        public void InvalidItemExpressions()
        {
            VerifySplitSemiColonSeparatedList("@(x", "@(x");
            VerifySplitSemiColonSeparatedList("@(x->')", "@(x->')");
            VerifySplitSemiColonSeparatedList("@(x->)", "@(x->)");
            VerifySplitSemiColonSeparatedList("@(x->''", "@(x->''");
            VerifySplitSemiColonSeparatedList("@(x->)", "@(x->)");
            VerifySplitSemiColonSeparatedList("@(x->", "@(x->");
            VerifySplitSemiColonSeparatedList("@(x,')", "@(x,')");

            // This one doesn't remove the ';' because it thinks it's in
            // an item list. This isn't worth tweaking, because the invalid expression is
            // going to lead to an error in the caller whether there's a ';' or not.
            VerifySplitSemiColonSeparatedList("@(x''';", "@(x''';");
        }

        [MSBuildTestMethod]
        public void RealisticExample()
        {
            VerifySplitSemiColonSeparatedList("@(_OutputPathItem->'%(FullPath)', ';');$(MSBuildAllProjects);\n                @(Compile);\n                @(ManifestResourceWithNoCulture);\n                $(ApplicationIcon);\n                $(AssemblyOriginatorKeyFile);\n                @(ManifestNonResxWithNoCultureOnDisk);\n                @(ReferencePath);\n                @(CompiledLicenseFile);\n                @(EmbeddedDocumentation);                \n                @(CustomAdditionalCompileInputs)",
                "@(_OutputPathItem->'%(FullPath)', ';')", "$(MSBuildAllProjects)", "@(Compile)", "@(ManifestResourceWithNoCulture)", "$(ApplicationIcon)", "$(AssemblyOriginatorKeyFile)", "@(ManifestNonResxWithNoCultureOnDisk)", "@(ReferencePath)", "@(CompiledLicenseFile)", "@(EmbeddedDocumentation)", "@(CustomAdditionalCompileInputs)");
        }

        // For reference, this is the authoritative definition of an item expression:
        //  @"@\(\s*
        //      (?<TYPE>[\w\x20-]*[\w-]+)
        //      (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
        //      (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
        //  \s*\)";
        // We need to support any item expressions that satisfy this expression.
        //
        // Try spaces everywhere that regex allows spaces:
        [MSBuildTestMethod]
        public void SpacingInItemListExpression()
        {
            VerifySplitSemiColonSeparatedList("@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )", "@(   foo  \n ->  \t  ';abc;def;'   , \t  'ghi;jkl'   )");
        }

        /// <summary>
        /// Helper method for SplitSemiColonSeparatedList tests
        /// </summary>
        /// <param name="input"></param>
        /// <param name="expected"></param>
        private void VerifySplitSemiColonSeparatedList(string input, params string[] expected)
        {
            var actual = ExpressionShredder.SplitSemiColonSeparatedList(input);
            Console.WriteLine(input);

            if (expected == null)
            {
                // passing "null" means you expect an empty array back
                expected = Array.Empty<string>();
            }

            Assert.IsTrue(actual.SequenceEqual(expected, StringComparer.Ordinal));
        }

        private void VerifyExpression(string test)
        {
            List<string> list = new List<string>();
            list.Add(test);
            ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata(list);

            HashSet<string> actualItems = pair.Items;
            Dictionary<string, MetadataReference> actualMetadata = pair.Metadata;

            HashSet<string> expectedItems = GetConsumedItemReferences_OriginalImplementation(test);
            Console.WriteLine("verifying item names...");
            VerifyAgainstCanonicalResults(test, actualItems, expectedItems);

            Dictionary<string, MetadataReference> expectedMetadata = GetConsumedMetadataReferences_OriginalImplementation(test);
            Console.WriteLine("verifying metadata ...");
            VerifyAgainstCanonicalResults(test, actualMetadata, expectedMetadata);

            Console.WriteLine("===OK===");
        }

        private static void VerifyAgainstCanonicalResults(string test, HashSet<string> actual, HashSet<string> expected)
        {
            List<string> messages = new List<string>();

            Console.WriteLine("Expecting " + expected.Count + " distinct values for <" + test + ">");

            if (actual != null)
            {
                foreach (string result in actual)
                {
                    if (expected?.Contains(result) != true)
                    {
                        messages.Add("Found <" + result + "> in <" + test + "> but it wasn't expected");
                    }
                }
            }

            if (expected != null)
            {
                foreach (string expect in expected)
                {
                    if (actual?.Contains(expect) != true)
                    {
                        messages.Add("Did not find <" + expect + "> in <" + test + ">");
                    }
                }
            }

            if (messages.Count > 0)
            {
                if (actual != null)
                {
                    Console.Write("FOUND: ");
                    foreach (string result in actual)
                    {
                        Console.Write("<" + result + "> ");
                    }
                    Console.WriteLine();
                }
            }

            foreach (string message in messages)
            {
                Console.WriteLine(message);
            }

            Assert.IsEmpty(messages);
        }

        private static void VerifyAgainstCanonicalResults(string test, IDictionary actual, IDictionary expected)
        {
            List<string> messages = new List<string>();

            Console.WriteLine("Expecting " + expected.Count + " distinct values for <" + test + ">");

            if (actual != null)
            {
                foreach (DictionaryEntry result in actual)
                {
                    if (expected?.Contains(result.Key) != true)
                    {
                        messages.Add("Found <" + result.Key + "> in <" + test + "> but it wasn't expected");
                    }
                }
            }

            if (expected != null)
            {
                foreach (DictionaryEntry expect in expected)
                {
                    if (actual?.Contains(expect.Key) != true)
                    {
                        messages.Add("Did not find <" + expect.Key + "> in <" + test + ">");
                    }
                }
            }

            if (messages.Count > 0)
            {
                if (actual != null)
                {
                    Console.Write("FOUND: ");
                    foreach (string result in actual.Keys)
                    {
                        Console.Write("<" + result + "> ");
                    }
                    Console.WriteLine();
                }
            }

            foreach (string message in messages)
            {
                Console.WriteLine(message);
            }

            Assert.IsEmpty(messages);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorTransform1()
        {
            string expression = "@(i->'%(Meta0)'->'%(Filename)'->Substring($(Val)))";
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());

            ExpressionShredder.ItemExpressionCapture capture = expressions.Current;

            Assert.IsFalse(expressions.MoveNext());
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("i", capture.ItemType);
            Assert.AreEqual("%(Meta0)", capture.Captures[0].Value);
            Assert.AreEqual("%(Filename)", capture.Captures[1].Value);
            Assert.AreEqual("Substring($(Val))", capture.Captures[2].Value);
        }

        /// <summary>
        /// Compare the results of the expression shredder based item expression extractor with the original regex based one
        /// NOTE: The medley of tests needs to be parsable by the old regex. This is a regression test against that
        /// regex. New expression types should be added in other tests
        /// </summary>
        [MSBuildTestMethod]
        public void ItemExpressionMedleyRegressionTestAgainstOldRegex()
        {
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;

            foreach (string expression in _medleyTests)
            {
                expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
                MatchCollection matches = s_itemVectorPattern.Matches(expression);
                int expressionCount = 0;

                while (expressions.MoveNext())
                {
                    Match match = matches[expressionCount];
                    ExpressionShredder.ItemExpressionCapture capture = expressions.Current;

                    Assert.AreEqual(match.Value, capture.Value);

                    Group transformGroup = match.Groups["TRANSFORM"];

                    if (capture.Captures != null)
                    {
                        for (int i = 0; i < transformGroup.Captures.Count; i++)
                        {
                            Assert.AreEqual(transformGroup.Captures[i].Value, capture.Captures[i].Value);
                        }
                    }
                    else
                    {
                        Assert.AreEqual(0, transformGroup.Length);
                    }

                    ++expressionCount;
                }

                if (expressionCount == 0)
                {
                    Assert.IsEmpty(matches);
                }
                else
                {
                    Assert.AreEqual(matches.Count, expressionCount);
                }
            }
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpressionInvalid1()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;

            expression = "@(type-&gt;'%($(a)), '%'')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsFalse(expressions.MoveNext());
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression1()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo)";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.IsNull(capture.Separator);
            Assert.IsNull(capture.Captures);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.IsNull(capture.Captures);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression2()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo, ';')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.IsNull(capture.Captures);
            Assert.AreEqual(";", capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.IsNull(capture.Captures);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression3()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Fullpath)')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.ContainsSingle(capture.Captures);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.ContainsSingle(capture.Captures);
            Assert.AreEqual("%(Fullpath)", capture.Captures[0].Value);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression4()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Fullpath)',';')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.ContainsSingle(capture.Captures);
            Assert.AreEqual(";", capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.ContainsSingle(capture.Captures);
            Assert.AreEqual("%(Fullpath)", capture.Captures[0].Value);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression5()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->Bar(a,b))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.ContainsSingle(capture.Captures);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.ContainsSingle(capture.Captures);
            Assert.AreEqual("Bar(a,b)", capture.Captures[0].Value);
            Assert.AreEqual("Bar", capture.Captures[0].FunctionName);
            Assert.AreEqual("a,b", capture.Captures[0].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression6()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->Bar(a,b),';')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.ContainsSingle(capture.Captures);
            Assert.AreEqual(";", capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.ContainsSingle(capture.Captures);
            Assert.AreEqual("Bar(a,b)", capture.Captures[0].Value);
            Assert.AreEqual("Bar", capture.Captures[0].FunctionName);
            Assert.AreEqual("a,b", capture.Captures[0].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression7()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->Metadata('Meta0')->Directory())";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("Metadata('Meta0')", capture.Captures[0].Value);
            Assert.AreEqual("Metadata", capture.Captures[0].FunctionName);
            Assert.AreEqual("'Meta0'", capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Directory()", capture.Captures[1].Value);
            Assert.AreEqual("Directory", capture.Captures[1].FunctionName);
            Assert.IsNull(capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression8()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->Metadata('Meta0')->Directory(),';')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.AreEqual(";", capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("Metadata('Meta0')", capture.Captures[0].Value);
            Assert.AreEqual("Metadata", capture.Captures[0].FunctionName);
            Assert.AreEqual("'Meta0'", capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Directory()", capture.Captures[1].Value);
            Assert.AreEqual("Directory", capture.Captures[1].FunctionName);
            Assert.IsNull(capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression9()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Fullpath)'->Directory(), '|')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.AreEqual("|", capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Fullpath)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Directory()", capture.Captures[1].Value);
            Assert.AreEqual("Directory", capture.Captures[1].FunctionName);
            Assert.IsNull(capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression10()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Fullpath)'->Directory(),';')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.AreEqual(";", capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Fullpath)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Directory()", capture.Captures[1].Value);
            Assert.AreEqual("Directory", capture.Captures[1].FunctionName);
            Assert.IsNull(capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression11()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'$(SOMEPROP)%(Fullpath)')";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.ContainsSingle(capture.Captures);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("$(SOMEPROP)%(Fullpath)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression12()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Filename)'->Substring($(Val), $(Boo)))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring($(Val), $(Boo))", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("$(Val), $(Boo)", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression13()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Filename)'->Substring(\"AA\", 'BB', `cc`))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring(\"AA\", 'BB', `cc`)", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("\"AA\", 'BB', `cc`", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression14()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Filename)'->Substring('()', $(Boo), ')('))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring('()', $(Boo), ')(')", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("'()', $(Boo), ')('", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression15()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Filename)'->Substring(`()`, $(Boo), \"AA\"))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring(`()`, $(Boo), \"AA\")", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("`()`, $(Boo), \"AA\"", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression16()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Filename)'->Substring(`()`, $(Boo), \")(\"))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring(`()`, $(Boo), \")(\")", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("`()`, $(Boo), \")(\"", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsSingleExpression17()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring(\"()\", $(Boo), `)(`)", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("\"()\", $(Boo), `)(`", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsMultipleExpression1()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture firstCapture;
            ExpressionShredder.ItemExpressionCapture capture;

            expression = "@(Bar);@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            firstCapture = expressions.Current;
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("Bar", firstCapture.ItemType);
            Assert.IsNull(firstCapture.Captures);
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring(\"()\", $(Boo), `)(`)", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("\"()\", $(Boo), `)(`", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsMultipleExpression2()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture firstCapture;
            ExpressionShredder.ItemExpressionCapture secondCapture;

            expression = "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`));@(Bar)";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            firstCapture = expressions.Current;
            Assert.IsTrue(expressions.MoveNext());
            secondCapture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("Bar", secondCapture.ItemType);
            Assert.IsNull(secondCapture.Captures);
            Assert.AreEqual(2, firstCapture.Captures.Count);
            Assert.IsNull(firstCapture.Separator);
            Assert.AreEqual("Foo", firstCapture.ItemType);
            Assert.AreEqual("%(Filename)", firstCapture.Captures[0].Value);
            Assert.IsNull(firstCapture.Captures[0].FunctionName);
            Assert.IsNull(firstCapture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring(\"()\", $(Boo), `)(`)", firstCapture.Captures[1].Value);
            Assert.AreEqual("Substring", firstCapture.Captures[1].FunctionName);
            Assert.AreEqual("\"()\", $(Boo), `)(`", firstCapture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsMultipleExpression3()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;
            ExpressionShredder.ItemExpressionCapture secondCapture;

            expression = "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(`));AAAAAA;@(Bar)";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsTrue(expressions.MoveNext());
            secondCapture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("Bar", secondCapture.ItemType);
            Assert.IsNull(secondCapture.Captures);
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring(\"()\", $(Boo), `)(`)", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("\"()\", $(Boo), `)(`", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsMultipleExpression4()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;
            ExpressionShredder.ItemExpressionCapture secondCapture;

            expression = "@(Foo->'%(Filename)'->Substring(\"()\", $(Boo), `)(\"`));@(;);@(aaa->;b);@(bbb->'d);@(`Foo->'%(Filename)'->Distinct());@(Bar)";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsTrue(expressions.MoveNext());
            secondCapture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("Bar", secondCapture.ItemType);
            Assert.IsNull(secondCapture.Captures);
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.IsNull(capture.Separator);
            Assert.AreEqual("Foo", capture.ItemType);
            Assert.AreEqual("%(Filename)", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
            Assert.IsNull(capture.Captures[0].FunctionArguments);
            Assert.AreEqual("Substring(\"()\", $(Boo), `)(\"`)", capture.Captures[1].Value);
            Assert.AreEqual("Substring", capture.Captures[1].FunctionName);
            Assert.AreEqual("\"()\", $(Boo), `)(\"`", capture.Captures[1].FunctionArguments);
        }

        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsMultipleExpression5()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;

            expression = "@(foo);@(foo,'-');@(foo);@(foo,',');@(foo)";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);

            Assert.IsTrue(expressions.MoveNext());
            Assert.AreEqual("foo", expressions.Current.ItemType);
            Assert.IsNull(expressions.Current.Separator);

            Assert.IsTrue(expressions.MoveNext());
            Assert.AreEqual("foo", expressions.Current.ItemType);
            Assert.AreEqual("-", expressions.Current.Separator);

            Assert.IsTrue(expressions.MoveNext());
            Assert.AreEqual("foo", expressions.Current.ItemType);
            Assert.IsNull(expressions.Current.Separator);

            Assert.IsTrue(expressions.MoveNext());
            Assert.AreEqual("foo", expressions.Current.ItemType);
            Assert.AreEqual(",", expressions.Current.Separator);

            Assert.IsTrue(expressions.MoveNext());
            Assert.AreEqual("foo", expressions.Current.ItemType);
            Assert.IsNull(expressions.Current.Separator);

            Assert.IsFalse(expressions.MoveNext());
        }

        /// <summary>
        /// Test that item function chaining works with whitespace before arrow operators
        /// </summary>
        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsChainedFunctionsWithWhitespace()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;
            ExpressionShredder.ItemExpressionCapture capture;

            // Test with space before second arrow: ") ->"
            expression = "@(I -> WithMetadataValue('M', 'T') -> WithMetadataValue('M', 'T'))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("I", capture.ItemType);
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.AreEqual("WithMetadataValue", capture.Captures[0].FunctionName);
            Assert.AreEqual("'M', 'T'", capture.Captures[0].FunctionArguments);
            Assert.AreEqual("WithMetadataValue", capture.Captures[1].FunctionName);
            Assert.AreEqual("'M', 'T'", capture.Captures[1].FunctionArguments);

            // Test without space before second arrow: ")->"
            expression = "@(I -> WithMetadataValue('M', 'T')-> WithMetadataValue('M', 'T'))";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("I", capture.ItemType);
            Assert.AreEqual(2, capture.Captures.Count);
            Assert.AreEqual("WithMetadataValue", capture.Captures[0].FunctionName);
            Assert.AreEqual("'M', 'T'", capture.Captures[0].FunctionArguments);
            Assert.AreEqual("WithMetadataValue", capture.Captures[1].FunctionName);
            Assert.AreEqual("'M', 'T'", capture.Captures[1].FunctionArguments);

            // Test with multiple spaces and chained functions
            expression = "@(I->Distinct() -> Reverse() ->Count())";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("I", capture.ItemType);
            Assert.AreEqual(3, capture.Captures.Count);
            Assert.AreEqual("Distinct", capture.Captures[0].FunctionName);
            Assert.AreEqual("Reverse", capture.Captures[1].FunctionName);
            Assert.AreEqual("Count", capture.Captures[2].FunctionName);

            // Test trailing whitespace after function call
            expression = "@(I -> Count() )";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("I", capture.ItemType);
            Assert.AreEqual(1, capture.Captures.Count);
            Assert.AreEqual("Count", capture.Captures[0].FunctionName);

            // Test trailing whitespace after quoted transform
            expression = "@(I -> 'Replacement' )";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            Assert.IsTrue(expressions.MoveNext());
            capture = expressions.Current;
            Assert.IsFalse(expressions.MoveNext());
            Assert.AreEqual("I", capture.ItemType);
            Assert.AreEqual(1, capture.Captures.Count);
            Assert.AreEqual("Replacement", capture.Captures[0].Value);
            Assert.IsNull(capture.Captures[0].FunctionName);
        }

        /// <summary>
        /// Test that invalid syntax after whitespace is properly rejected
        /// </summary>
        [MSBuildTestMethod]
        public void ExtractItemVectorExpressionsInvalidSyntaxAfterWhitespace()
        {
            string expression;
            ExpressionShredder.ReferencedItemExpressionsEnumerator expressions;

            // Invalid syntax after whitespace - should not be parsed as item expression
            expression = "@(I -> Count() invalid)";
            expressions = ExpressionShredder.GetReferencedItemExpressions(expression);
            // Should not find a valid expression due to invalid syntax
            Assert.IsFalse(expressions.MoveNext());
        }

        #region Original code to produce canonical results

        /// <summary>
        /// Looks through the parameters of the batchable object, and finds all referenced item lists.
        /// Returns a hashtable containing the item lists, where the key is the item name, and the
        /// value is always String.Empty (not used).
        /// </summary>
        private static HashSet<string> GetConsumedItemReferences_OriginalImplementation(string expression)
        {
            HashSet<string> result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match itemVector in s_itemVectorPattern.Matches(expression))
            {
                result.Add(itemVector.Groups["TYPE"].Value);
            }

            return result;
        }

        /// <summary>
        /// Looks through the parameters of the batchable object, and finds all references to item metadata
        /// (that aren't part of an item transform).  Returns a Hashtable containing a bunch of MetadataReference
        /// structs.  Each reference to item metadata may or may not be qualified with an item name (e.g.,
        /// %(Culture) vs. %(EmbeddedResource.Culture).
        /// </summary>
        /// <returns>Hashtable containing the metadata references.</returns>
        private static Dictionary<string, MetadataReference> GetConsumedMetadataReferences_OriginalImplementation(string expression)
        {
            // The keys in the hash table are the qualified metadata names (e.g. "EmbeddedResource.Culture"
            // or just "Culture").  The values are MetadataReference structs, which simply split out the item
            // name (possibly null) and the actual metadata name.
            Dictionary<string, MetadataReference> consumedMetadataReferences = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

            FindEmbeddedMetadataReferences_OriginalImplementation(expression, consumedMetadataReferences);

            return consumedMetadataReferences;
        }

        /// <summary>
        /// Looks through a single parameter of the batchable object, and finds all references to item metadata
        /// (that aren't part of an item transform).  Populates a Hashtable containing a bunch of MetadataReference
        /// structs.  Each reference to item metadata may or may not be qualified with an item name (e.g.,
        /// %(Culture) vs. %(EmbeddedResource.Culture).
        /// </summary>
        /// <param name="batchableObjectParameter"></param>
        /// <param name="consumedMetadataReferences"></param>
        private static void FindEmbeddedMetadataReferences_OriginalImplementation(
            string batchableObjectParameter,
            Dictionary<string, MetadataReference> consumedMetadataReferences)
        {
            MatchCollection embeddedMetadataReferences = FindEmbeddedMetadataReferenceMatches_OriginalImplementation(batchableObjectParameter);

            if (embeddedMetadataReferences != null)
            {
                foreach (Match embeddedMetadataReference in embeddedMetadataReferences)
                {
                    string metadataName = embeddedMetadataReference.Groups["NAME"].Value;
                    string qualifiedMetadataName = metadataName;

                    // Check if the metadata is qualified with the item name.
                    string itemName = null;
                    if (embeddedMetadataReference.Groups["ITEM_SPECIFICATION"].Length > 0)
                    {
                        itemName = embeddedMetadataReference.Groups["TYPE"].Value;
                        qualifiedMetadataName = itemName + "." + metadataName;
                    }

                    consumedMetadataReferences[qualifiedMetadataName] = new MetadataReference(itemName, metadataName);
                }
            }
        }

        // the leading characters that indicate the start of an item vector
        private const string itemVectorPrefix = "@(";

        // complete description of an item vector, including the optional transform expression and separator specification
        private const string itemVectorSpecification =
            @"@\(\s*
                (?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
                (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
                (?<SEPARATOR_SPECIFICATION>\s*,\s*'(?<SEPARATOR>[^']*)')?
            \s*\)";

        // description of an item vector, including the optional transform expression, but not the separator specification
        private const string itemVectorWithoutSeparatorSpecification =
            @"@\(\s*
                (?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
                (?<TRANSFORM_SPECIFICATION>\s*->\s*'(?<TRANSFORM>[^']*)')?
            \s*\)";

        // regular expression used to match item vectors, including those embedded in strings
        private static readonly Regex s_itemVectorPattern = new Regex(itemVectorSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // regular expression used to match a list of item vectors that have no separator specification -- the item vectors
        // themselves may be optionally separated by semi-colons, or they might be all jammed together
        private static readonly Regex s_listOfItemVectorsWithoutSeparatorsPattern =
            new Regex(@"^\s*(;\s*)*(" +
                      itemVectorWithoutSeparatorSpecification +
                      @"\s*(;\s*)*)+$",
                      RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // the leading characters that indicate the start of an item metadata reference
        private const string itemMetadataPrefix = "%(";

        // complete description of an item metadata reference, including the optional qualifying item type
        private const string itemMetadataSpecification =
            @"%\(\s*
                (?<ITEM_SPECIFICATION>(?<TYPE>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")\s*\.\s*)?
                (?<NAME>" + ProjectWriter.itemTypeOrMetadataNameSpecification + @")
            \s*\)";

        // regular expression used to match item metadata references embedded in strings
        private static readonly Regex s_itemMetadataPattern = new Regex(itemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // description of an item vector with a transform, split into two halves along the transform expression
        private const string itemVectorWithTransformLHS = @"@\(\s*" + ProjectWriter.itemTypeOrMetadataNameSpecification + @"\s*->\s*'[^']*";
        private const string itemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

        // PERF WARNING: this Regex is complex and tends to run slowly
        // regular expression used to match item metadata references outside of item vector expressions
        private static readonly Regex s_nonTransformItemMetadataPattern =
            new Regex(@"((?<=" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?!" + itemVectorWithTransformRHS + @")) |
                        ((?<!" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?=" + itemVectorWithTransformRHS + @")) |
                        ((?<!" + itemVectorWithTransformLHS + @")" + itemMetadataSpecification + @"(?!" + itemVectorWithTransformRHS + @"))",
                        RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        /// <summary>
        /// Looks through a single parameter of the batchable object, and finds all references to item metadata
        /// (that aren't part of an item transform).  Populates a MatchCollection object with any regex matches
        /// found in the input.  Each reference to item metadata may or may not be qualified with an item name (e.g.,
        /// %(Culture) vs. %(EmbeddedResource.Culture).
        /// </summary>
        /// <param name="batchableObjectParameter"></param>
        private static MatchCollection FindEmbeddedMetadataReferenceMatches_OriginalImplementation(string batchableObjectParameter)
        {
            MatchCollection embeddedMetadataReferences = null;

            // PERF NOTE: Regex matching is expensive, so if the string doesn't contain any item attribute references, just bail
            // out -- pre-scanning the string is actually cheaper than running the Regex, even when there are no matches!

            if (batchableObjectParameter.IndexOf(itemMetadataPrefix, StringComparison.Ordinal) != -1)
            {
                // if there are no item vectors in the string
                if (batchableObjectParameter.IndexOf(itemVectorPrefix, StringComparison.Ordinal) == -1)
                {
                    // run a simpler Regex to find item metadata references
                    embeddedMetadataReferences = s_itemMetadataPattern.Matches(batchableObjectParameter);
                }
                // PERF NOTE: this is a highly targeted optimization for a common pattern observed during profiling
                // if the string is a list of item vectors with no separator specifications
                else if (s_listOfItemVectorsWithoutSeparatorsPattern.IsMatch(batchableObjectParameter))
                {
                    // then even if the string contains item metadata references, those references will only be inside transform
                    // expressions, and can be safely skipped
                    embeddedMetadataReferences = null;
                }
                else
                {
                    // otherwise, run the more complex Regex to find item metadata references not contained in expressions
                    embeddedMetadataReferences = s_nonTransformItemMetadataPattern.Matches(batchableObjectParameter);
                }
            }

            return embeddedMetadataReferences;
        }

        #endregion
    }
}
