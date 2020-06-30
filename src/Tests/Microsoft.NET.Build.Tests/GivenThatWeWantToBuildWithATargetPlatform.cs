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

        [Theory]
        [InlineData("netcoreapp3.1", ".NETCoreApp", "v3.1", "Windows", "7.0")] // Default values pre-5.0
        [InlineData("net5.0", ".NETCoreApp", "v5.0", "", "")]
        [InlineData("net5.0-Windows7.0", ".NETCoreApp", "v5.0", "Windows", "7.0")]
        [InlineData("net5.0-WINDOWS7.0", ".NETCoreApp", "v5.0", "Windows", "7.0")]
        [InlineData("net5.0-windows", ".NETCoreApp", "v5.0", "Windows", "7.0")] // Depends on https://github.com/dotnet/wpf/pull/3177
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
                var getValuesCommand = new GetValuesCommand(Log, Path.Combine(testAsset.Path, testProj.Name), targetFramework, valueName)
                {
                    DependsOnTargets = "Build"
                };
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
            assertValue("TargetPlatformMoniker", $"{expectedTargetPlatformIdentifier},Version={expectedTargetPlatformVersion}");
            assertValue("TargetPlatformDisplayName", $"{expectedTargetPlatformIdentifier} {expectedTargetPlatformVersion}");
        }
    }
}
