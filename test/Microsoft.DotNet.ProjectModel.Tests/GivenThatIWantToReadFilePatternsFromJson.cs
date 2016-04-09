// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.ProjectModel.Files;
using Newtonsoft.Json.Linq;
using Xunit;
using FluentAssertions;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class GivenThatIWantToReadFilePatternsFromJson
    {
        private const string SomeProperty = "some property";
        private static readonly string[] SomeDefaultValues = { $"**{Path.DirectorySeparatorChar}*.cs" };

        [Fact]
        public void It_returns_empty_when_there_is_no_property_and_no_default_pattern()
        {
            var json = new JObject();
            var patternsCollection = PatternsCollectionHelper.GetPatternsCollection(
                json,
                AppContext.BaseDirectory,
                string.Empty,
                "some non-existing property");

            patternsCollection.Should().BeEmpty();
        }

        [Fact]
        public void It_uses_the_passed_in_default_collection_when_the_property_is_not_in_the_json()
        {
            var json = new JObject();
            var patternsCollection = PatternsCollectionHelper.GetPatternsCollection(
                json,
                AppContext.BaseDirectory,
                string.Empty,
                "some non-existing property",
                SomeDefaultValues);

            patternsCollection.Should().Contain(SomeDefaultValues);
        }

        [Fact]
        public void It_uses_the_value_in_the_property_when_it_is_a_string()
        {
            var json = new JObject();
            json.Add(SomeProperty, "*");
            var patternsCollection = PatternsCollectionHelper.GetPatternsCollection(
                json,
                AppContext.BaseDirectory,
                string.Empty,
                SomeProperty,
                SomeDefaultValues);

            patternsCollection.Should().Contain("*");
        }

        [Fact]
        public void It_uses_the_values_in_the_property_when_it_is_a_string_array()
        {
            var patterns = new[] {"*", $"**{Path.DirectorySeparatorChar}*.fs"};
            var json = new JObject();
            json.Add(SomeProperty, new JArray(patterns));
            var patternsCollection = PatternsCollectionHelper.GetPatternsCollection(
                json,
                AppContext.BaseDirectory,
                string.Empty,
                SomeProperty,
                SomeDefaultValues);

            patternsCollection.Should().Contain(patterns);
        }

        [Fact]
        public void It_throws_when_the_property_value_is_neither_a_string_nor_an_array()
        {
            var json = new JObject();
            json.Add(SomeProperty, new JObject());
            Action action = () => PatternsCollectionHelper.GetPatternsCollection(
                json,
                AppContext.BaseDirectory,
                string.Empty,
                SomeProperty,
                SomeDefaultValues);

            action.ShouldThrow<FileFormatException>().WithMessage("Value must be either string or array.");
        }

        [Fact]
        public void It_throws_when_we_ask_for_a_literal_and_specify_a_pattern()
        {
            var json = new JObject();
            json.Add(SomeProperty, "*");
            Action action = () => PatternsCollectionHelper.GetPatternsCollection(
                json,
                AppContext.BaseDirectory,
                string.Empty,
                SomeProperty,
                SomeDefaultValues,
                true);

            action.ShouldThrow<FileFormatException>()
                .WithMessage($"The '{SomeProperty}' property cannot contain wildcard characters.");
        }

        [Fact]
        public void It_throws_when_the_property_value_is_a_rooted_path()
        {
            var json = new JObject();
            json.Add(SomeProperty, AppContext.BaseDirectory);
            Action action = () => PatternsCollectionHelper.GetPatternsCollection(
                json,
                AppContext.BaseDirectory,
                string.Empty,
                SomeProperty,
                SomeDefaultValues,
                true);

            action.ShouldThrow<FileFormatException>()
                .WithMessage($"The '{SomeProperty}' property cannot be a rooted path.");
        }
    }
}
