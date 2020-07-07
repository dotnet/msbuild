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

        [Theory]
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
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1135");
        }

        [Fact]
        public void It_errors_when_missing_transitive_windows_target_platform()
        {
            var targetFramework = "net5.0";
            TestProject testProjectA = new TestProject()
            {
                Name = "A",
                IsSdkProject = true,
                TargetFrameworks = targetFramework
            };
            testProjectA.AdditionalProperties["TargetPlatformIdentifier"] = "Windows";
            testProjectA.AdditionalProperties["TargetPlatformVersion"] = "7.0";
            testProjectA.FrameworkReferences.Add("Microsoft.WindowsDesktop.App");

            TestProject testProjectB = new TestProject()
            {
                Name = "B",
                IsSdkProject = true,
                TargetFrameworks = targetFramework
            };
            testProjectB.ReferencedProjects.Add(testProjectA);

            TestProject testProjectC = new TestProject()
            {
                Name = "C",
                IsSdkProject = true,
                TargetFrameworks = targetFramework
            };
            testProjectC.ReferencedProjects.Add(testProjectB);

            var testAsset = _testAssetsManager.CreateTestProject(testProjectC);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1135");
        }
    }
}
