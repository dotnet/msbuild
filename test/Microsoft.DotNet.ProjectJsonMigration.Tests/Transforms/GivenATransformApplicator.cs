using System;
using Microsoft.Build.Construction;
using Xunit;
using Xunit.Runner.DotNet;
using FluentAssertions;
using System.Linq;
using Microsoft.DotNet.ProjectJsonMigration.Models;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenATransformApplicator
    {
        [Fact]
        public void It_merges_Metadata_and_Exclude_with_items_with_same_ItemType_and_Include_when_mergeExisting_is_true()
        {
            var metadata = new ItemMetadataValue<string>[]
            {
                new ItemMetadataValue<string>("metadata1", "value1"),
                new ItemMetadataValue<string>("metadata2", "value2")
            };

            var fullItemTransformSetIncludeValue = "include1;include2";

            var transform1 = new AddItemTransform<string>("item",
                    fullItemTransformSetIncludeValue,
                    "exclude1",
                    t => true)
                .WithMetadata(metadata[0]);

            var transform2 = new AddItemTransform<string>("item",
                    fullItemTransformSetIncludeValue,
                    "exclude2",
                    t => true)
                .WithMetadata(metadata[1]);

            var mockProj = ProjectRootElement.Create();
            var itemGroup = mockProj.AddItemGroup();

            var item1 = transform1.Transform("_");
            item1.AddMetadata(metadata[0].MetadataName, metadata[0].GetMetadataValue(null));

            var item2 = transform2.Transform("_");
            item2.AddMetadata(metadata[1].MetadataName, metadata[1].GetMetadataValue(null));

            var transformApplicator = new TransformApplicator();
            transformApplicator.Execute(new ProjectItemElement[] {item1, item2}, itemGroup, mergeExisting:true);

            itemGroup.Items.Count.Should().Be(1);

            var item = itemGroup.Items.First();
            item.Exclude.Should().Be("exclude1;exclude2");

            item.Metadata.Count().Should().Be(2);
            var foundMetadata = metadata.ToDictionary<ItemMetadataValue<string>, string, bool>(m => m.MetadataName,
                m => false);

            foreach (var metadataEntry in item.Metadata)
            {
                foundMetadata.Should().ContainKey(metadataEntry.Name);
                foundMetadata[metadataEntry.Name].Should().BeFalse();
                foundMetadata[metadataEntry.Name] = true;
            }

            foundMetadata.All(kv => kv.Value).Should().BeTrue();
        }
    }
}