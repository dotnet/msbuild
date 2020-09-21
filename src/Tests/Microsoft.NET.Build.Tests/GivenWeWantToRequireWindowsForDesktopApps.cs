// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml.Linq;
using Microsoft.NET.Build.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.ProjectConstruction;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using System.Linq;

namespace Microsoft.NET.Build.Tests
{
    public class GivenWeWantToRequireWindowsForDesktopApps : SdkTest
    {
        public GivenWeWantToRequireWindowsForDesktopApps(ITestOutputHelper log) : base(log)
        {
        }

        [WindowsOnlyTheory]
        [InlineData("UseWPF")]
        [InlineData("UseWindowsForms")]
        public void It_builds_on_windows_with_the_windows_desktop_sdk(string uiFrameworkProperty)
        {
            const string ProjectName = "WindowsDesktopSdkTest";

            var asset = CreateWindowsDesktopSdkTestAsset(ProjectName, uiFrameworkProperty);

            var command = new BuildCommand(asset);

            command
                .Execute()
                .Should()
                .Pass();
        }

        [PlatformSpecificTheory(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
        [InlineData("UseWPF")]
        [InlineData("UseWindowsForms")]
        public void It_errors_on_nonwindows_with_the_windows_desktop_sdk(string uiFrameworkProperty)
        {
            const string ProjectName = "WindowsDesktopSdkErrorTest";

            var asset = CreateWindowsDesktopSdkTestAsset(ProjectName, uiFrameworkProperty);

            var command = new BuildCommand(asset);

            command
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.WindowsDesktopFrameworkRequiresWindows);
        }

        [WindowsOnlyTheory]
        [InlineData("Microsoft.WindowsDesktop.App")]
        [InlineData("Microsoft.WindowsDesktop.App.WindowsForms")]
        [InlineData("Microsoft.WindowsDesktop.App.WPF")]
        public void It_builds_on_windows_with_a_framework_reference(string desktopFramework)
        {
            const string ProjectName = "WindowsDesktopReferenceTest";

            var asset = CreateWindowsDesktopReferenceTestAsset(ProjectName, desktopFramework);

            var command = new BuildCommand(asset);

            command
                .Execute()
                .Should()
                .Pass();
        }

        [PlatformSpecificTheory(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
        [InlineData("Microsoft.WindowsDesktop.App")]
        [InlineData("Microsoft.WindowsDesktop.App.WindowsForms")]
        [InlineData("Microsoft.WindowsDesktop.App.WPF")]
        public void It_errors_on_nonwindows_with_a_framework_reference(string desktopFramework)
        {
            const string ProjectName = "WindowsDesktopReferenceErrorTest";

            var asset = CreateWindowsDesktopReferenceTestAsset(ProjectName, desktopFramework);

            var command = new BuildCommand(asset);

            command
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.WindowsDesktopFrameworkRequiresWindows);
        }

        [WindowsOnlyTheory]
        [InlineData("net5.0", "TargetPlatformIdentifier", "Windows", "Exe")]
        [InlineData("netcoreapp3.1", "UseWindowsForms", "true", "WinExe")]
        [InlineData("netcoreapp3.1", "UseWPF", "true", "WinExe")]
        [InlineData("netcoreapp3.1", "UseWPF", "false", "Exe")]
        public void It_infers_WinExe_output_type(string targetFramework, string propName, string propValue, string expectedOutputType)
        {
            var testProject = new TestProject()
            {
                Name = "WinExeOutput",
                TargetFrameworks = targetFramework,
                IsSdkProject = true,
                IsExe = true,
            };
            testProject.AdditionalProperties[propName] = propValue;

            var asset = _testAssetsManager.CreateTestProject(testProject);

            var getValuesCommand = new GetValuesCommand(asset, "OutputType");
            getValuesCommand
                .Execute()
                .Should()
                .Pass();

            var values = getValuesCommand.GetValues();
            values.Count.Should().Be(1);
            values.First().Should().Be(expectedOutputType);
        }

        [WindowsOnlyRequiresMSBuildVersionFact("16.8.0")]
        public void It_builds_on_windows_with_the_windows_desktop_sdk_5_0_with_ProjectSdk_set()
        {
            const string ProjectName = "WindowsDesktopSdkTest_50";

            const string tfm = "net5.0-windows";

            var testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = tfm,
                IsSdkProject = true,
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                IsWinExe = true,
            };

            testProject.SourceFiles.Add("App.xaml.cs", _fileUseWindowsType);
            testProject.AdditionalProperties.Add("UseWPF", "true");

            var asset = _testAssetsManager.CreateTestProject(testProject);

            var command = new BuildCommand(Log, Path.Combine(asset.Path, ProjectName));

            command
                .Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyRequiresMSBuildVersionFact("16.8.0")]
        public void It_builds_on_windows_with_the_windows_desktop_sdk_5_0_without_ProjectSdk_set()
        {
            const string ProjectName = "WindowsDesktopSdkTest_without_ProjectSdk_set";

            const string tfm = "net5.0";

            var testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = tfm,
                IsSdkProject = true,
                IsWinExe = true,
            };

            testProject.SourceFiles.Add("App.xaml.cs", _fileUseWindowsType);
            testProject.AdditionalProperties.Add("UseWPF", "true");
            testProject.AdditionalProperties.Add("TargetPlatformIdentifier", "Windows");

            var asset = _testAssetsManager.CreateTestProject(testProject);

            var command = new BuildCommand(Log, Path.Combine(asset.Path, ProjectName));

            command
                .Execute()
                .Should()
                .Pass();
        }

