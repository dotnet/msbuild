// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.UnitTests.BackEnd;
using Xunit;
using ProjectInstanceItemSpec =
    Microsoft.Build.Evaluation.ItemSpec<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;
using ProjectInstanceExpander =
    Microsoft.Build.Evaluation.Expander<Microsoft.Build.Execution.ProjectPropertyInstance, Microsoft.Build.Execution.ProjectItemInstance>;

namespace Microsoft.Build.UnitTests.OM.Evaluation
{
    public class ItemSpec_Tests
    {
        [Fact]
        public void EachFragmentTypeShouldContributeToItemSpecGlob()
        {
            var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> {{"foo", new[] {"d", "e"}}}));

            var itemSpecGlob = itemSpec.ToMSBuildGlob();

            Assert.True(itemSpecGlob.IsMatch("a"));
            Assert.True(itemSpecGlob.IsMatch("bar"));
            Assert.True(itemSpecGlob.IsMatch("car"));
            Assert.True(itemSpecGlob.IsMatch("d"));
            Assert.True(itemSpecGlob.IsMatch("e"));
        }

        [Fact]
        public void FragmentGlobsWorkAfterStateIsPartiallyInitializedByOtherOperations()
        {
            var itemSpec = CreateItemSpecFrom("a;b*;c*;@(foo)", CreateExpander(new Dictionary<string, string[]> {{"foo", new[] {"d", "e"}}}));

            int matches;
            // cause partial Lazy state to initialize in the ItemExpressionFragment
            itemSpec.FragmentsMatchingItem("e", out matches);

            Assert.Equal(1, matches);

            var itemSpecGlob = itemSpec.ToMSBuildGlob();

            Assert.True(itemSpecGlob.IsMatch("a"));
            Assert.True(itemSpecGlob.IsMatch("bar"));
            Assert.True(itemSpecGlob.IsMatch("car"));
            Assert.True(itemSpecGlob.IsMatch("d"));
            Assert.True(itemSpecGlob.IsMatch("e"));
        }

        private ProjectInstanceItemSpec CreateItemSpecFrom(string itemSpec, ProjectInstanceExpander expander)
        {
            return new ProjectInstanceItemSpec(itemSpec, expander, MockElementLocation.Instance);
        }

        private ProjectInstanceExpander CreateExpander(Dictionary<string, string[]> items)
        {
            var itemDictionary = ToItemDictionary(items);

            return new ProjectInstanceExpander(new PropertyDictionary<ProjectPropertyInstance>(), itemDictionary);
        }

        private static ItemDictionary<ProjectItemInstance> ToItemDictionary(Dictionary<string, string[]> itemTypes)
        {
            var itemDictionary = new ItemDictionary<ProjectItemInstance>();

            var dummyProject = ProjectHelpers.CreateEmptyProjectInstance();

            foreach (var itemType in itemTypes)
            {
                foreach (var item in itemType.Value)
                {
                    itemDictionary.Add(new ProjectItemInstance(dummyProject, itemType.Key, item, dummyProject.FullPath));
                }
            }

            return itemDictionary;
        }
    }
}