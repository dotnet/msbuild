// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.ProjectModel;
using Xunit;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class GivenThatIWantToReadGlobalSettingsFromJson
    {
        private const string SomePath = "some path";

        [Fact]
        public void It_throws_if_the_stream_is_not_valid_JSON()
        {
            Action action = () =>
            {
                using (var stream = new MemoryStream())
                {
                    using (var sw = new StreamWriter(stream, Encoding.UTF8, 256, true))
                    {
                        using (var writer = new JsonTextWriter(sw))
                        {
                            writer.Formatting = Formatting.Indented;
                            new JValue("not an object").WriteTo(writer);
                        }

                        stream.Position = 0;
                        GlobalSettings.GetGlobalSettings(stream, string.Empty);
                    }
                }
            };

            action.ShouldThrow<InvalidOperationException>();
        }

        [Fact]
        public void It_leaves_the_searchPaths_empty_when_no_project_and_sources_are_set_in_the_GlobalJson()
        {
            var json = new JObject();
            var globalSettings = GetGlobalSettings(json);

            globalSettings.ProjectSearchPaths.Should().BeEmpty();
        }

        [Fact]
        public void It_leaves_the_searchPaths_empty_when_projects_is_not_an_array_in_the_GlobalJson()
        {
            var json = new JObject();
            json.Add("projects", "not an array");
            var globalSettings = GetGlobalSettings(json);

            globalSettings.ProjectSearchPaths.Should().BeEmpty();
        }

        [Fact]
        public void It_leaves_the_searchPaths_empty_when_sources_is_not_an_array_in_the_GlobalJson()
        {
            var json = new JObject();
            json.Add("sources", "not an array");
            var globalSettings = GetGlobalSettings(json);

            globalSettings.ProjectSearchPaths.Should().BeEmpty();
        }

        [Fact]
        public void It_sets_searchPaths_to_projects_when_projects_is_an_array_in_the_GlobalJson()
        {
            var projectsSearchPaths = new[] {"somepath1", "somepath2"};

            var json = new JObject();
            json.Add("projects", new JArray(projectsSearchPaths));
            var globalSettings = GetGlobalSettings(json);

            globalSettings.ProjectSearchPaths.Should().Contain(projectsSearchPaths);
        }

        [Fact]
        public void It_sets_searchPaths_to_sources_when_sources_is_an_array_in_the_GlobalJson()
        {
            var sourcesSearchPaths = new[] { "somepath1", "somepath2" };

            var json = new JObject();
            json.Add("sources", new JArray(sourcesSearchPaths));
            var globalSettings = GetGlobalSettings(json);

            globalSettings.ProjectSearchPaths.Should().Contain(sourcesSearchPaths);
        }

        [Fact]
        public void It_sets_searchPaths_to_projects_when_both_projects_and_sources_are_arrays_in_the_GlobalJson()
        {
            var projectsSearchPaths = new[] { "somepath1", "somepath2" };
            var sourcesSearchPaths = new[] { "someotherpath1", "someotherpath2" };

            var json = new JObject();
            json.Add("projects", new JArray(projectsSearchPaths));
            json.Add("sources", new JArray(sourcesSearchPaths));
            var globalSettings = GetGlobalSettings(json);

            globalSettings.ProjectSearchPaths.Should().Contain(projectsSearchPaths);
        }

        [Fact]
        public void It_leaves_packagesPath_null_when_packages_is_not_set_in_the_GlobalJson()
        {
            var json = new JObject();
            var globalSettings = GetGlobalSettings(json);

            globalSettings.PackagesPath.Should().BeNull();
        }

        [Fact]
        public void It_sets_packagesPath_to_packages_when_it_is_set_in_the_GlobalJson()
        {
            const string somePackagesPath = "some packages path";

            var json = new JObject();
            json.Add("packages", somePackagesPath);
            var globalSettings = GetGlobalSettings(json);

            globalSettings.PackagesPath.Should().Be(somePackagesPath);
        }

        [Fact]
        public void It_sets_filePath_to_the_path_passed_in()
        {
            var json = new JObject();
            var globalSettings = GetGlobalSettings(json);

            globalSettings.FilePath.Should().Be(SomePath);
        }

        public GlobalSettings GetGlobalSettings(JObject json)
        {
            using (var stream = new MemoryStream())
            {
                using (var sw = new StreamWriter(stream, Encoding.UTF8, 256, true))
                {
                    using (var writer = new JsonTextWriter(sw))
                    {
                        writer.Formatting = Formatting.Indented;
                        json.WriteTo(writer);
                    }

                    stream.Position = 0;
                    return GlobalSettings.GetGlobalSettings(stream, SomePath);
                }
            }
        }
    }
}
