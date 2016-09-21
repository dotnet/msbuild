using System.Linq;
using FluentAssertions;
using Microsoft.Build.Construction;
using Xunit;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenMSBuildExtensions
    {
        [Fact]
        public void ConditionChain_is_empty_when_element_and_parents_have_no_condition()
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.AddItemGroup();

            var item1 = itemGroup.AddItem("test", "include1");

            item1.ConditionChain().Should().HaveCount(0);
        }

        [Fact]
        public void ConditionChain_has_parent_conditions_when_element_is_empty()
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.AddItemGroup();
            itemGroup.Condition = "condition";

            var item1 = itemGroup.AddItem("test", "include1");

            item1.ConditionChain().Should().HaveCount(1);
            item1.ConditionChain().First().Should().Be("condition");
        }

        [Fact]
        public void ConditionChain_has_element_and_parent_conditions_when_they_exist()
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.AddItemGroup();
            itemGroup.Condition = "itemGroup";

            var item1 = itemGroup.AddItem("test", "include1");
            item1.Condition = "item";

            item1.ConditionChain().Should().HaveCount(2);
            item1.ConditionChain().Should().BeEquivalentTo("itemGroup", "item");
        }

        [Fact]
        public void ConditionChainsAreEquivalent_is_true_when_neither_element_or_parents_have_conditions()
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.AddItemGroup();

            var item1 = itemGroup.AddItem("test", "include1");
            var item2 = itemGroup.AddItem("test", "include2");

            item1.ConditionChainsAreEquivalent(item2).Should().BeTrue();
        }

        [Fact]
        public void ConditionChainsAreEquivalent_is_true_when_elements_have_the_same_condition()
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.AddItemGroup();

            var item1 = itemGroup.AddItem("test", "include1");
            var item2 = itemGroup.AddItem("test", "include2");
            item1.Condition = "item";
            item2.Condition = "item";

            item1.ConditionChainsAreEquivalent(item2).Should().BeTrue();
        }

        [Fact]
        public void ConditionChainsAreEquivalent_is_true_when_element_condition_matches_condition_of_other_element_parent()
        {
            var project = ProjectRootElement.Create();
            var itemGroup1 = project.AddItemGroup();
            var itemGroup2 = project.AddItemGroup();
            itemGroup1.Condition = "item";

            var item1 = itemGroup1.AddItem("test", "include1");
            var item2 = itemGroup2.AddItem("test", "include2");
            item2.Condition = "item";

            item1.ConditionChainsAreEquivalent(item2).Should().BeTrue();
        }

        [Fact]
        public void ConditionChainsAreEquivalent_is_false_when_elements_have_different_conditions()
        {
            var project = ProjectRootElement.Create();
            var itemGroup = project.AddItemGroup();

            var item1 = itemGroup.AddItem("test", "include1");
            var item2 = itemGroup.AddItem("test", "include2");
            item1.Condition = "item";
            item2.Condition = "item2";

            item1.ConditionChainsAreEquivalent(item2).Should().BeFalse();
        }

        [Fact]
        public void ConditionChainsAreEquivalent_is_false_when_other_element_parent_has_a_condition()
        {
            var project = ProjectRootElement.Create();
            var itemGroup1 = project.AddItemGroup();
            var itemGroup2 = project.AddItemGroup();
            itemGroup1.Condition = "item";

            var item1 = itemGroup1.AddItem("test", "include1");
            var item2 = itemGroup2.AddItem("test", "include2");

            item1.ConditionChainsAreEquivalent(item2).Should().BeFalse();
        }

        [Fact]
        public void ConditionChainsAreEquivalent_is_false_when_both_element_parent_conditions_dont_match()
        {
            var project = ProjectRootElement.Create();
            var itemGroup1 = project.AddItemGroup();
            var itemGroup2 = project.AddItemGroup();
            itemGroup1.Condition = "item";
            itemGroup2.Condition = "item2";

            var item1 = itemGroup1.AddItem("test", "include1");
            var item2 = itemGroup2.AddItem("test", "include2");

            item1.ConditionChainsAreEquivalent(item2).Should().BeFalse();
        }

        [Fact]
        public void GetMetadataWithName_is_case_insensitive()
        {
            var project = ProjectRootElement.Create();
            var item1 = project.AddItem("test", "include1");
            item1.AddMetadata("name", "value");

            item1.GetMetadataWithName("Name").Should().NotBeNull();
            item1.GetMetadataWithName("Name").Value.Should().Be("value");
        }

        [Fact]
        public void Includes_returns_include_value_split_by_semicolon()
        {
            var project = ProjectRootElement.Create();
            var item = project.CreateItemElement("test");
            item.Include = "include1;include2;aaa";

            var includes = item.Includes().ToArray();

            includes.Should().HaveCount(3);
            includes[0].Should().Be("include1");
            includes[1].Should().Be("include2");
            includes[2].Should().Be("aaa");
        }

        [Fact]
        public void Excludes_returns_include_value_split_by_semicolon()
        {
            var project = ProjectRootElement.Create();
            var item = project.CreateItemElement("test");
            item.Exclude = "include1;include2;aaa";

            var excludes = item.Excludes().ToArray();

            excludes.Should().HaveCount(3);
            excludes[0].Should().Be("include1");
            excludes[1].Should().Be("include2");
            excludes[2].Should().Be("aaa");
        }

        [Fact]
        public void ItemsWithoutConditions_returns_items_without_a_condition()
        {
            var project = ProjectRootElement.Create();
            var item = project.AddItem("test", "include1");

            project.ItemsWithoutConditions().Count().Should().Be(1);
            project.ItemsWithoutConditions().First().Should().Be(item);
        }

        [Fact]
        public void ItemsWithoutConditions_doesnt_return_items_with_a_condition()
        {
            var project = ProjectRootElement.Create();
            var conditionlessItems = project.AddItem("test", "include1");
            var conditionItem = project.AddItem("test2", "include2");
            conditionItem.Condition = "SomeCondition";

            project.ItemsWithoutConditions().Count().Should().Be(1);
            project.ItemsWithoutConditions().First().Should().Be(conditionlessItems);
        }

        [Fact]
        public void ItemsWithoutConditions_doesnt_return_items_with_a_parent_with_a_condition()
        {
            var project = ProjectRootElement.Create();
            var conditionlessItems = project.AddItem("test", "include1");

            var conditionItemGroup = project.AddItemGroup();
            conditionItemGroup.Condition = "SomeCondition";
            conditionItemGroup.AddItem("test2", "include2");

            project.ItemsWithoutConditions().Count().Should().Be(1);
            project.ItemsWithoutConditions().First().Should().Be(conditionlessItems);
        }

        [Fact]
        public void AddIncludes_merges_include_sets()
        {
            var project = ProjectRootElement.Create();
            var item1 = project.AddItem("test", "include1;include2");
            item1.UnionIncludes(new string[] {"include2", "include3"});

            item1.Include.Should().Be("include1;include2;include3");
        }

        [Fact]
        public void AddExcludes_merges_include_sets()
        {
            var project = ProjectRootElement.Create();
            var item1 = project.AddItem("test", "include1");
            item1.Exclude = "exclude1;exclude2";
            item1.UnionExcludes(new string[] {"exclude2", "exclude3"});

            item1.Exclude.Should().Be("exclude1;exclude2;exclude3");
        }

        [Fact]
        public void AddMetadata_adds_metadata_available_via_Metadata_on_an_item()
        {
            var project = ProjectRootElement.Create();
            var item1 = project.AddItem("test", "include1");
            item1.AddMetadata("name", "value");
            item1.HasMetadata.Should().BeTrue();

            var item2 = project.AddItem("test1", "include1");
            item2.AddMetadata(item1.Metadata);

            item2.HasMetadata.Should().BeTrue();
            item2.Metadata.First().Name.Should().Be("name");
            item2.Metadata.First().Value.Should().Be("value");
        }

        [Fact]
        public void AddMetadata_adds_metadata_from_an_item_generated_from_another_project()
        {
            var project = ProjectRootElement.Create();
            var item1 = project.AddItem("test", "include1");
            item1.AddMetadata("name", "value");
            item1.HasMetadata.Should().BeTrue();

            var project2 = ProjectRootElement.Create();
            var item2 = project2.AddItem("test1", "include1");
            item2.AddMetadata(item1.Metadata);

            item2.HasMetadata.Should().BeTrue();
            item2.Metadata.First().Name.Should().Be("name");
            item2.Metadata.First().Value.Should().Be("value");
        }
    }
}