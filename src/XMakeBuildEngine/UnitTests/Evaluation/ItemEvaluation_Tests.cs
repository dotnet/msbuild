// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for evaluation</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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

                            <i2 Include='d;e;f'>
                                <m1>m1_updated</m1>
                                <m2>m2_updated</m2>
                            </i2>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            var expectedMetadata = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"},
            };

            ObjectModelHelpers.AssertItems(new [] {"a", "b", "c"}, items, expectedMetadata);
        }

        [Fact]
        public void RemoveShouldPreserveIntermediaryReferences()
        {
            var content = @"
                            <i2 Include='a;b;c'>
                                <m1>m1_contents</m1>
                                <m2>m2_contents</m2>
                            </i2>

                            <i Include='@(i2)'/>

                            <i2 Remove='a;b;c'/>";

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

            var expectedMetadata = new Dictionary<string, string>
            {
                {"m1", "m1_contents"},
                {"m2", "m2_contents"}
            };

            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, items, expectedMetadata);
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

            IList<ProjectItem> items = ObjectModelHelpers.GetItemsFromFragment(content);

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

            ObjectModelHelpers.AssertItems(new[] { "a", "b", "c" }, items, new [] {a, b, c});
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
