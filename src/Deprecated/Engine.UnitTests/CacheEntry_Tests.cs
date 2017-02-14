// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

using NUnit.Framework;
using Microsoft.Build.Framework;
using Microsoft.Build.BuildEngine;

namespace Microsoft.Build.UnitTests
{
    [TestFixture]
    public class CacheEntry_Tests
    {
        /// <summary>
        /// Exercise the public constructors and getters
        /// </summary>
        [Test]
        public void CacheEntryGetters()
        {
            BuildItem[] buildItems = new BuildItem[2] { null, null };

            BuildItemCacheEntry tice = new BuildItemCacheEntry("tice", buildItems);
            Assertion.AssertEquals("tice", tice.Name);
            Assertion.AssertEquals(buildItems, tice.BuildItems);

            PropertyCacheEntry pce = new PropertyCacheEntry("pce", "propertyValue");
            Assertion.AssertEquals("pce", pce.Name);
            Assertion.AssertEquals("propertyValue", pce.Value);

            BuildResultCacheEntry brce = new BuildResultCacheEntry("brce", buildItems, true);
            Assertion.AssertEquals("brce", brce.Name);
            Assertion.AssertEquals(buildItems, brce.BuildItems);
            Assertion.AssertEquals(true, brce.BuildResult);
        }

        [Test]
        public void CacheEntryGettersDefaultConstructors()
        {
            BuildItem[] buildItems = new BuildItem[2] { null, null };

            BuildItemCacheEntry tice = new BuildItemCacheEntry();
            Assertion.AssertEquals(null, tice.Name);
            Assertion.AssertEquals(null, tice.BuildItems);
            
            tice.Name = "tice";
            tice.BuildItems = buildItems;
            Assertion.AssertEquals("tice", tice.Name);
            Assertion.AssertEquals(buildItems, tice.BuildItems);

            PropertyCacheEntry pce = new PropertyCacheEntry();
            Assertion.AssertEquals(null, pce.Name);
            Assertion.AssertEquals(null, pce.Value);

            pce.Name = "pce";
            pce.Value = "propertyValue";
            Assertion.AssertEquals("pce", pce.Name);
            Assertion.AssertEquals("propertyValue", pce.Value);

            BuildResultCacheEntry brce = new BuildResultCacheEntry();
            Assertion.AssertEquals(null, brce.Name);
            Assertion.AssertEquals(null, brce.BuildItems);
            Assertion.AssertEquals(default(bool), brce.BuildResult);

            brce.Name = "brce";
            brce.BuildItems = buildItems;
            brce.BuildResult = false;
            Assertion.AssertEquals("brce", brce.Name);
            Assertion.AssertEquals(buildItems, brce.BuildItems);
            Assertion.AssertEquals(false, brce.BuildResult);
        }

