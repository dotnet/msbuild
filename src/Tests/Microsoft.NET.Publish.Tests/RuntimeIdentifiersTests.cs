// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using FluentAssertions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

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

            var testAsset = _testAssetsManager.CreateTestProject(testProject);
            var buildCommand = new BuildCommand(testAsset);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            string targetFrameworkOutputDirectory = Path.Combine(buildCommand.GetNonSDKOutputDirectory().FullName, testProject.TargetFrameworks);
            string outputDirectoryWithRuntimeIdentifier = Directory.EnumerateDirectories(targetFrameworkOutputDirectory, "*", SearchOption.AllDirectories).FirstOrDefault();
            outputDirectoryWithRuntimeIdentifier.Should().NotBeNullOrWhiteSpace();

            var selfContainedExecutable = $"{testProject.Name}{Constants.ExeSuffix}";
            string selfContainedExecutableFullPath = Path.Combine(outputDirectoryWithRuntimeIdentifier, selfContainedExecutable);

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

        [WindowsOnlyTheory]
        [InlineData("net7.0", "win-x64", "win-x86", false, false)]
        [InlineData("net7.0", "win-x64", "win-x86", true, false)]
        [InlineData("net7.0", "win-x64", "win-x86", true, true)]
        public void PublishRuntimeIdentifierSetsRuntimeIdentifierAndDoesOrDoesntOverrideRID(string tfm, string publishRuntimeIdentifier, string runtimeIdentifier, bool runtimeIdentifierIsGlobal, bool publishRuntimeIdentifierIsGlobal)
        {
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = tfm
            };
            testProject.AdditionalProperties["RuntimeIdentifier"] = runtimeIdentifier;
            testProject.AdditionalProperties["PublishRuntimeIdentifier"] = publishRuntimeIdentifier;
            testProject.RecordProperties("RuntimeIdentifier");

            List<string> args = new List<string>
            {
                $"/p:_IsPublishing=true", // Normally this would be set by the CLI (OR VS Soon TM), but this calls directly into t:/Publish.
                runtimeIdentifierIsGlobal ? $"/p:RuntimeIdentifier={runtimeIdentifier}" : "",
                publishRuntimeIdentifierIsGlobal ? $"/p:PublishRuntimeIdentifier={publishRuntimeIdentifier}" : ""
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: $"{publishRuntimeIdentifierIsGlobal}-{runtimeIdentifierIsGlobal}");
            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute(args.ToArray())
                .Should()
                .Pass();

            string expectedRid = runtimeIdentifierIsGlobal ? runtimeIdentifier : publishRuntimeIdentifier;
            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: tfm, runtimeIdentifier: expectedRid);
            var finalRid = properties["RuntimeIdentifier"];

            Assert.True(finalRid == expectedRid); // This assert is theoretically worthless as the above code will fail if the RID path is wrong.
        }

        [WindowsOnlyTheory]
        [InlineData("net7.0", "tizen")] // tizen is an arbitrary nonwindows rid, picked because it will be different from a windows rid.
        public void PublishRuntimeIdentifierDoesNotOverrideUseCurrentRuntime(string tfm, string publishRid)
        {
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = tfm
            };

            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "true";
            testProject.AdditionalProperties["PublishRuntimeIdentifier"] = publishRid;
            testProject.RecordProperties("RuntimeIdentifier");
            testProject.RecordProperties("NETCoreSdkPortableRuntimeIdentifier");

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: "UCR_PUBLISH_RID_OVERRIDES");
            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute($"/p:_IsPublishing=true")
                .Should()
                .Pass();

            var projectTfmPath = Path.Combine(testAsset.Path, System.Reflection.MethodBase.GetCurrentMethod().Name, "bin", "Debug", tfm);
            var projectPath = Directory.GetDirectories(projectTfmPath).FirstOrDefault();
            var testResolvedRid = Path.GetFileName(projectPath);

            var properties = testProject.GetPropertyValues(testAsset.TestRoot, targetFramework: tfm, runtimeIdentifier: testResolvedRid);
            var finalRid = properties["RuntimeIdentifier"];
            var expectedRid = properties["NETCoreSdkPortableRuntimeIdentifier"];

            Assert.True(expectedRid == testResolvedRid);
            Assert.True(finalRid == expectedRid); // This assert is theoretically worthless as the above code will fail if the RID path is wrong.
        }

        [Fact]
        public void ImplicitRuntimeIdentifierOptOutCorrecltyOptsOut()
        {
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid(targetFramework);
            var testProject = new TestProject()
            {
                IsExe = true,
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["SelfContained"] = "true";
            testProject.AdditionalProperties["UseCurrentRuntimeIdentifier"] = "false";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var publishCommand = new PublishCommand(testAsset);
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

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
