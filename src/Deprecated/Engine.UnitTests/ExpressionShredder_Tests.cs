// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;
using System.Text.RegularExpressions;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Compares the items and metadata that ExpressionShredder finds
    /// with the results from the old regexes to make sure they're identical
    /// in every case.
    /// </summary>
    [TestFixture]
    public class ExpressionShredder_Tests
    {
        [Test]
        public void Medley()
        {
            string[] tests = new string[]
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
                "%(foo.bar.baz)",
                "%(foo.bar baz)",
                "%(foo bar.rhu barb)",
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
                "%(  fooBar  )",
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

            foreach (string test in tests)
            {
                VerifyExpression(test);
            }
        }

        private void VerifyExpression(string test)
        {
            List<string> list = new List<string>();
            list.Add(test);
            ItemsAndMetadataPair pair = ExpressionShredder.GetReferencedItemNamesAndMetadata(list);

            Hashtable actualItems = pair.Items;
            Dictionary<string, MetadataReference> actualMetadata = pair.Metadata;

            Hashtable expectedItems = GetConsumedItemReferences_OriginalImplementation(test);
            Console.WriteLine("verifying item names...");
            VerifyAgainstCanonicalResults(test, actualItems, expectedItems);

            Hashtable expectedMetadata = GetConsumedMetadataReferences_OriginalImplementation(test);
            Console.WriteLine("verifying metadata ...");
            VerifyAgainstCanonicalResults(test, actualMetadata, expectedMetadata);

            Console.WriteLine("===OK===");
        }

        private static void VerifyAgainstCanonicalResults(string test, IDictionary actual, IDictionary expected)
        {
            List<string> messages = new List<string>();

            Console.WriteLine("Expecting " + expected.Count + " distinct values for <" + test + ">");

            if (actual != null)
            {
                foreach (DictionaryEntry result in actual)
                {
                    if (expected == null || !expected.Contains(result.Key))
                    {
                        messages.Add("Found <" + result.Key + "> in <" + test + "> but it wasn't expected");
                    }
                }
            }

            if (expected != null)
            {
                foreach (DictionaryEntry expect in expected)
                {
                    if (actual == null || !actual.Contains(expect.Key))
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

            Assertion.Assert(messages.Count == 0);
        }

        #region Original code to produce canonical results

        /// <summary>
        /// Looks through the parameters of the batchable object, and finds all referenced item lists.
        /// Returns a hashtable containing the item lists, where the key is the item name, and the
        /// value is always String.Empty (not used).
        /// </summary>
        private static Hashtable GetConsumedItemReferences_OriginalImplementation(string expression)
        {
            Hashtable result = new Hashtable(StringComparer.OrdinalIgnoreCase);

            foreach (Match itemVector in itemVectorPattern.Matches(expression))
            {
                result[itemVector.Groups["TYPE"].Value] = String.Empty;
            }

            return result;
        }

        /// <summary>
        /// Looks through the parameters of the batchable object, and finds all references to item metadata
        /// (that aren't part of an item transform).  Returns a Hashtable containing a bunch of MetadataReference
        /// structs.  Each reference to item metadata may or may not be qualified with an item name (e.g., 
        /// %(Culture) vs. %(EmbeddedResource.Culture).
        /// </summary>
        /// <owner>SumedhK, RGoel</owner>
        /// <returns>Hashtable containing the metadata references.</returns>
        private static Hashtable GetConsumedMetadataReferences_OriginalImplementation(string expression)
        {
            // The keys in the hash table are the qualified metadata names (e.g. "EmbeddedResource.Culture"
            // or just "Culture").  The values are MetadataReference structs, which simply split out the item 
            // name (possibly null) and the actual metadata name.
            Hashtable consumedMetadataReferences = new Hashtable(StringComparer.OrdinalIgnoreCase);

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
        private static void FindEmbeddedMetadataReferences_OriginalImplementation
        (
            string batchableObjectParameter,
            Hashtable consumedMetadataReferences
        )
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
        private static readonly Regex itemVectorPattern = new Regex(itemVectorSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // regular expression used to match a list of item vectors that have no separator specification -- the item vectors
        // themselves may be optionally separated by semi-colons, or they might be all jammed together
        private static readonly Regex listOfItemVectorsWithoutSeparatorsPattern =
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
        private static readonly Regex itemMetadataPattern = new Regex(itemMetadataSpecification, RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

        // description of an item vector with a transform, split into two halves along the transform expression
        private const string itemVectorWithTransformLHS = @"@\(\s*" + ProjectWriter.itemTypeOrMetadataNameSpecification + @"\s*->\s*'[^']*";
        private const string itemVectorWithTransformRHS = @"[^']*'(\s*,\s*'[^']*')?\s*\)";

        // PERF WARNING: this Regex is complex and tends to run slowly
        // regular expression used to match item metadata references outside of item vector transforms
        private static readonly Regex nonTransformItemMetadataPattern =
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
                    embeddedMetadataReferences = itemMetadataPattern.Matches(batchableObjectParameter);
                }
                // PERF NOTE: this is a highly targeted optimization for a common pattern observed during profiling
                // if the string is a list of item vectors with no separator specifications
                else if (listOfItemVectorsWithoutSeparatorsPattern.IsMatch(batchableObjectParameter))
                {
                    // then even if the string contains item metadata references, those references will only be inside transform
                    // expressions, and can be safely skipped
                    embeddedMetadataReferences = null;
                }
                else
                {
                    // otherwise, run the more complex Regex to find item metadata references not contained in transforms
                    embeddedMetadataReferences = nonTransformItemMetadataPattern.Matches(batchableObjectParameter);
                }
            }

            return embeddedMetadataReferences;
        }

        #endregion
    }
}
