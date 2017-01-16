// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for evaluation</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Build.Evaluation;
using Xunit;
using System.Text;

namespace Microsoft.Build.UnitTests.Evaluation
{
    /// <summary>
    /// Tests mainly for project evaluation
    /// </summary>
    public class ItemEvaluation_Tests : IDisposable
    {
        /// <summary>
        /// Cleanup
        /// </summary>
        public ItemEvaluation_Tests()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        /// <summary>
        /// Cleanup
        /// </summary>
        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
            GC.Collect();
        }

        [Fact]
        public void IncludeShouldPreserveIntermediaryReferences()
        {
            var content = @"
                            <i2 Include='a;b;c'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i2>

                            <i Include='@(i2)'/>

                            <i2 Include='d;e;f;@(i2)'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                            </i2>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

            var mI2_1 = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"},
            };

            var itemsForI = items.Where(i => i.ItemType == "i").ToList();
            ObjectModelHelpers.AssertItems(new [] {"a", "b", "c"}, itemsForI, mI2_1);

            var mI2_2 = new Dictionary<string, string>
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"},
            };

            var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
            ObjectModelHelpers.AssertItems(
                new[] { "a", "b", "c", "d", "e", "f", "a", "b", "c" },
                itemsForI2,
                new [] { mI2_1, mI2_1 , mI2_1, mI2_2, mI2_2, mI2_2, mI2_2, mI2_2, mI2_2 });
        }

        [Theory]
        // remove the items by referencing each one
        [InlineData(
            @"
            <i2 Include='a;b;c'>
                <m1>m1_contents</m1>
                <m2>m2_contents</m2>
            </i2>

            <i Include='@(i2)'/>

            <i2 Remove='a;b;c'/>"
            )]
        // remove the items via a glob
        [InlineData(
            @"
            <i2 Include='a;b;c'>
                <m1>m1_contents</m1>
                <m2>m2_contents</m2>
            </i2>

            <i Include='@(i2)'/>

            <i2 Remove='*'/>"
            )]
        public void RemoveShouldPreserveIntermediaryReferences(string content)
        {
            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

            var expectedMetadata = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"}
            };
            
            var itemsForI = items.Where(i => i.ItemType == "i").ToList();
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, itemsForI, expectedMetadata);

            var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
            ObjectModelHelpers.AssertItems(new string[0], itemsForI2);
        }

        [Fact]
        public void UpdateShouldPreserveIntermediaryReferences()
        {
            var content = @"
                            <i2 Include='a;b;c'>
                                <m1>m1_contents</m1>
                                <m2>%(Identity)</m2>
                            </i2>

                            <i Include='@(i2)'>
                                <m3>@(i2 -> '%(m2)')</m3>
                                <m4 Condition=""'@(i2 -> '%(m2)')' == 'a;b;c'"">m4_contents</m4>
                            </i>

                            <i2 Update='a;b;c'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                                <m3>m3_updated</m3>
                                <m4>m4_updated</m4>
                            </i2>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);


            var a = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "a"},
                {"m3", "a;b;c"},
                {"m4", "m4_contents"},
            };

            var b = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "b"},
                {"m3", "a;b;c"},
                {"m4", "m4_contents"}
            };

            var c = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "c"},
                {"m3", "a;b;c"},
                {"m4", "m4_contents"},
            };

            var itemsForI = items.Where(i => i.ItemType == "i").ToList();
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, itemsForI, new [] {a, b, c});

            var metadataForI2 = new Dictionary<string, string>()
            {
                {"m1", "m1_updated"},
                {"m2", "m2_updated"},
                {"m3", "m3_updated"},
                {"m4", "m4_updated"}
            };

            var itemsForI2 = items.Where(i => i.ItemType == "i2").ToList();
            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, itemsForI2, metadataForI2);
        }

        [Fact]
        public void MultipleInterItemDependenciesOnSameItemOperation()
        {
            var content = @"
                            <i1 Include='i1_1;i1_2;i1_3;i1_4;i1_5'/>
                            <i1 Update='*'>
                                <m>i1</m>
                            </i1>
                            <i1 Remove='*i1_5'/>

                            <i_cond Condition='@(i1->Count()) == 4' Include='i1 has 4 items'/>

                            <i2 Include='@(i1);i2_4'/>
                            <i2 Remove='i?_4'/>
                            <i2 Update='i?_1'>
                               <m>i2</m>
                            </i2>

                            <i3 Include='@(i1);i3_3'/>
                            <i3 Remove='*i?_3'/>

                            <i1 Remove='*i1_2'/>
                            <i1 Include='i1_6'/>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content, allItems: true);

            var i1BaseMetadata = new Dictionary<string, string>()
            {
                {"m", "i1"}
            };

            //i1 items: i1_1; i1_3; i1_4; i1_6
            var i1Metadata = new Dictionary<string, string>[]
            {
                i1BaseMetadata,
                i1BaseMetadata,
                i1BaseMetadata,
                new Dictionary<string, string>()
            };

            var i1Items = items.Where(i => i.ItemType == "i1").ToList();
            ObjectModelHelpers.AssertItems(new[] { "i1_1", "i1_3", "i1_4", "i1_6" }, i1Items, i1Metadata);

            //i2 items: i1_1; i1_2; i1_3
            var i2Metadata = new Dictionary<string, string>[]
            {
                new Dictionary<string, string>()
                {
                    {"m", "i2"}
                }, 
                i1BaseMetadata,
                i1BaseMetadata
            };

            var i2Items = items.Where(i => i.ItemType == "i2").ToList();
            ObjectModelHelpers.AssertItems(new[] { "i1_1", "i1_2", "i1_3" }, i2Items, i2Metadata);

            //i3 items: i1_1; i1_2; i1_4
            var i3Items = items.Where(i => i.ItemType == "i3").ToList();
            ObjectModelHelpers.AssertItems(new[] { "i1_1", "i1_2", "i1_4" }, i3Items, i1BaseMetadata);

            var i_condItems = items.Where(i => i.ItemType == "i_cond").ToList();
            ObjectModelHelpers.AssertItems(new[] { "i1 has 4 items" }, i_condItems);
        }

        [Fact]
        public void LongIncludeChain()
        {
            const int INCLUDE_COUNT = 10000;
            
            //  This was about the minimum count needed to repro a StackOverflowException
            //const int INCLUDE_COUNT = 4000;

            StringBuilder content = new StringBuilder();
            for (int i = 0; i < INCLUDE_COUNT; i++)
            {
                content.AppendLine($"<i Include='ItemValue{i}' />");
            }

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content.ToString());

            Assert.Equal(INCLUDE_COUNT, items.Count);
        }
    }
}
