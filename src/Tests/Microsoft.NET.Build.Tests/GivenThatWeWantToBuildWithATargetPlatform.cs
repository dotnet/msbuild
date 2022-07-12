// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using FluentAssertions;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.IO;
using System;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildWithATargetPlatform : SdkTest
    {
        public GivenThatWeWantToBuildWithATargetPlatform(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyRequiresMSBuildVersionTheory("16.8.0.41402")]
        [InlineData("netcoreapp3.1", ".NETCoreApp", "v3.1", "Windows", "7.0")] // Default values pre-5.0
        [InlineData(ToolsetInfo.CurrentTargetFramework, ".NETCoreApp", $"v{ToolsetInfo.CurrentTargetFrameworkVersion}", "", "")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-Windows7.0", ".NETCoreApp", $"v{ToolsetInfo.CurrentTargetFrameworkVersion}", "Windows", "7.0")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-WINDOWS7.0", ".NETCoreApp", $"v{ToolsetInfo.CurrentTargetFrameworkVersion}", "Windows", "7.0")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows", ".NETCoreApp", $"v{ToolsetInfo.CurrentTargetFrameworkVersion}", "Windows", "7.0")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041.0", ".NETCoreApp", $"v{ToolsetInfo.CurrentTargetFrameworkVersion}", "Windows", "10.0.19041.0")]
        public void It_defines_target_platform_from_target_framework(string targetFramework, string expectedTargetFrameworkIdentifier, string expectedTargetFrameworkVersion, string expectedTargetPlatformIdentifier, string expectedTargetPlatformVersion)
        {
            var testProj = new TestProject()
            {
                Name = "TargetPlatformTests",
                TargetFrameworks = targetFramework
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProj, identifier: targetFramework);

            Action<string, string> assertValue = (string valueName, string expected) =>
            {
                var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.Path, testProj.Name), targetFramework, valueName);
                getValuesCommand
                    .Execute()
                    .Should()
                    .Pass();
                if (expected.Trim().Equals(string.Empty))
                {
                    getValuesCommand.GetValues().Count.Should().Be(0, $"expect '{valueName}' to be '{expected}'. But get {string.Join(";", getValuesCommand.GetValues())}");
                }
                else
                {
                    getValuesCommand.GetValues().ShouldBeEquivalentTo(new[] { expected }, $"Asserting \"{valueName}\"'s value");
                }
            };

            assertValue("TargetFrameworkIdentifier", expectedTargetFrameworkIdentifier);
            assertValue("TargetFrameworkVersion", expectedTargetFrameworkVersion);
            assertValue("TargetPlatformIdentifier", expectedTargetPlatformIdentifier);
            assertValue("TargetPlatformIdentifier", expectedTargetPlatformIdentifier);
            assertValue("TargetPlatformVersion", expectedTargetPlatformVersion);
            assertValue("TargetPlatformMoniker", expectedTargetPlatformIdentifier.Equals(string.Empty) && expectedTargetPlatformVersion.Equals(string.Empty) ?
                string.Empty : $"{expectedTargetPlatformIdentifier},Version={expectedTargetPlatformVersion}");
            assertValue("TargetPlatformDisplayName", $"{expectedTargetPlatformIdentifier} {expectedTargetPlatformVersion}");
        }

        [WindowsOnlyRequiresMSBuildVersionFact("16.8.0.41402")]
        public void It_defines_target_platform_from_target_framework_with_explicit_version()
        {
            var targetPlatformVersion = "10.0.19041.0";
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
            var testProj = new TestProject()
            {
                Name = "TargetPlatformTests",
                TargetFrameworks = targetFramework
            };
            testProj.AdditionalProperties["TargetPlatformVersion"] = targetPlatformVersion;
            var testAsset = _testAssetsManager.CreateTestProject(testProj);

            var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.Path, testProj.Name), targetFramework, "TargetPlatformIdentifier");
            getValuesCommand
                .Execute()
                .Should()
                .Pass();
            getValuesCommand.GetValues().ShouldBeEquivalentTo(new[] { "Windows" });
        }

        [Fact]
        public void It_fails_on_unsupported_os()
        {
            TestProject testProject = new TestProject()
            {
                Name = "UnsupportedOS",
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-unsupported"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var build = new BuildCommand(Log, Path.Combine(testAsset.Path, testProject.Name));
            build.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1139");
        }
    }
}
