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
using System;

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
            var targetFramework = ToolsetInfo.CurrentTargetFramework;
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
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
            };
            testProjectB.ReferencedProjects.Add(testProjectA);

            TestProject testProjectC = new TestProject()
            {
                Name = "C",
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework
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
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework}-windows";
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
            var targetFramework = $"{ToolsetInfo.CurrentTargetFramework};net472;netcoreapp3.1";
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

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
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
            //  The template sets Nullable to "enable", which isn't supported on .NET Framework
            project.Root.Elements(ns + "PropertyGroup").Elements(ns + "Nullable").Remove();
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
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-windows1.0"
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
                TargetFrameworks = setInTargetframework ? $"{ToolsetInfo.CurrentTargetFramework}-windows10.0.18362" : ToolsetInfo.CurrentTargetFramework
            };
            if (!setInTargetframework)
            {
                testProject.AdditionalProperties["TargetPlatformIdentifier"] = "Windows";
                testProject.AdditionalProperties["TargetPlatformVersion"] = "10.0.18362";
            }
            var testAsset = _testAssetsManager.CreateTestProject(testProject, setInTargetframework.ToString());

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
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-custom1.0"
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

        [WindowsOnlyRequiresMSBuildVersionFact("17.0.0.32901")]
        public void UseWPFCanBeSetInDirectoryBuildTargets()
        {
            var testDir = _testAssetsManager.CreateTestDirectory();

            var newCommand = new DotnetCommand(Log);
            newCommand.WorkingDirectory = testDir.Path;

            newCommand.Execute("new", "wpf", "--debug:ephemeral-hive").Should().Pass();

            var projectPath = Path.Combine(testDir.Path, Path.GetFileName(testDir.Path) + ".csproj");

            var project = XDocument.Load(projectPath);
            var ns = project.Root.Name.Namespace;

            project.Root.Element(ns + "PropertyGroup")
                .Element(ns + "UseWPF")
                .Remove();

            project.Save(projectPath);

            string DirectoryBuildTargetsContent = @"
<Project>
  <PropertyGroup>
    <UseWPF>true</UseWPF>
  </PropertyGroup>
</Project>
";

            File.WriteAllText(Path.Combine(testDir.Path, "Directory.Build.targets"), DirectoryBuildTargetsContent);

            var buildCommand = new BuildCommand(Log, testDir.Path);

            buildCommand.Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void TargetPlatformVersionCanBeSetInDirectoryBuildTargets()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-windows"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            string targetPlatformVersion = "10.0.18362.0";

            string DirectoryBuildTargetsContent = $@"
<Project>
  <PropertyGroup>
    <TargetPlatformVersion>{targetPlatformVersion}</TargetPlatformVersion>
  </PropertyGroup>
</Project>
";

            File.WriteAllText(Path.Combine(testAsset.TestRoot, "Directory.Build.targets"), DirectoryBuildTargetsContent);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();

            GetPropertyValue(testAsset, "SupportedOSPlatformVersion").Should().Be(targetPlatformVersion);
            GetPropertyValue(testAsset, "TargetPlatformMinVersion").Should().Be(targetPlatformVersion);
            GetPropertyValue(testAsset, "TargetPlatformMoniker").Should().Be($"Windows,Version={targetPlatformVersion}");
        }

        [WindowsOnlyFact]
        public void SupportedOSPlatformVersionCanBeSetInDirectoryBuildTargets()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041.0"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            string supportedOSPlatformVersion = "10.0.18362.0";

            string DirectoryBuildTargetsContent = $@"
<Project>
  <PropertyGroup>
    <SupportedOSPlatformVersion>{supportedOSPlatformVersion}</SupportedOSPlatformVersion>
  </PropertyGroup>
