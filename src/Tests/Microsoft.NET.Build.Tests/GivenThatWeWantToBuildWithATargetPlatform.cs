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
        [InlineData("netcoreapp3.1", "Windows", "7.0")] // Default values pre-5.0
        [InlineData("net5.0-windows", "Windows", "7.0")] // Depends on https://github.com/dotnet/wpf/pull/3177
        [InlineData("net5.0-android", "Android", "0.0")] // We don't set a default version for android
        [InlineData("net5.0-ios1.1", "iOS", "1.1")]
        [InlineData("net5.0-macos7.0", "MacOS", "7.0")]
        [InlineData("net5.0-windows10.0", "Windows", "10.0")]
        [InlineData("net5.0-ios14.0", "iOS", "14.0")]
        public void It_defines_target_platform_from_target_framework(string targetFramework, string expectedTargetPlatformIdentifier, string expectedTargetPlatformVersion)
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
                var values = getValuesCommand.GetValues();
                values.Count.Should().Be(1);
                values[0].ToUpperInvariant().Should().Be(expected.ToUpperInvariant());
            };

            assertValue("TargetPlatformIdentifier", expectedTargetPlatformIdentifier);
            assertValue("TargetPlatformVersion", expectedTargetPlatformVersion);
            assertValue("TargetPlatformMoniker", $"{expectedTargetPlatformIdentifier},Version={expectedTargetPlatformVersion}");
            assertValue("TargetPlatformDisplayName", $"{expectedTargetPlatformIdentifier} {expectedTargetPlatformVersion}");
        }

        [Fact]
        public void It_errors_on_invalid_target_framework()
        {
            var testProj = new TestProject()
            {
                Name = "TargetPlatformTests",
                IsSdkProject = true,
                TargetFrameworks = "netcoreapp3.1-windows"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProj);

            var buildCommand = new BuildCommand(Log, Path.Combine(testAsset.Path, testProj.Name));
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1134");
        }
    }
}
