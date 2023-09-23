// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToBuildAnAppWithLibrary : SdkTest
    {
        public GivenThatWeWantToBuildAnAppWithLibrary(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void It_builds_the_project_successfully()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            VerifyAppBuilds(testAsset);
        }

        [Fact]
        public void It_builds_the_project_successfully_twice()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            for (int i = 0; i < 2; i++)
            {
                VerifyAppBuilds(testAsset);
            }
        }

        void VerifyAppBuilds(TestAsset testAsset)
        {
            var buildCommand = new BuildCommand(testAsset, "TestApp");
            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);

            buildCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                $"TestApp{EnvironmentInfo.ExecutableExtension}",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.json",
                "TestLibrary.dll",
                "TestLibrary.pdb",
            });

            new DotnetCommand(Log, Path.Combine(outputDirectory.FullName, "TestApp.dll"))
                .Execute()
                .Should()
                .Pass()
                .And
                .HaveStdOutContaining("This string came from the test library!");

            var appInfo = FileVersionInfo.GetVersionInfo(Path.Combine(outputDirectory.FullName, "TestApp.dll"));
            appInfo.CompanyName.Should().Be("Test Authors");
            appInfo.FileVersion.Should().Be("1.2.3.0");
            appInfo.FileDescription.Should().Be("Test AssemblyTitle");
            appInfo.LegalCopyright.Should().Be("Copyright (c) Test Authors");
            appInfo.ProductName.Should().Be("Test Product");
            appInfo.ProductVersion.Should().Be("1.2.3-beta");

            var libInfo = FileVersionInfo.GetVersionInfo(Path.Combine(outputDirectory.FullName, "TestLibrary.dll"));
            libInfo.CompanyName.Trim().Should().Be("TestLibrary");
            libInfo.FileVersion.Should().Be("42.43.44.45");
            libInfo.FileDescription.Should().Be("TestLibrary");
            libInfo.LegalCopyright.Trim().Should().BeEmpty();
            libInfo.ProductName.Should().Be("TestLibrary");
            libInfo.ProductVersion.Should().Be("42.43.44.45-alpha");
        }

        [Fact]
        public void It_generates_satellite_assemblies()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("KitchenSink")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset, "TestApp");
            buildCommand
                .Execute("/p:PredefinedCulturesOnly=false")
                .Should()
                .Pass();

            var outputDir = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);

            var commandResult = new DotnetCommand(Log, Path.Combine(outputDir.FullName, "TestApp.dll"))
                .Execute();

            commandResult.Should().Pass();

            Dictionary<string, string> cultureValueMap = new()
            {
                {"", "Welcome to .Net!"},
                {"da", "Velkommen til .Net!"},
                {"de", "Willkommen in .Net!"},
                {"fr", "Bienvenue à .Net!"}
            };

            foreach (var cultureValuePair in cultureValueMap)
            {
                var culture = cultureValuePair.Key;
                var val = cultureValuePair.Value;

                if (culture != "")
                {
                    var cultureDir = new DirectoryInfo(Path.Combine(outputDir.FullName, culture));
                    cultureDir.Should().Exist();
                    cultureDir.Should().HaveFile("TestApp.resources.dll");
                    cultureDir.Should().HaveFile("TestLibrary.resources.dll");
                }

                commandResult.Should().HaveStdOutContaining(val);
            }
        }

        [WindowsOnlyFact]
        public void The_clean_target_removes_all_files_from_the_output_folder()
        {
            var testAsset = _testAssetsManager
                .CopyTestAsset("AppWithLibrary")
                .WithSource();

            var buildCommand = new BuildCommand(testAsset, "TestApp");

            buildCommand
                .Execute()
                .Should()
                .Pass();

            var outputDirectory = buildCommand.GetOutputDirectory(ToolsetInfo.CurrentTargetFramework);

            outputDirectory.Should().OnlyHaveFiles(new[] {
                "TestApp.dll",
                "TestApp.pdb",
                $"TestApp{EnvironmentInfo.ExecutableExtension}",
                "TestApp.deps.json",
                "TestApp.runtimeconfig.json",
                "TestLibrary.dll",
                "TestLibrary.pdb"
            });

            var cleanCommand = new MSBuildCommand(Log, "Clean", buildCommand.FullPathProjectFile);

            cleanCommand
                .Execute()
                .Should()
                .Pass();

            outputDirectory.Should().OnlyHaveFiles(Array.Empty<string>());
        }

        [Fact]
        public void An_appx_app_can_reference_a_cross_targeted_library()
        {
            var asset = _testAssetsManager
                .CopyTestAsset("AppxReferencingCrossTargeting")
                .WithSource();

            var buildCommand = new BuildCommand(asset, "Appx");

            buildCommand
                .Execute()
                .Should()
                .Pass();
        }
    }
}