</Project>
";

            File.WriteAllText(Path.Combine(testAsset.TestRoot, "Directory.Build.targets"), DirectoryBuildTargetsContent);

            var buildCommand = new BuildCommand(testAsset);
            buildCommand.Execute()
                .Should()
                .Pass();

            GetPropertyValue(testAsset, "SupportedOSPlatformVersion").Should().Be(supportedOSPlatformVersion);
            GetPropertyValue(testAsset, "TargetPlatformMinVersion").Should().Be(supportedOSPlatformVersion);
            GetPropertyValue(testAsset, "TargetPlatformVersion").Should().Be("10.0.19041.0");
            GetPropertyValue(testAsset, "TargetPlatformMoniker").Should().Be("Windows,Version=10.0.19041.0");
        }

        [WindowsOnlyTheory]
        [InlineData(ToolsetInfo.CurrentTargetFramework, true)]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041.0", true)]
        [InlineData("netcoreapp3.1", false)]
        [InlineData("net472", false)]
        public void WindowsWorkloadIsInstalledForNet5AndUp(string targetFramework, bool supportsWindowsTargetPlatformIdentifier)
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = targetFramework
            };
            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework);

            var getValueCommand = new GetValuesCommand(testAsset, "SdkSupportedTargetPlatformIdentifier", GetValuesCommand.ValueType.Item);
            getValueCommand.Execute()
                .Should()
                .Pass();

            if (supportsWindowsTargetPlatformIdentifier)
            {
                getValueCommand.GetValues()
                    .Should()
                    .Contain("windows");
            }
            else
            {
                getValueCommand.GetValues()
                    .Should()
                    .NotContain("windows");
            }
        }

        [WindowsOnlyTheory]
        //  Basic Windows TargetFramework
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041.0", false, null, "10.0.19041.*")]
        //  Basic UseWindowsSdkPreview usage
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.99999.0", true, null, "10.0.99999-preview")]
        //  Basic WindowsSdkPackageVersion usage
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041.0", null, "10.0.99999-abc", "10.0.99999-abc")]
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041.0", null, "10.0.99999.0", "10.0.99999.0")]
        //  WindowsSdkPackageVersion should supercede UseWindowsSDKPreview property
        [InlineData($"{ToolsetInfo.CurrentTargetFramework}-windows10.0.19041.0", true, "10.0.99999-abc", "10.0.99999-abc")]
        public void ItUsesCorrectWindowsSdkPackVersion(string targetFramework, bool? useWindowsSDKPreview, string windowsSdkPackageVersion, string expectedWindowsSdkPackageVersion)
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = targetFramework
            };
            if (useWindowsSDKPreview != null)
            {
                testProject.AdditionalProperties["UsewindowsSdkPreview"] = useWindowsSDKPreview.Value.ToString();
            }
            if (!string.IsNullOrEmpty(windowsSdkPackageVersion))
            {
                testProject.AdditionalProperties["WindowsSdkPackageVersion"] = windowsSdkPackageVersion;
            }

            var testAsset = _testAssetsManager.CreateTestProject(testProject, identifier: targetFramework + useWindowsSDKPreview + windowsSdkPackageVersion);

            var getValueCommand = new GetValuesCommand(testAsset, "PackageDownload", GetValuesCommand.ValueType.Item);
            getValueCommand.ShouldRestore = false;
            getValueCommand.DependsOnTargets = "_CheckForInvalidConfigurationAndPlatform;CollectPackageDownloads";
            getValueCommand.MetadataNames.Add("Version");
            getValueCommand.Execute()
                .Should()
                .Pass();

            var packageDownloadValues = getValueCommand.GetValuesWithMetadata().Where(kvp => kvp.value == "Microsoft.Windows.SDK.NET.Ref").ToList();

            packageDownloadValues.Count.Should().Be(1);

            var packageDownloadVersion = packageDownloadValues.Single().metadata["Version"];
            packageDownloadVersion[0].Should().Be('[');
            packageDownloadVersion.Last().Should().Be(']');

            //  The patch version of the Windows SDK Ref pack will change over time, so we use a '*' in the expected version to indicate that and replace it with
            //  the 4th part of the version number of the resolved package.
            var trimmedPackageDownloadVersion = packageDownloadVersion.Substring(1, packageDownloadVersion.Length - 2);
            if (expectedWindowsSdkPackageVersion.Contains('*'))
            {
                expectedWindowsSdkPackageVersion = expectedWindowsSdkPackageVersion.Replace("*", new Version(trimmedPackageDownloadVersion).Revision.ToString());
            }

            trimmedPackageDownloadVersion.Should().Be(expectedWindowsSdkPackageVersion);
        }

        private string GetPropertyValue(TestAsset testAsset, string propertyName)
        {
            var getValueCommand = new GetValuesCommand(testAsset, propertyName);
            getValueCommand.Execute()
                .Should()
                .Pass();

            return getValueCommand.GetValues().Single();
        }
    }
}
