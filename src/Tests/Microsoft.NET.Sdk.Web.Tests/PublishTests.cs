// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json.Nodes;

namespace Microsoft.NET.Sdk.Web.Tests
{
    public class PublishTests : SdkTest
    {
        public PublishTests(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [MemberData(nameof(SupportedTfms))]
        public void TrimmingOptions_Are_Defaulted_Correctly_On_Trimmed_Apps(string targetFramework)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = rid;
            testProject.SelfContained = "true";
            testProject.PropertiesToRecord.Add("TrimMode");

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: projectName + targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute($"/p:RuntimeIdentifier={rid}").Should().Pass();

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            buildProperties["TrimMode"].Should().Be("");

            string outputDirectory = publishCommand.GetOutputDirectory(targetFramework: targetFramework, runtimeIdentifier: rid).FullName;
            string runtimeConfigFile = Path.Combine(outputDirectory, $"{projectName}.runtimeconfig.json");
            string runtimeConfigContents = File.ReadAllText(runtimeConfigFile);

            JsonNode runtimeConfig = JsonNode.Parse(runtimeConfigContents);
            JsonNode configProperties = runtimeConfig["runtimeOptions"]["configProperties"];

            configProperties["System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault"].GetValue<bool>()
                    .Should().BeFalse();
        }

        [Fact]
        public void TrimMode_Defaulted_Correctly_On_Trimmed_Apps_Pre_Net8()
        {
            var projectName = "HelloWorld";
            var targetFramework = "net7.0";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            testProject.AdditionalProperties["PublishTrimmed"] = "true";
            testProject.AdditionalProperties["RuntimeIdentifier"] = rid;
            testProject.SelfContained = "true";
            testProject.PropertiesToRecord.Add("TrimMode");

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: projectName + targetFramework);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand.Execute().Should().Pass();

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            buildProperties["TrimMode"].Should().Be("partial");
        }

        [Theory]
        [MemberData(nameof(SupportedTfms))]
        public void TrimmingOptions_Are_Defaulted_Correctly_On_Aot_Apps(string targetFramework)
        {
            var projectName = "HelloWorld";
            var rid = EnvironmentInfo.GetCompatibleRid(targetFramework);

            var testProject = CreateTestProjectForILLinkTesting(targetFramework, projectName);
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");
            testProject.AdditionalProperties["PublishAot"] = "true";
            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";
            testProject.PropertiesToRecord.Add("PublishTrimmed");
            testProject.PropertiesToRecord.Add("TrimMode");
            testProject.PropertiesToRecord.Add("PublishIISAssets");

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: projectName + targetFramework);
            var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand.Execute().Should().Pass();

            var buildProperties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework);
            buildProperties["PublishTrimmed"].Should().Be("true");
            buildProperties["TrimMode"].Should().Be("");
            buildProperties["PublishIISAssets"].Should().Be("false");
            var ucrRid = buildProperties["NETCoreSdkPortableRuntimeIdentifier"];

            string outputDirectory = publishCommand.GetIntermediateDirectory(targetFramework, runtimeIdentifier: ucrRid).FullName;
            string responseFile = Path.Combine(outputDirectory, "native", $"{projectName}.ilc.rsp");
            var responseFileContents = File.ReadLines(responseFile);

            responseFileContents.Should().Contain("--feature:System.Text.Json.JsonSerializer.IsReflectionEnabledByDefault=false");
            responseFileContents.Should().Contain("--feature:System.Diagnostics.Tracing.EventSource.IsSupported=true");
            responseFileContents.Should().Contain("--runtimeknob:System.GC.DynamicAdaptationMode=1");
            File.Exists(Path.Combine(outputDirectory, "web.config")).Should().BeFalse();
        }

        public static IEnumerable<object[]> SupportedTfms { get; } = new List<object[]>
        {
#if NET8_0
            new object[] { ToolsetInfo.CurrentTargetFramework }
#else
#error If building for a newer TFM, please update the values above
#endif
        };

        private TestProject CreateTestProjectForILLinkTesting(
            string targetFramework,
            string projectName)
        {
            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = targetFramework,
                IsExe = true,
                ProjectSdk = "Microsoft.NET.Sdk.Web"
            };

            testProject.SourceFiles[$"Program.cs"] = """
                using Microsoft.AspNetCore.Builder;
                using Microsoft.Extensions.Hosting;

                var builder = WebApplication.CreateBuilder();
                var app = builder.Build();
                app.Start();
                """;

            return testProject;
        }
    }
}
