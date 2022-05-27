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
using System.Runtime.CompilerServices;

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

            var asset = CreateWindowsDesktopSdkTestAsset(ProjectName, uiFrameworkProperty, uiFrameworkProperty);

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

            var asset = CreateWindowsDesktopSdkTestAsset(ProjectName, uiFrameworkProperty, uiFrameworkProperty);

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

            var asset = CreateWindowsDesktopReferenceTestAsset(ProjectName, desktopFramework, desktopFramework);

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

            var asset = CreateWindowsDesktopReferenceTestAsset(ProjectName, desktopFramework, desktopFramework);

            var command = new BuildCommand(asset);

            command
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.WindowsDesktopFrameworkRequiresWindows);
        }

        [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
        public void AppTargetingWindows10CanBuildOnNonWindows()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework + "-windows10.0.19041.0",
                IsWinExe = true
            };
            testProject.AdditionalProperties["EnableWindowsTargeting"] = "true";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();
        }

        [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX | TestPlatforms.FreeBSD)]
        public void WindowsFormsAppCanBuildOnNonWindows()
        {
            var testInstance = _testAssetsManager.CopyTestAsset("WindowsFormsTestApp")
                .WithSource();

            new BuildCommand(Log, testInstance.Path)
                .WithEnvironmentVariable("EnableWindowsTargeting", "true")
                .Execute()
                .Should()
                .Pass();
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

            var publishCommand = new PublishCommand(asset);
            var runtimeIdentifier = $"{ToolsetInfo.LatestWinRuntimeIdentifier}-x64";
            publishCommand.Execute("-p:SelfContained=true", $"-p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            Assert(publishCommand.GetOutputDirectory(tfm, runtimeIdentifier: runtimeIdentifier));

            var filesCopiedToPublishDirCommand = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, testProject.Name),
                testProject.TargetFrameworks,
                "FilesCopiedToPublishDir",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "ComputeFilesCopiedToPublishDir",
                MetadataNames = { "RelativePath" },
            };

            filesCopiedToPublishDirCommand.Execute().Should().Pass();
            var filesCopiedToPublishDircommandItems
                = from item in filesCopiedToPublishDirCommand.GetValuesWithMetadata()
                  select new
                  {
                      Identity = item.value,
                      RelativePath = item.metadata["RelativePath"]
                  };

            filesCopiedToPublishDircommandItems
                .Should().Contain(i => i.RelativePath == "Microsoft.Windows.SDK.NET.dll" && Path.GetFileName(i.Identity) == "Microsoft.Windows.SDK.NET.dll",
                                  because: "wapproj should copy cswinrt dlls");
            filesCopiedToPublishDircommandItems
                .Should()
                .Contain(i => i.RelativePath == "WinRT.Runtime.dll" && Path.GetFileName(i.Identity) == "WinRT.Runtime.dll",
                         because: "wapproj should copy cswinrt dlls");

            var publishItemsOutputGroupOutputsCommand = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, testProject.Name),
                testProject.TargetFrameworks,
                "PublishItemsOutputGroupOutputs",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Publish",
                MetadataNames = { "OutputPath" },
            };

            publishItemsOutputGroupOutputsCommand.Execute().Should().Pass();
            var publishItemsOutputGroupOutputsItems =
                from item in publishItemsOutputGroupOutputsCommand.GetValuesWithMetadata()
                select new
                {
                    FullAssetPath = Path.GetFullPath(Path.Combine(asset.Path, testProject.Name, item.metadata["OutputPath"]))
                };

            publishItemsOutputGroupOutputsItems
                .Should().Contain(i => Path.GetFileName(Path.GetFullPath(i.FullAssetPath)) == "WinRT.Runtime.dll" && File.Exists(i.FullAssetPath),
                      because: (string)"as the replacement for FilesCopiedToPublishDir, wapproj should copy cswinrt dlls");
            publishItemsOutputGroupOutputsItems
                .Should()
                .Contain(i => Path.GetFileName(Path.GetFullPath(i.FullAssetPath)) == "WinRT.Runtime.dll" && File.Exists(i.FullAssetPath),
                         because: "as the replacement for FilesCopiedToPublishDir, wapproj should copy cswinrt dlls");

            // ready to run is supported
            publishCommand.Execute("-p:SelfContained=true", $"-p:RuntimeIdentifier={runtimeIdentifier}", $"-p:PublishReadyToRun=true")
                .Should()
                .Pass();

            // PublishSingleFile is supported
            publishCommand.Execute("-p:SelfContained=true", $"-p:RuntimeIdentifier={runtimeIdentifier}", $"-p:PublishSingleFile=true")
                .Should()
                .Pass();
        }

        [WindowsOnlyRequiresMSBuildVersionFact("16.8.0")]
        public void Given_duplicated_ResolvedFileToPublish_It_Can_Publish()
        {
            const string ProjectName = "WindowsDesktopSdkTest_without_ProjectSdk_set";

            const string tfm = "net5.0";

            var testProject = new TestProject()
            {
                Name = ProjectName,
                TargetFrameworks = tfm,
                IsWinExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject).WithProjectChanges((project) =>
            {
                var ns = project.Root.Name.Namespace;
                var duplicatedResolvedFileToPublish = XElement.Parse(@"
<ItemGroup>
    <ResolvedFileToPublish Include=""obj\Debug\net5.0\WindowsDesktopSdkTest_without_ProjectSdk_set.dll"">
      <RelativePath>WindowsDesktopSdkTest_without_ProjectSdk_set.dll</RelativePath>
    </ResolvedFileToPublish>
    <ResolvedFileToPublish Include=""obj\Debug\net5.0\WindowsDesktopSdkTest_without_ProjectSdk_set.dll"">
      <RelativePath>WindowsDesktopSdkTest_without_ProjectSdk_set.dll</RelativePath>
    </ResolvedFileToPublish>
  </ItemGroup>
");
                project.Root.Add(duplicatedResolvedFileToPublish);
            });

            var publishItemsOutputGroupOutputsCommand = new GetValuesCommand(
                Log,
                Path.Combine(testAsset.Path, testProject.Name),
                testProject.TargetFrameworks,
                "PublishItemsOutputGroupOutputs",
                GetValuesCommand.ValueType.Item)
            {
                DependsOnTargets = "Publish",
                MetadataNames = { "OutputPath" },
            };

            publishItemsOutputGroupOutputsCommand.Execute().Should().Pass();
            var publishItemsOutputGroupOutputsItems =
                from item in publishItemsOutputGroupOutputsCommand.GetValuesWithMetadata()
                select new
                {
                    OutputPath = item.metadata["OutputPath"]
                };
        }

        private TestAsset CreateWindowsDesktopSdkTestAsset(string projectName, string uiFrameworkProperty, string identifier, [CallerMemberName] string callingMethod = "")
        {
            const string tfm = "netcoreapp3.0";

            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                IsWinExe = true,
            };

            testProject.AdditionalProperties.Add(uiFrameworkProperty, "true");

            return _testAssetsManager.CreateTestProject(testProject, callingMethod, identifier);
        }

        private TestAsset CreateWindowsDesktopReferenceTestAsset(string projectName, string desktopFramework, string identifier, [CallerMemberName] string callingMethod = "")
        {
            const string tfm = "netcoreapp3.0";

            var testProject = new TestProject()
            {
                Name = projectName,
                TargetFrameworks = tfm,
                IsWinExe = true,
            };

            testProject.FrameworkReferences.Add(desktopFramework);

            return _testAssetsManager.CreateTestProject(testProject, callingMethod, identifier);
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
