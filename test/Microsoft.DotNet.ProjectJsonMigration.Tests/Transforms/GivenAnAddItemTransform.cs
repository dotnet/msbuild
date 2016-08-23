using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using Microsoft.DotNet.ProjectJsonMigration.Models;
using Microsoft.DotNet.ProjectJsonMigration.Transforms;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenAnAddItemTransform
    {
        [Fact]
        public void It_returns_an_item_with_Include_Exclude_and_Metadata_to_project_when_condition_is_true()
        {
            var itemTransforms = GetFullItemTransformSet(true);

            foreach (var transform in itemTransforms)
            {
                var item = transform.Transform("_");

                item.Should().NotBeNull();
                item.Include.Should().Be(FullItemTransformSetIncludeValue);
                item.Exclude.Should().Be(FullItemTransformSetExcludeValue);

                item.HasMetadata.Should().BeTrue();

                var metadata = item.Metadata.First();
                metadata.Name.Should().Be(FullItemTransformSetMetadataName);
                metadata.Value.Should().Be(FullItemTransformSetMetadataValue);
            }
        }

        [Fact]
        public void It_returns_null_when_condition_is_false()
        {
            var itemTransforms = GetFullItemTransformSet(false);

            foreach (var transform in itemTransforms)
            {
                transform.Transform("_").Should().BeNull();
            }
        }

        private static string FullItemTransformSetItemNamePrefix => "item";
        private static string FullItemTransformSetIncludeValue => "include1;include2";
        private static string FullItemTransformSetExcludeValue => "exclude1;exclude2";
        private static string FullItemTransformSetMetadataName => "SomeName";
        private static string FullItemTransformSetMetadataValue => "SomeValue";

        private AddItemTransform<string>[] GetFullItemTransformSet(bool condition)
        {
            return new AddItemTransform<string>[]
            {
                new AddItemTransform<string>(FullItemTransformSetItemNamePrefix + "1",
                    FullItemTransformSetIncludeValue.Split(';'),
                    FullItemTransformSetExcludeValue.Split(';'),
                    t => condition)
                    .WithMetadata(FullItemTransformSetMetadataName, FullItemTransformSetMetadataValue),
                new AddItemTransform<string>(FullItemTransformSetItemNamePrefix + "2",
                    t => FullItemTransformSetIncludeValue,
                    t => FullItemTransformSetExcludeValue,
                    t => condition)
                    .WithMetadata(FullItemTransformSetMetadataName, t => FullItemTransformSetMetadataValue),
                new AddItemTransform<string>(FullItemTransformSetItemNamePrefix + "3",
                    FullItemTransformSetIncludeValue,
                    t => FullItemTransformSetExcludeValue,
                    t => condition)
                    .WithMetadata(new ItemMetadataValue<string>(FullItemTransformSetMetadataName, FullItemTransformSetMetadataValue)),
                new AddItemTransform<string>(FullItemTransformSetItemNamePrefix + "4",
                    t => FullItemTransformSetIncludeValue,
                    FullItemTransformSetExcludeValue,
                    t => condition)
                    .WithMetadata(new ItemMetadataValue<string>(FullItemTransformSetMetadataName, t => FullItemTransformSetMetadataValue)),
                new AddItemTransform<string>(FullItemTransformSetItemNamePrefix + "5",
                    FullItemTransformSetIncludeValue,
                    FullItemTransformSetExcludeValue,
                    t => condition)
                    .WithMetadata(FullItemTransformSetMetadataName, FullItemTransformSetMetadataValue)
            };
        }
    }
}
