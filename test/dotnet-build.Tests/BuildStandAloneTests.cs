// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;
using System.Linq;
using Microsoft.DotNet.TestFramework;
using Newtonsoft.Json.Linq;
using FluentAssertions;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildStandAloneTests : TestBase
    {
        [Fact]
        public void BuildingAStandAloneProjectProducesARuntimeConfigDevJsonFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            netstandardappOutput.Should().Exist().And.HaveFile("StandaloneApp.runtimeconfig.dev.json");
        }

        [Fact]
        public void BuildingAStandAloneProjectProducesARuntimeConfigJsonFile()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            netstandardappOutput.Should().Exist().And.HaveFile("StandaloneApp.runtimeconfig.json");
        }

        [Fact]
        public void RuntimeOptionsGetsCopiedToRuntimeConfigJsonForAStandaloneApp()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("PortableTests")
                .WithLockFiles();

            var netstandardappOutput = Build(testInstance);

            var runtimeConfigJsonPath = Path.Combine(netstandardappOutput.FullName, "StandaloneApp.runtimeconfig.json");

            using (var stream = new FileStream(runtimeConfigJsonPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var reader = new StreamReader(stream);

                var rawProject = JObject.Parse(reader.ReadToEnd());
                var runtimeOptions = rawProject["runtimeOptions"];

                runtimeOptions["somethingString"].Value<string>().Should().Be("anything");
                runtimeOptions["somethingBoolean"].Value<bool>().Should().BeTrue();
                runtimeOptions["someArray"].ToObject<string[]>().Should().Contain("one", "two");
                runtimeOptions["someObject"].Value<JObject>()["someProperty"].Value<string>().Should().Be("someValue");
            }
        }

        public DirectoryInfo Build(TestInstance testInstance)
        {
            var projectPath = Path.Combine(testInstance.TestRoot, "StandaloneApp");

            var result = new BuildCommand(
                projectPath: projectPath)
                .ExecuteWithCapturedOutput();

            var contexts = ProjectContext.CreateContextForEachFramework(
                projectPath,
                null,
                PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());

            var runtime = contexts.FirstOrDefault(c => !string.IsNullOrEmpty(c.RuntimeIdentifier))?.RuntimeIdentifier;

            result.Should().Pass();

            var outputBase = new DirectoryInfo(
                Path.Combine(testInstance.TestRoot, "StandaloneApp", "bin", "Debug", "netcoreapp1.0"));

            return outputBase.Sub(runtime);
        }
    }
}
