// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Newtonsoft.Json.Linq;
using Xunit;
using Microsoft.DotNet.ProjectModel.Files;
using FluentAssertions;
using System.IO;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class GivenThatIWantToReadNamedResources
    {
        private readonly string ProjectFilePath = AppContext.BaseDirectory;

        [Fact]
        public void It_returns_an_empty_dictionary_when_no_namedResource_is_set()
        {
            var json = new JObject();

            var namedResources = NamedResourceReader.ReadNamedResources(json, ProjectFilePath);

            namedResources.Should().BeEmpty();
        }

        [Fact]
        public void It_throws_when_the_namedResource_is_not_a_Json_object()
        {
            var json = new JObject();
            json.Add("namedResource", "not an object");

            Action action = () => NamedResourceReader.ReadNamedResources(json, ProjectFilePath);

            action.ShouldThrow<FileFormatException>("Value must be an object");
        }

        [Fact]
        public void It_throws_when_a_specified_namedResource_value_is_not_a_string()
        {
            var json = new JObject();
            var namedResources = new JObject();
            json.Add("namedResource", namedResources);

            namedResources.Add("System.Strings", new JObject());

            Action action = () => NamedResourceReader.ReadNamedResources(json, ProjectFilePath);

            action.ShouldThrow<FileFormatException>("Value must be string.");
        }

        [Fact]
        public void It_throws_when_a_specified_namedResource_value_contains_a_wild_card()
        {
            var json = new JObject();
            var namedResources = new JObject();
            json.Add("namedResource", namedResources);

            namedResources.Add("System.Strings", "*");

            Action action = () => NamedResourceReader.ReadNamedResources(json, ProjectFilePath);

            action.ShouldThrow<FileFormatException>("Value cannot contain wildcards.");
        }

        [Fact]
        public void It_adds_named_resources_and_uses_the_full_path_for_their_values()
        {
            var json = new JObject();
            var namedResourcesJson = new JObject();
            json.Add("namedResource", namedResourcesJson);

            namedResourcesJson.Add("System.Strings", "System.Strings.resx");
            namedResourcesJson.Add("Another.System.Strings", "Another.System.Strings.resx");

            var namedResources = NamedResourceReader.ReadNamedResources(json, ProjectFilePath);

            namedResources["System.Strings"].Should().Be(
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectFilePath), "System.Strings.resx")));
            namedResources["Another.System.Strings"].Should().Be(
                Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectFilePath), "Another.System.Strings.resx")));
        }
    }
}
