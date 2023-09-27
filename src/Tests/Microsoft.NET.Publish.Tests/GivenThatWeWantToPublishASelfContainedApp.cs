// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Tasks;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishASelfContainedApp : SdkTest
    {
        private const string TestProjectName = "HelloWorld";
        private const string TargetFramework = "netcoreapp2.1";

        public GivenThatWeWantToPublishASelfContainedApp(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_errors_when_publishing_self_contained_without_apphost()
        {
            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute(
                    "/p:SelfContained=true",
                    "/p:UseAppHost=false",
                    $"/p:TargetFramework={TargetFramework}",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining(Strings.CannotUseSelfContainedWithoutAppHost);
        }

        // repro https://github.com/dotnet/sdk/issues/2466
        [Fact]
        public void It_does_not_fail_publishing_a_self_twice()
        {
            var runtimeIdentifier = RuntimeInformation.RuntimeIdentifier;

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource();

            var msbuildArgs = new string[] { "/p:SelfContained=true",
                    $"/p:TargetFramework={TargetFramework}",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}"};

            var restoreCommand = new RestoreCommand(testAsset);

            restoreCommand.Execute(msbuildArgs);

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute(msbuildArgs)
                .Should().Pass();

            publishCommand
                .Execute(msbuildArgs)
                .Should().Pass().And.NotHaveStdOutContaining("HelloWorld.exe' already exists");
        }

        private const int PEHeaderPointerOffset = 0x3C;
        private const int SubsystemOffset = 0x5C;

        [WindowsOnlyFact]
        public void It_can_make_a_Windows_GUI_exe()
        {
            var runtimeIdentifier = EnvironmentInfo.GetCompatibleRid("netcoreapp2.0");

            var testAsset = _testAssetsManager
                .CopyTestAsset(TestProjectName)
                .WithSource()
                .WithProjectChanges(doc =>
                {
                    doc.Root.Element("PropertyGroup").Element("TargetFramework").SetValue(TargetFramework);
                });

            var publishCommand = new PublishCommand(testAsset);
            publishCommand
                .Execute(
                    "/p:SelfContained=true",
                    "/p:OutputType=WinExe",
                    $"/p:TargetFramework={TargetFramework}",
                    $"/p:RuntimeIdentifier={runtimeIdentifier}")
                .Should()
                .Pass();

            string outputDirectory = publishCommand.GetOutputDirectory(
                targetFramework: TargetFramework,
                runtimeIdentifier: runtimeIdentifier).FullName;
            byte[] fileContent = File.ReadAllBytes(Path.Combine(outputDirectory, TestProjectName + ".exe"));
            uint peHeaderOffset = BitConverter.ToUInt32(fileContent, PEHeaderPointerOffset);
            BitConverter
                .ToUInt16(fileContent, (int)(peHeaderOffset + SubsystemOffset))
                .Should()
                .Be(2);
        }

        [RequiresMSBuildVersionFact("17.4.0.41702")]
        public void It_publishes_an_app_with_a_netcoreapp_lib_reference()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithNetCoreAppLib")
                .WithSource();

            var args = new string[]
            {
                "/p:SelfContained=true",
                $"/p:TargetFramework={ToolsetInfo.CurrentTargetFramework}",
                $"/p:RuntimeIdentifier={EnvironmentInfo.GetCompatibleRid(ToolsetInfo.CurrentTargetFramework)}"
            };

            new RestoreCommand(testAsset, "main").Execute(args);

            new PublishCommand(testAsset, "main")
                .Execute(args)
                .Should()
                .Pass();
        }

        [WindowsOnlyFact]
        public void It_publishes_runtime_pack_resources()
        {
            const string tfm = $"{ToolsetInfo.CurrentTargetFramework}-windows";

            var testProject = new TestProject()
            {
                Name = "WpfProjectAllResources",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                IsWinExe = true,
            };

            testProject.AdditionalProperties.Add("UseWPF", "true");

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var rid = EnvironmentInfo.GetCompatibleRid(tfm);
            var command = new PublishCommand(testProjectInstance);

            command
                .Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(targetFramework: tfm, runtimeIdentifier: rid);

            output.Should().HaveFiles(new[] {
                "cs/PresentationCore.resources.dll",
                "de/PresentationCore.resources.dll",
                "es/PresentationCore.resources.dll",
                "fr/PresentationCore.resources.dll",
                "it/PresentationCore.resources.dll",
                "ja/PresentationCore.resources.dll",
                "ko/PresentationCore.resources.dll",
                "pl/PresentationCore.resources.dll",
                "pt-BR/PresentationCore.resources.dll",
                "ru/PresentationCore.resources.dll",
                "tr/PresentationCore.resources.dll",
                "zh-Hans/PresentationCore.resources.dll",
                "zh-Hant/PresentationCore.resources.dll",
            });
        }

        [WindowsOnlyFact]
        public void It_publishes_runtime_pack_resources_for_specific_languages()
        {
            const string tfm = $"{ToolsetInfo.CurrentTargetFramework}-windows";

            var testProject = new TestProject()
            {
                Name = "WpfProjectSelectResources",
                TargetFrameworks = tfm,
                ProjectSdk = "Microsoft.NET.Sdk.WindowsDesktop",
                IsWinExe = true,
            };

            testProject.AdditionalProperties.Add("UseWPF", "true");
            testProject.AdditionalProperties.Add("SatelliteResourceLanguages", "cs;zh-Hant;ko");

            var testProjectInstance = _testAssetsManager.CreateTestProject(testProject);

            var rid = EnvironmentInfo.GetCompatibleRid(tfm);
            var command = new PublishCommand(testProjectInstance);

            command
                .Execute($"/p:RuntimeIdentifier={rid}", "/p:SelfContained=true")
                .Should()
                .Pass();

            var output = command.GetOutputDirectory(targetFramework: tfm, runtimeIdentifier: rid);

            output
                .Should()
                .HaveFiles(new[] {
                    "cs/PresentationCore.resources.dll",
                    "ko/PresentationCore.resources.dll",
                    "zh-Hant/PresentationCore.resources.dll",
                })
                .And
                .NotHaveSubDirectories(new[] {
                    "de",
                    "es",
                    "fr",
                    "it",
                    "ja",
                    "pl",
                    "pt-BR",
                    "ru",
                    "tr",
                    "zh-Hans",
                });
        }

        [RequiresMSBuildVersionFact("17.0.0.32901")]
        public void NoStaticLibs()
        {
            var testAsset = _testAssetsManager
               .CopyTestAsset(TestProjectName)
               .WithSource();

            var publishCommand = new PublishCommand(testAsset);
            var tfm = ToolsetInfo.CurrentTargetFramework;
            var rid = RuntimeInformation.RuntimeIdentifier;
            publishCommand
                .Execute(
                    "/p:SelfContained=true",
                    $"/p:TargetFramework={tfm}",
                    $"/p:RuntimeIdentifier={rid}")
                .Should()
                .Pass();

            var output = publishCommand.GetOutputDirectory(targetFramework: tfm, runtimeIdentifier: rid);
            output.Should()
                .NotHaveFilesMatching("*.lib", SearchOption.AllDirectories)
                .And
                .NotHaveFilesMatching("*.a", SearchOption.AllDirectories);
        }
    }
}
