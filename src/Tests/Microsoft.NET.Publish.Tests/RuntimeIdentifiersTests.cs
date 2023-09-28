// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Publish.Tests
{
    public class RuntimeIdentifiersTests : SdkTest
    {
        public RuntimeIdentifiersTests(ITestOutputHelper log) : base(log)
        {
        }

        //  Run on core MSBuild only as using a local packages folder hits long path issues on full MSBuild
        [CoreMSBuildOnlyFact]
        public void BuildWithRuntimeIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "BuildWithRid",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var compatibleRid = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            var runtimeIdentifiers = new[]
            {
                "win-x64",
                "linux-x64",
                compatibleRid
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = string.Join(';', runtimeIdentifiers);

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var restoreCommand = new RestoreCommand(testAsset);

            restoreCommand
                .Execute()
                .Should()
                .Pass();

            foreach (var runtimeIdentifier in runtimeIdentifiers)
            {
                var buildCommand = new BuildCommand(testAsset);

                buildCommand
                    .ExecuteWithoutRestore($"/p:RuntimeIdentifier={runtimeIdentifier}")
                    .Should()
                    .Pass();

                if (runtimeIdentifier == compatibleRid)
                {
                    var outputDirectory = buildCommand.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: runtimeIdentifier);
                    var selfContainedExecutable = $"{testProject.Name}{Constants.ExeSuffix}";
                    string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

                    new RunExeCommand(Log, selfContainedExecutableFullPath)
                        .Execute()
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOutContaining("Hello World!");
                }
            }
        }

        [Fact]
        public void BuildWithUseCurrentRuntimeIdentifier()
        {
            var testProject = new TestProject()
            {
                Name = "BuildWithUseCurrentRuntimeIdentifier",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsSdkProject = true,
                IsExe = true
            };

            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "True";

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            testProject.RecordProperties("RuntimeIdentifier");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var runtimeIdentifier = testProject.GetPropertyValues(testAsset.TestRoot)["RuntimeIdentifier"];
            runtimeIdentifier.Should().NotBeNullOrWhiteSpace();

            var selfContainedExecutable = $"{testProject.Name}{Constants.ExeSuffix}";
            string selfContainedExecutableFullPath = Path.Combine(buildCommand.GetOutputDirectory(runtimeIdentifier: runtimeIdentifier).FullName, selfContainedExecutable);

            new RunExeCommand(Log, selfContainedExecutableFullPath)
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("Hello World!");
        }

        //  Run on core MSBuild only as using a local packages folder hits long path issues on full MSBuild
        [CoreMSBuildOnlyTheory]
        [InlineData(false)]
        //  "No build" scenario doesn't currently work: https://github.com/dotnet/sdk/issues/2956
        //[InlineData(true)]
        public void PublishWithRuntimeIdentifier(bool publishNoBuild)
        {
            var testProject = new TestProject()
            {
                Name = "PublishWithRid",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var compatibleRid = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            var runtimeIdentifiers = new[]
            {
                "win-x64",
                "linux-x64",
                compatibleRid
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = string.Join(';', runtimeIdentifiers);

            //  Use a test-specific packages folder
            testProject.AdditionalProperties["RestorePackagesPath"] = @"$(MSBuildProjectDirectory)\..\pkg";

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: publishNoBuild ? "nobuild" : string.Empty);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            foreach (var runtimeIdentifier in runtimeIdentifiers)
            {
                var publishArgs = new List<string>()
                {
                    $"/p:RuntimeIdentifier={runtimeIdentifier}"
                };
                if (publishNoBuild)
                {
                    publishArgs.Add("/p:NoBuild=true");
                }

                var publishCommand = new PublishCommand(testAsset);
                publishCommand.Execute(publishArgs.ToArray())
                    .Should()
                    .Pass();

                if (runtimeIdentifier == compatibleRid)
                {
                    var outputDirectory = publishCommand.GetOutputDirectory(testProject.TargetFrameworks, runtimeIdentifier: runtimeIdentifier);
                    var selfContainedExecutable = $"{testProject.Name}{Constants.ExeSuffix}";
                    string selfContainedExecutableFullPath = Path.Combine(outputDirectory.FullName, selfContainedExecutable);

                    new RunExeCommand(Log, selfContainedExecutableFullPath)
                        .Execute()
                        .Should()
                        .Pass()
                        .And
                        .HaveStdOutContaining("Hello World!");

                }
            }
        }

        [Theory]
        [InlineData(false, false)] // publish rid overrides rid in project file if publishing
        [InlineData(true, false)] // publish rid doesnt override global rid
        [InlineData(true, true)] // publish rid doesnt override global rid, even if global
        public void PublishRuntimeIdentifierSetsRuntimeIdentifierAndDoesOrDoesntOverrideRID(bool runtimeIdentifierIsGlobal, bool publishRuntimeIdentifierIsGlobal)
        {
            string tfm = ToolsetInfo.CurrentTargetFramework;
            string publishRuntimeIdentifier = "win-x64";
            string runtimeIdentifier = "win-x86";

            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = tfm
            };
            if (!publishRuntimeIdentifierIsGlobal)
                testProject.AdditionalProperties["PublishRuntimeIdentifier"] = publishRuntimeIdentifier;
            if (!runtimeIdentifierIsGlobal)
                testProject.AdditionalProperties["RuntimeIdentifier"] = runtimeIdentifier;
            testProject.RecordProperties("RuntimeIdentifier");

            List<string> args = new()
            {
                runtimeIdentifierIsGlobal ? $"/p:RuntimeIdentifier={runtimeIdentifier}" : "",
                publishRuntimeIdentifierIsGlobal ? $"/p:PublishRuntimeIdentifier={publishRuntimeIdentifier}" : ""
            };

            string identifier = $"PublishRuntimeIdentifierOverrides-{publishRuntimeIdentifierIsGlobal}-{runtimeIdentifierIsGlobal}";
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: identifier);
            var publishCommand = new DotnetPublishCommand(Log);
            publishCommand
                .WithWorkingDirectory(Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute(args.ToArray())
                .Should()
                .Pass();

            string expectedRid = runtimeIdentifierIsGlobal ? runtimeIdentifier : publishRuntimeIdentifier;
            var properties = testProject.GetPropertyValues(testAsset.TestRoot, configuration: "Release", targetFramework: tfm);
            var finalRid = properties["RuntimeIdentifier"];

            Assert.True(finalRid == expectedRid);
        }

        [WindowsOnlyFact]
        public void PublishRuntimeIdentifierOverridesUseCurrentRuntime()
        {
            string tfm = ToolsetInfo.CurrentTargetFramework;
            string publishRid = "linux-x64"; // linux is arbitrarily picked; just because it is different than a windows RID.
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = tfm
            };

            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";
            testProject.AdditionalProperties["PublishRuntimeIdentifier"] = publishRid;
            testProject.RecordProperties("RuntimeIdentifier");
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var publishCommand = new DotnetPublishCommand(Log);
            publishCommand
                .WithWorkingDirectory(Path.Combine(testAsset.TestRoot, MethodBase.GetCurrentMethod().Name))
                .Execute()
                .Should()
                .Pass();

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, configuration: "Release", targetFramework: tfm);
            var finalRid = properties["RuntimeIdentifier"];
            var ucrRid = properties["NETCoreSdkPortableRuntimeIdentifier"];

            Assert.True(finalRid == publishRid);
            Assert.True(ucrRid != finalRid);
        }

        [Theory]
        [InlineData("PublishReadyToRun", true)]
        [InlineData("PublishSingleFile", true)]
        [InlineData("PublishTrimmed", true)]
        [InlineData("PublishAot", true)]
        [InlineData("PublishReadyToRun", false)]
        [InlineData("PublishSingleFile", false)]
        [InlineData("PublishTrimmed", false)]
        public void SomePublishPropertiesInferSelfContained(string property, bool useFrameworkDependentDefaultTargetFramework)
        {
            // Note: there is a bug with PublishAot I think where this test will fail for Aot if the testname is too long. Do not make it longer.
            var tfm = useFrameworkDependentDefaultTargetFramework ? ToolsetInfo.CurrentTargetFramework : "net7.0"; // net 7 is the last non FDD default TFM at the time of this PR.
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = tfm,
            };
            testProject.AdditionalProperties[property] = "true";

            testProject.RecordProperties("SelfContained");
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: $"{property}-{useFrameworkDependentDefaultTargetFramework}");

            var publishCommand = new DotnetPublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            if (property == "PublishTrimmed" && !useFrameworkDependentDefaultTargetFramework)
            {
                publishCommand
                   .Execute()
                   .Should()
                   .Fail();
            }
            else
            {
                publishCommand
                    .Execute()
                    .Should()
                    .Pass();
            }

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: tfm, configuration: useFrameworkDependentDefaultTargetFramework ? "Release" : "Debug");

            var expectedSelfContainedValue = "true";
            if (
                (property == "PublishReadyToRun" && useFrameworkDependentDefaultTargetFramework) || // This property should no longer infer SelfContained in net 8
                (property == "PublishTrimmed" && !useFrameworkDependentDefaultTargetFramework) // This property did not infer SelfContained until net 8
               )
            {
                expectedSelfContainedValue = "false";
            }

            properties["SelfContained"].Should().Be(expectedSelfContainedValue);
        }

        [Fact]
        public void ImplicitRuntimeIdentifierOptOutCorrectlyOptsOut()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = targetFramework,
                SelfContained = "true"
            };

            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new DotnetPublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1191");
        }

        [Fact]
        public void DuplicateRuntimeIdentifiers()
        {
            var testProject = new TestProject()
            {
                Name = "DuplicateRuntimeIdentifiers",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true
            };

            var compatibleRid = EnvironmentInfo.GetCompatibleRid(testProject.TargetFrameworks);

            testProject.AdditionalProperties["RuntimeIdentifiers"] = compatibleRid + ";" + compatibleRid;
            testProject.RuntimeIdentifier = compatibleRid;

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

        }

        [Fact]
        public void PublishSuccessfullyWithRIDRequiringPropertyAndRuntimeIdentifiersNoRuntimeIdentifier()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = targetFramework
            };

            testProject.AdditionalProperties["RuntimeIdentifiers"] = runtimeIdentifier;
            testProject.AdditionalProperties["PublishReadyToRun"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new DotnetPublishCommand(Log, Path.Combine(testAsset.TestRoot, testProject.Name));
            publishCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
