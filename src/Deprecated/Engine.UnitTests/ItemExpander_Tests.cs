// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;
using Microsoft.Build.BuildEngine.Shared;
using System.Xml;
using System.Text.RegularExpressions;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class ItemExpanderTest
    {
        /// <summary>
        /// Generate a hashtable of items by type with a bunch of sample items, so that we can exercise
        /// ItemExpander.ItemizeItemVector.
        /// </summary>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private Hashtable GenerateTestItems()
        {
            Hashtable itemGroupsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);

            // Set up our item group programmatically.
            BuildItemGroup itemGroup = new BuildItemGroup();
            itemGroupsByType["Compile"] = itemGroup;
            BuildItem a = itemGroup.AddNewItem("Compile", "a.cs");
            a.SetMetadata("WarningLevel", "4");
            BuildItem b = itemGroup.AddNewItem("Compile", "b.cs");
            b.SetMetadata("WarningLevel", "3");

            BuildItemGroup itemGroup2 = new BuildItemGroup();
            itemGroupsByType["Resource"] = itemGroup2;
            BuildItem c = itemGroup2.AddNewItem("Resource", "c.resx");

            return itemGroupsByType;
        }

        /// <summary>
        /// Expand item vectors, basic case
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ExpandEmbeddedItemVectorsBasic()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            XmlNode foo = new XmlDocument().CreateElement("Foo");
            string evaluatedString = ItemExpander.ExpandEmbeddedItemVectors("@(Compile)", foo, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals("a.cs;b.cs", evaluatedString);
        }

        /// <summary>
        /// Expand item vectors, macro expansion
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ExpandEmbeddedItemVectorsMacroExpansion()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            XmlNode foo = new XmlDocument().CreateElement("Foo");
            string evaluatedString = ItemExpander.ExpandEmbeddedItemVectors("@(Compile->'%(filename)')", foo, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals("a;b", evaluatedString);
        }

        /// <summary>
        /// Expand item vectors, separator
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ExpandEmbeddedItemVectorsSeparator()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            XmlNode foo = new XmlDocument().CreateElement("Foo");
            string evaluatedString = ItemExpander.ExpandEmbeddedItemVectors("@(Compile, '#')", foo, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals("a.cs#b.cs", evaluatedString);
        }
        /// <summary>
        /// Expand item vectors, multiple vectors
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ExpandEmbeddedItemVectorsMultiple()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            XmlNode foo = new XmlDocument().CreateElement("Foo");
            string evaluatedString = ItemExpander.ExpandEmbeddedItemVectors("...@(Compile)...@(Resource)...", foo, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals("...a.cs;b.cs...c.resx...", evaluatedString);
        }

        /// <summary>
        /// Expand item vectors, macro expansion and separator
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ExpandEmbeddedItemVectorsSeparatorAndMacroExpansion()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            XmlNode foo = new XmlDocument().CreateElement("Foo");
            string evaluatedString = ItemExpander.ExpandEmbeddedItemVectors("@(Compile->'%(filename)','#')", foo, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals("a#b", evaluatedString);
        }

        /// <summary>
        /// Expand item vectors, no vectors
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ExpandEmbeddedItemVectorsNoVectors()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            XmlNode foo = new XmlDocument().CreateElement("Foo");
            string evaluatedString = ItemExpander.ExpandEmbeddedItemVectors("blah", foo, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals("blah", evaluatedString);
        }

        /// <summary>
        /// Expand item vectors, empty
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ExpandEmbeddedItemVectorsEmpty()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            XmlNode foo = new XmlDocument().CreateElement("Foo");
            string evaluatedString = ItemExpander.ExpandEmbeddedItemVectors(String.Empty, foo, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals(String.Empty, evaluatedString);
        }

        /// <summary>
        /// Itemize a normal item vector -- @(Compile)
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ItemizeItemVectorNormal()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            BuildItemGroup compileItems = ItemExpander.ItemizeItemVector("@(Compile)", null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);

            Assertion.AssertEquals("Resulting item group should have 2 items", 2, compileItems.Count);
            Assertion.AssertEquals("First item should be a.cs", "a.cs", compileItems[0].FinalItemSpecEscaped);
            Assertion.AssertEquals("First item WarningLevel should be 4", "4", compileItems[0].GetMetadata("WarningLevel"));
            Assertion.AssertEquals("First item should be b.cs", "b.cs", compileItems[1].FinalItemSpecEscaped);
            Assertion.AssertEquals("First item WarningLevel should be 3", "3", compileItems[1].GetMetadata("WarningLevel"));
        }      

        /// <summary>
        /// Attempt to itemize an expression that is an @(...) item list concatenated with another string.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void ItemizeItemVectorWithConcatenation()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            BuildItemGroup compileItems = ItemExpander.ItemizeItemVector("@(Compile)foo", null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
        }

        /// <summary>
        /// Attempt to itemize an expression that is in fact not an item list at all.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ItemizeItemVectorWithNoItemLists()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            BuildItemGroup compileItems = ItemExpander.ItemizeItemVector("foobar", null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            
            // If the specified expression does not contain any item lists, then we expect ItemizeItemVector
            // to give us back null, but not throw an exception.
            Assertion.AssertNull(compileItems);
        }

        /// <summary>
        /// Verify that an item list reference *with a separator* produces a scalar when itemized
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void ItemizeItemVectorWithSeparator()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            Match disposableMatch;
            BuildItemGroup compileItems = ItemExpander.ItemizeItemVector("@(Compile, ' ')", null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup, out disposableMatch);

            // @(Compile, ' ') is a scalar, so we should only have one item in the resulting item group
            Assertion.AssertEquals(1, compileItems.Count);
            Assertion.Assert(compileItems[0].Include == "a.cs b.cs");
        }

        /// <summary>
        /// Verify that an item list reference *with a separator* produces a scalar when itemized.
        /// This test makes sure that bucketed items take precedence over the full set of items in the project.
        /// </summary>
        /// <owner>LukaszG</owner>
        [Test]
        public void ItemizeItemVectorWithSeparatorBucketed()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();
            Lookup lookup = LookupHelpers.CreateLookup(itemGroupsByType);

            lookup.EnterScope();
            lookup.PopulateWithItem(new BuildItem("Compile", "c.cs"));
            lookup.PopulateWithItem(new BuildItem("Compile", "d.cs"));

            Match disposableMatch;
            BuildItemGroup compileItems = ItemExpander.ItemizeItemVector("@(Compile, ' ')", null, new ReadOnlyLookup(lookup), out disposableMatch);

            // @(Compile, ' ') is a scalar, so we should only have one item in the resulting item group
            Assertion.AssertEquals(1, compileItems.Count);
            Assertion.Assert(compileItems[0].Include == "c.cs d.cs");
        }

        /// <summary>
        /// Regression test for bug 534115.  Using an item separator when there are no items in the list.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ItemizeItemVectorWithSeparatorWithZeroItems1()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            Match disposableMatch;
            BuildItemGroup zeroItems = ItemExpander.ItemizeItemVector("@(ItemThatDoesNotExist, ' ')", null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup, out disposableMatch);

            Assertion.AssertEquals(0, zeroItems.Count);
        }

        /// <summary>
        /// Regression test for bug 534115.  Using an item separator when there are no items in the list.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ItemizeItemVectorWithSeparatorWithZeroItems2()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <exists Include=`foo`/>
                    </ItemGroup>

                    <Target Name=`t`>

                        <!-- REPRO 1 -->
                        <CreateProperty Value=`@(doesntexist, ' ')`>
	                        <Output TaskParameter=`Value` ItemName=`zz`/>
                        </CreateProperty>

                        <!-- REPRO 2-->
                        <CreateProperty Value=`@(exists->'', '!')`>
	                        <Output TaskParameter=`Value` ItemName=`zz`/>
                        </CreateProperty>

                        <Message Text=`zz=[@(zz)]`/>
                    </Target>

                </Project>
                ");

            logger.AssertLogContains("zz=[]");
        }

        // Valid names. The goal here is that an item name is recognized iff it is valid 
        // in a project. (That is, iff it matches "[A-Za-z_][A-Za-z_0-9\-]*")
        private string[] validItemVectors = new string[]
        {
            "@(   a1234567890_-AXZaxz   )", 
            "@(z1234567890_-AZaz)", 
            "@(A1234567890_-AZaz)", 
            "@(Z1234567890_-AZaz)", 
            "@(x1234567890_-AZaz)", 
            "@(_X)", 
            "@(a)", 
            "@(_)"
        };

        // Invalid item names
        private string[] invalidItemVectors = new string[]
        {
            "@(Com pile)",
            "@(Com.pile)",
            "@(Com%pile)",
            "@(Com:pile)",
            "@(.Compile)",
            "@(%Compile)",
            "@(:Compile)",
            "@(-Compile)",
            "@(1Compile)",
            "@()",
            "@( )"
        };

        private string[] validMetadataExpressions = new string[]
        {
            "%(   a1234567890_-AXZaxz.a1234567890_-AXZaxz   )", 
            "%(z1234567890_-AZaz.z1234567890_-AZaz)", 
            "%(A1234567890_-AZaz.A1234567890_-AZaz)", 
            "%(Z1234567890_-AZaz.Z1234567890_-AZaz)", 
            "%(x1234567890_-AZaz.x1234567890_-AZaz)", 
            "%(abc._X)", 
            "%(a12.a)", 
            "%(x._)",
            "%(a1234567890_-AXZaxz)", 
            "%(z1234567890_-AZaz)", 
            "%(A1234567890_-AZaz)", 
            "%(Z1234567890_-AZaz)", 
            "%(x1234567890_-AZaz)", 
            "%(_X)", 
            "%(a)", 
            "%(_)"
        };

        private string[] invalidMetadataExpressions = new string[]
        {
            "%(Com pile.Com pile)",
            "%(Com.pile.Com.pile)",
            "%(Com%pile.Com%pile)",
            "%(Com:pile.Com:pile)",
            "%(.Compile)",
            "%(Compile.)",
            "%(%Compile.%Compile)",
            "%(:Compile.:Compile)",
            "%(-Compile.-Compile)",
            "%(1Compile.1Compile)",
            "%()",
            "%(.)",
            "%( )",
            "%(Com pile)",
            "%(Com%pile)",
            "%(Com:pile)",
            "%(.Compile)",
            "%(%Compile)",
            "%(:Compile)",
            "%(-Compile)",
            "%(1Compile)"
        };

        private string[] validItemVectorsWithTransforms = new string[]
        {
            "@(z1234567890_-AXZaxz  -> '%(a1234567890_-AXZaxz).%(adfas)'   )", 
            "@(a1234567890_-AZaz->'z1234567890_-AZaz')", 
            "@(A1234567890_-AZaz ->'A1234567890_-AZaz')", 
            "@(Z1234567890_-AZaz -> 'Z1234567890_-AZaz')", 
            "@(x1234567890_-AZaz->'x1234567890_-AZaz')", 
            "@(_X->'_X')", 
            "@(a->'a')", 
            "@(_->'@#$%$%^&*&*)')"
        };

        private string[] validItemVectorsWithSeparators = new string[]
        {
            "@(a1234567890_-AXZaxz  , 'z123%%4567890_-AXZaxz'   )", 
            "@(z1234567890_-AZaz,'a1234567890_-AZaz')", 
            "@(A1234567890_-AZaz,'!@#$%^&*)(_+'))", 
            "@(_X,'X')", 
            "@(a  ,  'a')", 
            "@(_,'@#$%$%^&*&*)')"
        };

        private string[] validItemVectorsWithTransformsAndSeparators = new string[]
        {
            "@(a1234567890_-AXZaxz  -> 'a1234567890_-AXZaxz'   ,  'z1234567890_-AXZaxz'   )", 
            "@(z1234567890_-AZaz->'z1234567890_-AZaz','a1234567890_-AZaz')", 
            "@(A1234567890_-AZaz ->'A1234567890_-AZaz' , '!@#$%^&*)(_+'))", 
            "@(_X->'_X','X')", 
            "@(a->'a'  ,  'a')", 
            "@(_->'@#$%$%^&*&*)','@#$%$%^&*&*)')"
        };

        private string[] invalidItemVectorsWithTransforms = new string[]
        {
            "@(z123456.7890_-AXZaxz  -> '%(a1234567890_-AXZaxz).%(adfas)'  )", 
            "@(a1234:567890_-AZaz->'z1234567890_-AZaz')", 
            "@(.A1234567890_-AZaz ->'A1234567890_-AZaz')", 
            "@(:Z1234567890_-AZaz -> 'Z1234567890_-AZaz')", 
            "@(x123 4567890_-AZaz->'x1234567890_-AZaz')", 
            "@(-x->'_X')", 
            "@(1->'a')", 
            "@(1x->'@#$%$%^&*&*)')"
        };

        /// <summary>
        /// Ensure that valid item list expressions are matched.
        /// This tests "itemVectorPattern".
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ItemizeItemVectorsWithValidNames()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            foreach (string candidate in validItemVectors)
            {
                Assertion.AssertNotNull(candidate, ItemExpander.ItemizeItemVector(candidate, null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup));
            }
            foreach (string candidate in validItemVectorsWithTransforms)
            {
                Assertion.AssertNotNull(candidate, ItemExpander.ItemizeItemVector(candidate, null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup));
            }
        }

        /// <summary>
        /// Ensure that leading sand trailing pace is ignored for the item name
        /// This tests "itemVectorPattern". 
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ItemizeItemVectorsWithLeadingAndTrailingSpaces()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();
            
            // Spaces around are fine, but it's ignored for the item name
            BuildItemGroup items = ItemExpander.ItemizeItemVector("@(  Compile    )", null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals("Resulting item group should have 2 items", 2, items.Count);
        }

        /// <summary>
        /// Ensure that invalid item list expressions are not matched.
        /// This tests "itemVectorPattern". 
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ItemizeItemVectorsWithInvalidNames()
        {
            Hashtable itemGroupsByType = this.GenerateTestItems();

            // First, verify that a valid but simply non-existent item list returns an empty BuildItemGroup, 
            // not just null.
            BuildItemGroup control = ItemExpander.ItemizeItemVector("@(nonexistent)", null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup);
            Assertion.AssertEquals(0, control.Count);

            foreach (string candidate in invalidItemVectors)
            {
                Assertion.AssertNull(candidate, ItemExpander.ItemizeItemVector(candidate, null, LookupHelpers.CreateLookup(itemGroupsByType).ReadOnlyLookup));
            }
        }

        /// <summary>
        /// This tests "listOfItemVectorsWithoutSeparatorsPattern"
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ListOfItemVectorsWithoutSeparatorsPattern()
        {
            // Positive cases
            foreach (string candidate in validItemVectorsWithTransforms)
            {
                Assertion.Assert(candidate, ItemExpander.listOfItemVectorsWithoutSeparatorsPattern.IsMatch(candidate));
            }
            // Negative cases
            foreach (string candidate in invalidItemVectors)
            {
                Assertion.Assert(candidate, !ItemExpander.listOfItemVectorsWithoutSeparatorsPattern.IsMatch(candidate));
            }
        }

        /// <summary>
        /// This tests "itemVectorPattern"
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ItemVectorPattern()
        {
            // Positive cases
            foreach (string candidate in validItemVectorsWithTransforms)
            {
                Assertion.Assert(candidate, ItemExpander.itemVectorPattern.IsMatch(candidate));
            }
            foreach (string candidate in validItemVectorsWithSeparators)
            {
                Assertion.Assert(candidate, ItemExpander.itemVectorPattern.IsMatch(candidate));
            }
            foreach (string candidate in validItemVectorsWithTransformsAndSeparators)
            {
                Assertion.Assert(candidate, ItemExpander.itemVectorPattern.IsMatch(candidate));
            }
        }

        /// <summary>
        /// This tests "itemMetadataPattern"
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ItemMetadataPattern()
        {
            // Positive cases
            foreach (string candidate in validMetadataExpressions)
            {
                Assertion.Assert(candidate, ItemExpander.itemMetadataPattern.IsMatch(candidate));
            }
            // Negative cases
            foreach (string candidate in invalidMetadataExpressions)
            {
                Assertion.Assert(candidate, !ItemExpander.itemMetadataPattern.IsMatch(candidate));
            }
        }

        // ProjectWriter has regular expressions that must match these same expressions. Test them here,
        // too, so we can re-use the same sample expressions.

        /// <summary>
        /// ItemVectorTransformPattern should match any item expressions containing transforms.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ProjectWriterItemVectorTransformPattern()
        {
            // Positive cases
            foreach (string candidate in validItemVectorsWithTransforms)
            {
                Assertion.Assert(candidate, ProjectWriter.itemVectorTransformPattern.IsMatch(candidate));
            }
            foreach (string candidate in validItemVectorsWithTransformsAndSeparators)
            {
                Assertion.Assert(candidate, ProjectWriter.itemVectorTransformPattern.IsMatch(candidate));
            }
        }

        /// <summary>
        /// itemVectorTransformRawPattern should match any item expressions containing transforms.
        /// </summary>
        /// <owner>danmose</owner>
        [Test]
        public void ProjectWriterItemVectorTransformRawPattern()
        {
            // Positive cases
            foreach (string candidate in validItemVectorsWithTransforms)
            {
                Assertion.Assert(candidate, ProjectWriter.itemVectorTransformRawPattern.IsMatch(candidate));
            }
            foreach (string candidate in validItemVectorsWithTransformsAndSeparators)
            {
                Assertion.Assert(candidate, ProjectWriter.itemVectorTransformRawPattern.IsMatch(candidate));
            }
            // Negative cases
            foreach (string candidate in invalidItemVectorsWithTransforms)
            {
                Assertion.Assert(candidate, !ProjectWriter.itemVectorTransformRawPattern.IsMatch(candidate));
            }
        }

    }
}
