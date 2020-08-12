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

        [WindowsOnlyRequiresMSBuildVersionTheory("16.8.0-preview-20411-03")]
        [InlineData("netcoreapp3.1", ".NETCoreApp", "v3.1", "Windows", "7.0")] // Default values pre-5.0
        [InlineData("net5.0", ".NETCoreApp", "v5.0", "", "")]
        [InlineData("net5.0-Windows7.0", ".NETCoreApp", "v5.0", "Windows", "7.0")]
        [InlineData("net5.0-WINDOWS7.0", ".NETCoreApp", "v5.0", "Windows", "7.0")]
        [InlineData("net5.0-windows", ".NETCoreApp", "v5.0", "Windows", "7.0")]
        [InlineData("net5.0-windows10.0.19041", ".NETCoreApp", "v5.0", "Windows", "10.0.19041")]
        public void It_defines_target_platform_from_target_framework(string targetFramework, string expectedTargetFrameworkIdentifier, string expectedTargetFrameworkVersion, string expectedTargetPlatformIdentifier, string expectedTargetPlatformVersion)
        {
            var testProj = new TestProject()
            {
                Name = "TargetPlatformTests",
                IsSdkProject = true, 
                TargetFrameworks = targetFramework
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProj);

            Action<string, string> assertValue = (string valueName, string expected) =>
            {
                var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.Path, testProj.Name), targetFramework, valueName);
                getValuesCommand
                    .Execute()
                    .Should()
                    .Pass();
                if (expected.Trim().Equals(string.Empty))
                {
                    getValuesCommand.GetValues().Count.Should().Be(0);
                }
                else
                {
                    getValuesCommand.GetValues().ShouldBeEquivalentTo(new[] { expected });
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

        [Fact]
        public void It_fails_on_unsupported_os()
        {
            TestProject testProject = new TestProject()
            {
                Name = "UnsupportedOS",
                IsSdkProject = true,
                TargetFrameworks = "net5.0-unsupported"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var build = new BuildCommand(Log, Path.Combine(testAsset.Path, testProject.Name));
            build.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1136");
        }
    }
}