        [WindowsOnlyRequiresMSBuildVersionFact("16.8.0")]
        public void When_TargetPlatformVersion_is_set_higher_than_10_It_can_reference_cswinrt_api()
        {
            const string ProjectName = "WindowsDesktopSdkTest_without_ProjectSdk_set";

            const string tfm = "net5.0";

            var testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = tfm,
                IsSdkProject = true,
                IsWinExe = true,
            };

            testProject.SourceFiles.Add("Program.cs", _useCsWinrtApi);
            testProject.AdditionalProperties.Add("TargetPlatformIdentifier", "Windows");
            testProject.AdditionalProperties.Add("TargetPlatformVersion", "10.0.17763");

            var asset = _testAssetsManager.CreateTestProject(testProject);

            var buildCommand = new BuildCommand(Log, Path.Combine(asset.Path, ProjectName));

            buildCommand.Execute()
                .Should()
                .Pass();

            void Assert(DirectoryInfo outputDir)
            {
                outputDir.File("Microsoft.Windows.SDK.NET.dll").Exists.Should().BeTrue("The output has cswinrt dll");
                outputDir.File("WinRT.Runtime.dll").Exists.Should().BeTrue("The output has cswinrt dll");
                var runtimeconfigjson = File.ReadAllText(outputDir.File(ProjectName + ".runtimeconfig.json").FullName);
                runtimeconfigjson.Contains(@"""name"": ""Microsoft.NETCore.App""").Should().BeTrue("runtimeconfig.json only reference Microsoft.NETCore.App");
                runtimeconfigjson.Contains("Microsoft.Windows.SDK.NET").Should().BeFalse("runtimeconfig.json does not reference windows SDK");
            }

            Assert(buildCommand.GetOutputDirectory(tfm));

            var publishCommand = new PublishCommand(Log, Path.Combine(asset.Path, ProjectName));
            var runtimeIdentifier = "win-x64";
            publishCommand.Execute("-p:SelfContained=true", $"-p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            Assert(publishCommand.GetOutputDirectory(tfm, runtimeIdentifier: runtimeIdentifier));

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, testProject.Name),
                testProject.TargetFrameworks,
                "FilesCopiedToPublishDir",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "ComputeFilesCopiedToPublishDir",
                MetadataNames = { "RelativePath" },
            };

            command.Execute().Should().Pass();
            var items = from item in command.GetValuesWithMetadata()
                        select new
                        {
                            Identity = item.value,
                            RelativePath = item.metadata["RelativePath"]
                        };

            items
                .Should().Contain(i => i.RelativePath == "Microsoft.Windows.SDK.NET.dll" && Path.GetFileName(i.Identity) == "Microsoft.Windows.SDK.NET.dll",
                                  because: "wapproj should copy cswinrt dlls");
            items
                .Should()
                .Contain(i => i.RelativePath == "WinRT.Runtime.dll" && Path.GetFileName(i.Identity) == "WinRT.Runtime.dll",
                         because: "wapproj should copy cswinrt dlls");
        }

        private TestAsset CreateWindowsDesktopSdkTestAsset(string projectName, string uiFrameworkProperty)
        {
            const string tfm = "netcoreapp3.0";

            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = tfm,
                IsSdkProject = true,
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                IsWinExe = true,
            };

            testProject.AdditionalProperties.Add(uiFrameworkProperty, "true");

            return _testAssetsManager.CreateTestProject(testProject);
        }

        private TestAsset CreateWindowsDesktopReferenceTestAsset(string projectName, string desktopFramework)
        {
            const string tfm = "netcoreapp3.0";

            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = tfm,
                IsSdkProject = true,
                IsWinExe = true,
            };

            testProject.FrameworkReferences.Add(desktopFramework);

            return _testAssetsManager.CreateTestProject(testProject);
        }

        private readonly string _fileUseWindowsType = @"
using System.Windows;

namespace wpf
{
    public partial class App : Application
    {
    }

    class Program
    {
        static void Main(string[] args)
        {
        }
    }
}
";

        private readonly string _useCsWinrtApi = @"
using System;
using Windows.Data.Json;

namespace consolecswinrt
{
    class Program
    {
        static void Main(string[] args)
        {
            var rootObject = JsonObject.Parse(""{\""greet\"": \""Hello\""}"");
            Console.WriteLine(rootObject[""greet""]);
        }
    }
}
";
    }
}
