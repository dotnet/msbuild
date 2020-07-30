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

        [WindowsOnlyRequiresMSBuildVersionTheory("16.7.0-preview-20310-07")]
        [InlineData("UseWindowsForms")]
        [InlineData("UseWPF")]
        public void It_errors_when_missing_windows_target_platform(string propertyName)
        {
            var targetFramework = "net5.0";
            TestProject testProject = new TestProject()
            {
                Name = "MissingTargetPlatform",
                IsSdkProject = true,
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties[propertyName] = "true";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = "custom"; // Make sure we don't get windows implicitly set as the TPI
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: propertyName);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1136");
        }

        [WindowsOnlyRequiresMSBuildVersionTheory("16.7.0-preview-20310-07")]
        [InlineData("UseWindowsForms")]
        [InlineData("UseWPF")]
        public void It_errors_when_missing_transitive_windows_target_platform(string propertyName)
        {
            TestProject testProjectA = new TestProject()
            {
                Name = "A",
                IsSdkProject = true,
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                TargetFrameworks = "netcoreapp3.1"
            };
            testProjectA.AdditionalProperties[propertyName] = "true";

            TestProject testProjectB = new TestProject()
            {
                Name = "B",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };
            testProjectB.ReferencedProjects.Add(testProjectA);

            TestProject testProjectC = new TestProject()
            {
                Name = "C",
                IsSdkProject = true,
                TargetFrameworks = "net5.0"
            };
            testProjectC.ReferencedProjects.Add(testProjectB);

            var testAsset = _testAssetsManager.CreateTestProject(testProjectC);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1136");
        }

        [WindowsOnlyFact]
        public void It_warns_when_specifying_windows_desktop_sdk()
        {
            var targetFramework = "net5.0";
            TestProject testProject = new TestProject()
            {
                Name = "windowsDesktopSdk",
                IsSdkProject = true,
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("NETSDK1137");
        }
    }
}
