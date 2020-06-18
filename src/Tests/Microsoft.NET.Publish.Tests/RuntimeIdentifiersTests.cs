using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;

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
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
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
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
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

                var publishCommand = new PublishCommand(Log, Path.Combine(testAsset.Path, testProject.Name));
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

        [Fact]
        public void DuplicateRuntimeIdentifiers()
        {
            var testProject = new TestProject()
            {
                Name = "DuplicateRuntimeIdentifiers",
                TargetFrameworks = "netcoreapp3.0",
                IsSdkProject = true,
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
    }
}
