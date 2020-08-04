// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAWindowsDesktopProject : SdkTest
    {
        public GivenThatWeWantToBuildAWindowsDesktopProject(ITestOutputHelper log) : base(log)
        {}

        [WindowsOnlyFact]
        public void It_fails_if_windows_target_platform_version_is_invalid()
        {
            var testProject = new TestProject()
            {
                Name = "InvalidWindowsVersion",
                IsSdkProject = true,
                TargetFrameworks = "net5.0-windows1.0"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1137");
        }

        [WindowsOnlyFact]
        public void It_succeeds_if_windows_target_platform_version_has_trailing_zeros()
        {
            var testProject = new TestProject()
            {
                Name = "ValidWindowsVersion",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = "windows"; 
            testProject.AdditionalProperties["TargetPlatformVersion"] = "10.0.18362.0"; // We must set this manually because if we set it in the TFM we remove the trailing zeroes. 
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void It_fails_if_target_platform_identifier_and_version_are_invalid()
        {
            var testProject = new TestProject()
            {
                Name = "InvalidTargetPlatform",
                IsSdkProject = true,
                TargetFrameworks = "net5.0-custom1.0"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1136")
                .And
                .NotHaveStdOutContaining("NETSDK1137");
        }
    }
}
