// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Collections;
using System.Text.RegularExpressions;

using NUnit.Framework;

using Microsoft.Build.BuildEngine;
using System.Collections.Generic;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class BatchingEngineTests
    {
        /// <summary>
        /// Helper method so we can keep the real Expander.ExpandItemsIntoString private.
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private static string ExpandItemsIntoString
            (
            ItemBucket bucket,
            string expression
            )
        {
            
            Expander itemExpander = new Expander(new ReadOnlyLookup(bucket.Lookup), null, ExpanderOptions.ExpandItems);
            return itemExpander.ExpandAllIntoString(expression, (new XmlDocument()).CreateAttribute("foo"));
        }

        /// <summary>
        /// Helper method so we can keep the real Expander.ExpandMetadataAndProperties private.
        /// </summary>
        /// <param name="bucket"></param>
        /// <param name="expression"></param>
        /// <returns></returns>
        /// <owner>RGoel</owner>
        private static string ExpandMetadataAndProperties
            (
            ItemBucket bucket,
            string expression
            )
        {
            Expander itemExpander = new Expander(bucket.Expander, ExpanderOptions.ExpandPropertiesAndMetadata);
            return itemExpander.ExpandAllIntoString(expression, (new XmlDocument()).CreateAttribute("foo"));
        }

        [Test]
        public void GetBuckets()
        {
            List<string> parameters = new List<string>();
            parameters.Add("@(File);$(unittests)");
            parameters.Add("$(obj)\\%(Filename).ext");
            parameters.Add("@(File->'%(extension)')");  // attributes in transforms don't affect batching

            Hashtable itemsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildItemGroup items = new BuildItemGroup();
            items.AddNewItem("File", "a.foo");
            items.AddNewItem("File", "b.foo");
            items.AddNewItem("File", "c.foo");
            items.AddNewItem("File", "d.foo");
            items.AddNewItem("File", "e.foo");
            itemsByType["FILE"] = items;

            items = new BuildItemGroup();
            items.AddNewItem("Doc", "a.doc");
            items.AddNewItem("Doc", "b.doc");
            items.AddNewItem("Doc", "c.doc");
            items.AddNewItem("Doc", "d.doc");
            items.AddNewItem("Doc", "e.doc");
            itemsByType["DOC"] = items;

            BuildPropertyGroup properties = new BuildPropertyGroup();
            properties.SetProperty("UnitTests", "unittests.foo");
            properties.SetProperty("OBJ", "obj");

            ArrayList buckets = BatchingEngine.PrepareBatchingBuckets(new XmlDocument().CreateElement("Foo"), parameters, CreateLookup(itemsByType, properties));

            Assertion.AssertEquals(5, buckets.Count);

            foreach (ItemBucket bucket in buckets)
            {
                // non-batching data -- same for all buckets
                XmlAttribute tempXmlAttribute = (new XmlDocument()).CreateAttribute("attrib");
                tempXmlAttribute.Value = "'$(Obj)'=='obj'";

                Assertion.Assert(BuildEngine.Utilities.EvaluateCondition(tempXmlAttribute.Value,
                    tempXmlAttribute, bucket.Expander, null, ParserOptions.AllowAll, null, null));
                Assertion.AssertEquals("a.doc;b.doc;c.doc;d.doc;e.doc", ExpandItemsIntoString(bucket, "@(doc)"));
                Assertion.AssertEquals("unittests.foo", ExpandMetadataAndProperties(bucket, "$(bogus)$(UNITTESTS)"));
            }

            Assertion.AssertEquals("a.foo", ExpandItemsIntoString((ItemBucket)buckets[0], "@(File)"));
            Assertion.AssertEquals(".foo", ExpandItemsIntoString((ItemBucket)buckets[0], "@(File->'%(Extension)')"));
            Assertion.AssertEquals("obj\\a.ext", ExpandMetadataAndProperties((ItemBucket)buckets[0], "$(obj)\\%(Filename).ext"));

            // we weren't batching on this attribute, so it has no value
            Assertion.AssertEquals(String.Empty, ExpandMetadataAndProperties((ItemBucket)buckets[0], "%(Extension)"));

            items = ((ItemBucket)buckets[0]).Expander.ExpandSingleItemListExpressionIntoItemsLeaveEscaped("@(file)", null);
            Assertion.AssertNotNull(items);
            Assertion.AssertEquals(1, items.Count);

            int invalidProjectFileExceptions = 0;
            try
            {
                // This should throw because we don't allow item lists to be concatenated
                // with other strings.
                items = ((ItemBucket)buckets[0]).Expander.ExpandSingleItemListExpressionIntoItemsLeaveEscaped("@(file);$(unitests)", null);
            }
            catch (InvalidProjectFileException)
            {
                invalidProjectFileExceptions++;
            }

            // We do allow separators in item vectors, this results in an item group with a single flattened item
            items = ((ItemBucket)buckets[0]).Expander.ExpandSingleItemListExpressionIntoItemsLeaveEscaped("@(file, ',')", null);
            Assertion.AssertNotNull(items);
            Assertion.AssertEquals(1, items.Count);
            Assertion.AssertEquals("a.foo", items[0].FinalItemSpec);

            Assertion.AssertEquals(1, invalidProjectFileExceptions);
        }

        /// <summary>
        /// Tests the real simple case of using an unqualified metadata reference %(Culture),
        /// where there are only two items and both of them have a value for Culture, but they
        /// have different values.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void ValidUnqualifiedMetadataReference()
        {
            List<string> parameters = new List<string>();
            parameters.Add("@(File)");
            parameters.Add("%(Culture)");

            Hashtable itemsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildItemGroup items = new BuildItemGroup();
            itemsByType["FILE"] = items;

            BuildItem a = items.AddNewItem("File", "a.foo");
            BuildItem b = items.AddNewItem("File", "b.foo");
            a.SetMetadata("Culture", "fr-fr");
            b.SetMetadata("Culture", "en-en");

            BuildPropertyGroup properties = new BuildPropertyGroup();

            ArrayList buckets = BatchingEngine.PrepareBatchingBuckets(new XmlDocument().CreateElement("Foo"), parameters, CreateLookup(itemsByType, properties));
            Assertion.AssertEquals(2, buckets.Count);
        }

        /// <summary>
        /// Tests the case where an unqualified metadata reference is used illegally.
        /// It's illegal because not all of the items consumed contain a value for
        /// that metadata.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void InvalidUnqualifiedMetadataReference()
        {
            List<string> parameters = new List<string>();
            parameters.Add("@(File)");
            parameters.Add("%(Culture)");

            Hashtable itemsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);

            BuildItemGroup items = new BuildItemGroup();
            itemsByType["FILE"] = items;

            BuildItem a = items.AddNewItem("File", "a.foo");
            BuildItem b = items.AddNewItem("File", "b.foo");
            a.SetMetadata("Culture", "fr-fr");

            BuildPropertyGroup properties = new BuildPropertyGroup();

            // This is expected to throw because not all items contain a value for metadata "Culture".
            // Only a.foo has a Culture metadata.  b.foo does not.
            ArrayList buckets = BatchingEngine.PrepareBatchingBuckets(new XmlDocument().CreateElement("Foo"), parameters, CreateLookup(itemsByType, properties));
        }

        /// <summary>
        /// Tests the case where an unqualified metadata reference is used illegally.
        /// It's illegal because not all of the items consumed contain a value for
        /// that metadata.
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        [ExpectedException(typeof(InvalidProjectFileException))]
        public void NoItemsConsumed()
        {
            List<string> parameters = new List<string>();
            parameters.Add("$(File)");
            parameters.Add("%(Culture)");

            Hashtable itemsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);
            BuildPropertyGroup properties = new BuildPropertyGroup();

            // This is expected to throw because we have no idea what item list %(Culture) refers to.
            ArrayList buckets = BatchingEngine.PrepareBatchingBuckets(new XmlDocument().CreateElement("Foo"), parameters, CreateLookup(itemsByType, properties));
        }

        /// <summary>
        /// Missing unittest found by mutation testing.
        /// REASON TEST WASN'T ORIGINALLY PRESENT: Missed test.
        /// 
        /// This test ensures that two items with duplicate attributes end up in exactly one batching
        /// bucket.
        /// </summary>
        [Test]
        public void Regress_Mutation_DuplicateBatchingBucketsAreFoldedTogether()
        {
            List<string> parameters = new List<string>();
            parameters.Add("%(File.Culture)");

            Hashtable itemsByType = new Hashtable(StringComparer.OrdinalIgnoreCase);
            
            BuildItemGroup items = new BuildItemGroup();
            items.AddNewItem("File", "a.foo");
            items.AddNewItem("File", "b.foo"); // Need at least two items for this test case to ensure multiple buckets might be possible
            itemsByType["FILE"] = items;

            BuildPropertyGroup properties = new BuildPropertyGroup();

            ArrayList buckets = BatchingEngine.PrepareBatchingBuckets(new XmlDocument().CreateElement("Foo"), parameters, CreateLookup(itemsByType, properties));

            // If duplicate buckes have been folded correctly, then there will be exactly one bucket here
            // containing both a.foo and b.foo.
            Assertion.AssertEquals(1, buckets.Count);
        }

        [Test]
        public void Simple()
        {
            MockLogger log = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <AToB Include=`a;b`/>
                    </ItemGroup>

                    <Target Name=`Build`>
                        <CreateItem Include=`%(AToB.Identity)`>
                            <Output ItemName=`AToBBatched` TaskParameter=`Include`/>
                        </CreateItem>
                        <Message Text=`[AToBBatched: @(AToBBatched)]`/>
                    </Target>

                </Project>
                ");

            log.AssertLogContains("[AToBBatched: a;b]");
        }

        /// <summary>
        /// Regression test for bug 528104.  It is important that the batching engine invokes
        /// the different batches in the same order as the items are declared in the project, especially
        /// when batching is simply being used as a "for loop".
        /// </summary>
        /// <owner>RGoel</owner>
        [Test]
        public void BatcherPreservesItemOrderWithinASingleItemList()
        {
            MockLogger log = ObjectModelHelpers.BuildProjectExpectSuccess(@"
                <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>

                    <ItemGroup>
                        <AToZ Include=`a;b;c;d;e;f;g;h;i;j;k;l;m;n;o;p;q;r;s;t;u;v;w;x;y;z`/>
                        <ZToA Include=`z;y;x;w;v;u;t;s;r;q;p;o;n;m;l;k;j;i;h;g;f;e;d;c;b;a`/>
                    </ItemGroup>

                    <Target Name=`Build`>
                        <CreateItem Include=`%(AToZ.Identity)`>
                            <Output ItemName=`AToZBatched` TaskParameter=`Include`/>
                        </CreateItem>
                        <CreateItem Include=`%(ZToA.Identity)`>
                            <Output ItemName=`ZToABatched` TaskParameter=`Include`/>
                        </CreateItem>
                        <Message Text=`AToZBatched: @(AToZBatched)`/>
                        <Message Text=`ZToABatched: @(ZToABatched)`/>
                    </Target>

                </Project>
                ");

            log.AssertLogContains("AToZBatched: a;b;c;d;e;f;g;h;i;j;k;l;m;n;o;p;q;r;s;t;u;v;w;x;y;z");
            log.AssertLogContains("ZToABatched: z;y;x;w;v;u;t;s;r;q;p;o;n;m;l;k;j;i;h;g;f;e;d;c;b;a");
        }

        private static Lookup CreateLookup(Hashtable itemsByType, BuildPropertyGroup properties)
        {
            return new Lookup(itemsByType, properties, new ItemDefinitionLibrary(new Project()));
        }
    }
}
