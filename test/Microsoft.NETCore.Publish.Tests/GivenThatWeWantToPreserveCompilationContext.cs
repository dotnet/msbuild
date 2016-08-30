// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using FluentAssertions.Json;
using Microsoft.NETCore.TestFramework;
using Microsoft.NETCore.TestFramework.Assertions;
using Microsoft.NETCore.TestFramework.Commands;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;
using static Microsoft.NETCore.TestFramework.Commands.MSBuildTest;

namespace Microsoft.NETCore.Publish.Tests
{
    public class GivenThatWeWantToPreserveCompilationContext
    {
        private TestAssetsManager _testAssetsManager = TestAssetsManager.TestProjectsAssetsManager;

        [Fact]
        public void It_publishes_the_project_with_a_refs_folder_and_correct_deps_file()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("TestAppCompilationContext")
                .WithSource()
                .Restore("--fallbacksource", $"{RepoInfo.PackagesPath}");

            var appProjectDirectory = Path.Combine(testAsset.TestRoot, "TestApp");

            var publishCommand = new PublishCommand(Stage0MSBuild, appProjectDirectory);

            publishCommand
                .Execute()
                .Should()
                .Pass();

            var publishDirectory = publishCommand.GetOutputDirectory();

            publishDirectory.Should().HaveFile("TestApp.dll");
            publishDirectory.Should().HaveFile("TestLibrary.dll");
            publishDirectory.Should().HaveFile("Newtonsoft.Json.dll");

            var refsDirectory = new DirectoryInfo(Path.Combine(publishDirectory.FullName, "refs"));
            // Should have compilation time assemblies
            refsDirectory.Should().HaveFile("System.IO.dll");
            // Libraries in which lib==ref should be deduped
            refsDirectory.Should().NotHaveFile("TestLibrary.dll");
            refsDirectory.Should().NotHaveFile("Newtonsoft.Json.dll");

            JObject depsJson = ReadJson(Path.Combine(publishDirectory.FullName, "TestApp.deps.json"));

            JObject baselineCompilationOptions = new JObject(
                new JProperty("defines", new JArray("DEBUG", "TRACE")),
                new JProperty("languageVersion", ""),
                new JProperty("platform", "AnyCPU"),
                new JProperty("optimize", false),
                new JProperty("keyFile", ""),
                new JProperty("emitEntryPoint", true),
                new JProperty("debugType", "portable"));

            baselineCompilationOptions
                .Should()
                .BeEquivalentTo(depsJson["compilationOptions"]);
        }

        private static JObject ReadJson(string path)
        {
            using (JsonTextReader jsonReader = new JsonTextReader(File.OpenText(path)))
            {
                JsonSerializer serializer = new JsonSerializer();
                return serializer.Deserialize<JObject>(jsonReader);
            }
        }
    }
}