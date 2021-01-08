// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using Xunit.Abstractions;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.ProjectConstruction;
using System.Xml.Linq;
using System.IO;
using System.Linq;

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
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties[propertyName] = "true";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = "custom"; // Make sure we don't get windows implicitly set as the TPI
            testProject.AdditionalProperties["TargetPlatformSupported"] = "true";
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
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                TargetFrameworks = "netcoreapp3.1"
            };
            testProjectA.AdditionalProperties[propertyName] = "true";

            TestProject testProjectB = new TestProject()
            {
                Name = "B",
                TargetFrameworks = "net5.0"
            };
            testProjectB.ReferencedProjects.Add(testProjectA);

            TestProject testProjectC = new TestProject()
            {
                Name = "C",
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

        [WindowsOnlyRequiresMSBuildVersionFact("16.8.0")]
        public void It_warns_when_specifying_windows_desktop_sdk()
        {
            var targetFramework = "net5.0-windows";
            TestProject testProject = new TestProject()
            {
                Name = "windowsDesktopSdk",
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

        [WindowsOnlyFact]
        public void It_does_not_warn_when_multitargeting()
        {
            var targetFramework = "net5.0;net472;netcoreapp3.1";
            TestProject testProject = new TestProject()
            {
                Name = "windowsDesktopSdk",
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = "Windows";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass()
                .And
                .NotHaveStdOutContaining("NETSDK1137");
        }

        [WindowsOnlyFact]
        public void It_imports_when_targeting_dotnet_3()
        {
            var targetFramework = "netcoreapp3.1";
            TestProject testProject = new TestProject()
            {
                Name = "windowsDesktopSdk",
                TargetFrameworks = targetFramework
            };
            testProject.AdditionalProperties["UseWPF"] = "true";
            testProject.AdditionalProperties["TargetPlatformIdentifier"] = "Windows";
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();

            var getValuesCommand = new GetValuesCommand(testAsset, "ImportWindowsDesktopTargets");
            getValuesCommand.Execute()
                .Should()
                .Pass();
            getValuesCommand.GetValues().ShouldBeEquivalentTo(new[] { "true" });
        }

        [WindowsOnlyFact]
        public void It_builds_successfully_when_targeting_net_framework()
        {
            var testDirectory = _testAssetsManager.CreateTestDirectory().Path;
            var newCommand = new DotnetCommand(Log, "new", "wpf", "--no-restore");
            newCommand.WorkingDirectory = testDirectory;
            newCommand.Execute()
                .Should()
                .Pass();

            // Set TargetFramework to net472
            var projFile = Path.Combine(testDirectory, Path.GetFileName(testDirectory) + ".csproj");
            var project = XDocument.Load(projFile);
            var ns = project.Root.Name.Namespace;
            project.Root.Elements(ns + "PropertyGroup").Elements(ns + "TargetFramework").Single().Value = "net472";
            project.Save(projFile);

            var buildCommand = new BuildCommand(Log, testDirectory);
            buildCommand.Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void It_fails_if_windows_target_platform_version_is_invalid()
        {
            var testProject = new TestProject()
            {
                Name = "InvalidWindowsVersion",
                TargetFrameworks = "net5.0-windows1.0"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1140");
        }

        [WindowsOnlyTheory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_succeeds_if_windows_target_platform_version_does_not_have_trailing_zeros(bool setInTargetframework)
        {
            var testProject = new TestProject()
            {
                Name = "ValidWindowsVersion",
                TargetFrameworks = setInTargetframework ? "net5.0-windows10.0.18362" : "net5.0"
            };
            if (!setInTargetframework)
            {
                testProject.AdditionalProperties["TargetPlatformIdentifier"] = "Windows";
                testProject.AdditionalProperties["TargetPlatformVersion"] = "10.0.18362";
            }
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();

            var getValuesCommand = new GetValuesCommand(testAsset, "TargetPlatformVersion");
            getValuesCommand.Execute()
                .Should()
                .Pass();
            getValuesCommand.GetValues().Should().BeEquivalentTo(new[] { "10.0.18362.0" });
        }

        [Fact]
        public void It_fails_if_target_platform_identifier_and_version_are_invalid()
        {
            var testProject = new TestProject()
            {
                Name = "InvalidTargetPlatform",
                TargetFrameworks = "net5.0-custom1.0"
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1139")
                .And
                .NotHaveStdOutContaining("NETSDK1140");
        }
    }
}
