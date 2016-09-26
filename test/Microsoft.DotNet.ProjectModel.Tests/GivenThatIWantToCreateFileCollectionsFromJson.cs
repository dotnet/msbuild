// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using System.Linq;
using FluentAssertions;

namespace Microsoft.DotNet.ProjectModel.Tests
{
    public class GivenThatIWantToCreateFileCollectionsFromJson
    {
        private const string ProjectName = "some project name";
        private readonly string ProjectFilePath = AppContext.BaseDirectory;

        [Fact]
        public void PackInclude_is_empty_when_it_is_not_set_in_the_ProjectJson()
        {
            var json = new JObject();
            var project = GetProject(json);

            project.Files.PackInclude.Should().BeEmpty();
        }

        [Fact]
        public void It_sets_PackInclude_when_packInclude_is_set_in_the_ProjectJson()
        {
            const string somePackTarget = "some pack target";
            const string somePackValue = "some pack value";

            var json = new JObject();
            var packIncludeJson = new JObject();
            json.Add("packInclude", packIncludeJson);

            packIncludeJson.Add(somePackTarget, somePackValue);

            var project = GetProject(json);

            var packInclude = project.Files.PackInclude.FirstOrDefault();

            packInclude.Target.Should().Be(somePackTarget);
            packInclude.SourceGlobs.Should().Contain(somePackValue);
        }

        [Fact]
        public void It_parses_namedResources_successfully()
        {
            const string someString = "some string";

            var json = new JObject();
            var namedResources= new JObject();
            json.Add("namedResource", namedResources);

            namedResources.Add(someString, "Some/Resource.resx");

            var project = GetProject(json);

            var key = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(ProjectFilePath), "Some", "Resource.resx"));
            project.Files.ResourceFiles[key].Should().Be(someString);
        }

        private Project GetProject(JObject json, ProjectReaderSettings settings = null)
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
                    var projectReader = new ProjectReader();
                    return projectReader.ReadProject(
                        stream,
                        ProjectName,
                        ProjectFilePath,
                        settings);
                }
            }
        }
    }
}