        [Test]
        public void IsEquivalentProperty()
        {
            PropertyCacheEntry e = new PropertyCacheEntry("name", "value");
            
            Assert.IsFalse(e.IsEquivalent(null));
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry()));
            Assert.IsFalse(e.IsEquivalent(new PropertyCacheEntry()));
            Assert.IsFalse(e.IsEquivalent(new PropertyCacheEntry("naame", "value")));
            Assert.IsFalse(e.IsEquivalent(new PropertyCacheEntry("name", "valuue")));
            Assert.IsTrue(e.IsEquivalent(new PropertyCacheEntry("name", "value")));
        }

        [Test]
        public void IsEquivalentTaskItem()
        {
            BuildItem bi = new BuildItem("itemname", "itemspec");
            bi.SetMetadata("mn", "mv");

            BuildItemCacheEntry e = new BuildItemCacheEntry("name", new BuildItem[] { bi });

            Assert.IsFalse(e.IsEquivalent(null));
            Assert.IsFalse(e.IsEquivalent(new PropertyCacheEntry()));
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry()));
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry("naame", new BuildItem[] { bi })));
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry("name", null)));
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry("name", new BuildItem[] { null })));
            Assert.IsFalse(new BuildItemCacheEntry("name", new BuildItem[] { null }).IsEquivalent(e));
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry("name", new BuildItem[] { new BuildItem("itemname", "itemspec") })));
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry("name", new BuildItem[] { bi, bi })));

            BuildItem bi2 = new BuildItem("itemname", "itemspec");
            bi2.SetMetadata("n", "v");
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry("name", new BuildItem[] { bi2 })));
            bi2.SetMetadata("mn", "mv");
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry("name", new BuildItem[] { bi2 })));

            BuildItem bi3 = new BuildItem("itemname", "itemspec");
            bi3.SetMetadata("mn", "mv");
            Assert.IsTrue(e.IsEquivalent(new BuildItemCacheEntry("name", new BuildItem[] { bi3 })));
        }

        [Test]
        public void IsEquivalentBuildResult()
        {
            BuildResultCacheEntry e = new BuildResultCacheEntry("name", null, false);

            Assert.IsFalse(e.IsEquivalent(null));
            Assert.IsFalse(e.IsEquivalent(new PropertyCacheEntry("name", "value")));
            Assert.IsFalse(e.IsEquivalent(new BuildItemCacheEntry("name", null)));
            Assert.IsFalse(e.IsEquivalent(new BuildResultCacheEntry()));
            Assert.IsFalse(e.IsEquivalent(new BuildResultCacheEntry("naame", null, false)));
            Assert.IsFalse(e.IsEquivalent(new BuildResultCacheEntry("name", null, true)));
            Assert.IsTrue(e.IsEquivalent(new BuildResultCacheEntry("name", null, false)));
        }


        [Test]
        public void TestCacheEntryCustomSerialization()
        {
            // Stream, writer and reader where the events will be serialized and deserialized from
            MemoryStream stream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(stream);
            BinaryReader reader = new BinaryReader(stream);
            try
            {
                BuildItem buildItem1 = new BuildItem("BuildItem1", "Item1");
                BuildItem buildItem2 = new BuildItem("BuildItem2", "Item2");
                buildItem1.Include = "TestInclude1";
                buildItem2.Include = "TestInclude2";
                BuildItem[] buildItems = new BuildItem[2];
                buildItems[0] = buildItem1;
                buildItems[1] = buildItem2;

                BuildItemCacheEntry buildItemEntry = new BuildItemCacheEntry("Badger", buildItems);
                BuildResultCacheEntry buildResultEntry = new BuildResultCacheEntry("Koi", buildItems, true);
                PropertyCacheEntry propertyEntry = new PropertyCacheEntry("Seagull", "bread");
                
                stream.Position = 0;
                // Serialize
                buildItemEntry.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                long streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                BuildItemCacheEntry newCacheEntry = new BuildItemCacheEntry();
                newCacheEntry.CreateFromStream(reader);
                long streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream End Positions Should Match");
                Assert.IsTrue(string.Compare(newCacheEntry.Name, buildItemEntry.Name, StringComparison.OrdinalIgnoreCase) == 0);
                BuildItem[] buildItemArray = newCacheEntry.BuildItems;
                Assert.IsTrue(buildItemArray.Length == 2);
                Assert.IsTrue(string.Compare(buildItemArray[0].Include, buildItem1.Include, StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsTrue(string.Compare(buildItemArray[1].Include, buildItem2.Include, StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsTrue(string.Compare(buildItemArray[1].Name, buildItem2.Name, StringComparison.OrdinalIgnoreCase) == 0);


                stream.Position = 0;
                // Serialize
                buildResultEntry.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                BuildResultCacheEntry newCacheEntryBuildResult = new BuildResultCacheEntry();
                newCacheEntryBuildResult.CreateFromStream(reader);
                streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream End Positions Should Match");
                Assert.IsTrue(string.Compare(newCacheEntryBuildResult.Name, buildResultEntry.Name, StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsTrue(buildResultEntry.BuildResult == newCacheEntryBuildResult.BuildResult);
                buildItemArray = newCacheEntryBuildResult.BuildItems;
                Assert.IsTrue(buildItemArray.Length == 2);
                Assert.IsTrue(string.Compare(buildItemArray[0].Include, buildItem1.Include, StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsTrue(string.Compare(buildItemArray[1].Include, buildItem2.Include, StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsTrue(string.Compare(buildItemArray[1].Name, buildItem2.Name, StringComparison.OrdinalIgnoreCase) == 0);


                stream.Position = 0;
                // Serialize
                propertyEntry.WriteToStream(writer);
                // Get position of stream after write so it can be compared to the position after read
                streamWriteEndPosition = stream.Position;

                // Deserialize and Verify
                stream.Position = 0;
                PropertyCacheEntry newPropertyCacheEntry = new PropertyCacheEntry();
                newPropertyCacheEntry.CreateFromStream(reader);
                streamReadEndPosition = stream.Position;
                Assert.IsTrue(streamWriteEndPosition == streamReadEndPosition, "Stream End Positions Should Match");
                Assert.IsTrue(string.Compare(newPropertyCacheEntry.Name, propertyEntry.Name, StringComparison.OrdinalIgnoreCase) == 0);
                Assert.IsTrue(string.Compare(newPropertyCacheEntry.Value, propertyEntry.Value, StringComparison.OrdinalIgnoreCase) == 0);
            }
            finally
            {
                // Close will close the writer/reader and the underlying stream
                writer.Close();
                reader.Close();
                reader = null;
                stream = null;
                writer = null;
            }
        }
    }
}
