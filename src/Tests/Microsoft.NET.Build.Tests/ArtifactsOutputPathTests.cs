// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Build.Tests
{
    using System.Runtime.InteropServices;
    using ArtifactsTestExtensions;

    public class ArtifactsOutputPathTests : SdkTest
    {
        public ArtifactsOutputPathTests(ITestOutputHelper log) : base(log)
        {
        }

        (List<TestProject> testProjects, TestAsset testAsset) GetTestProjects(bool putArtifactsInProjectFolder = false, [CallerMemberName] string callingMethod = "")
        {
            var testProject1 = new TestProject()
            {
                Name = "App1",
                IsExe = true
            };

            var testProject2 = new TestProject()
            {
                Name = "App2",
                IsExe = true
            };

            var testLibraryProject = new TestProject()
            {
                Name = "Library",
            };

            testProject1.ReferencedProjects.Add(testLibraryProject);
            testProject2.ReferencedProjects.Add(testLibraryProject);

            List<TestProject> testProjects = new() { testProject1, testProject2, testLibraryProject };

            foreach (var testProject in testProjects)
            {
                testProject.UseArtifactsOutput = true;
            }

            var testAsset = _testAssetsManager.CreateTestProjects(testProjects, callingMethod: callingMethod, identifier: putArtifactsInProjectFolder.ToString());

            if (putArtifactsInProjectFolder)
            {
                File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"),
                    """
                    <Project>
                        <PropertyGroup>
                            <ArtifactsPath>$(MSBuildProjectDirectory)\artifacts</ArtifactsPath>
                            <IncludeProjectNameInArtifactsPaths>false</IncludeProjectNameInArtifactsPaths>
                        </PropertyGroup>
                    </Project>
                    """);
            }
            else
            {
                File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"),
                    """
                    <Project>
                        <PropertyGroup>
                            <UseArtifactsOutput>true</UseArtifactsOutput>
                        </PropertyGroup>
                    </Project>
                    """);
            }

            return (testProjects, testAsset);
        }

        [Fact]
        public void ItUsesArtifactsOutputPathForBuild()
        {
            var (testProjects, testAsset) = GetTestProjects();

            new DotnetCommand(Log, "build")
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects);

            foreach (var testProject in testProjects)
            {
                OutputPathCalculator outputPathCalculator = OutputPathCalculator.FromProject(Path.Combine(testAsset.Path, testProject.Name), testProject);
                new FileInfo(Path.Combine(outputPathCalculator.GetOutputDirectory(), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
            }
        }

        [Fact]
        public void ItUsesArtifactsOutputPathForPublish()
        {
            var (testProjects, testAsset) = GetTestProjects();

            new DotnetCommand(Log, "publish")
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects, "release");

            foreach (var testProject in testProjects)
            {
                OutputPathCalculator outputPathCalculator = OutputPathCalculator.FromProject(Path.Combine(testAsset.Path, testProject.Name), testProject);
                new FileInfo(Path.Combine(outputPathCalculator.GetOutputDirectory(configuration: "release"), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
                new FileInfo(Path.Combine(outputPathCalculator.GetPublishDirectory(configuration: "release"), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
            }
        }

        [Fact]
        public void ItUseArtifactsOutputPathForPack()
        {
            var (testProjects, testAsset) = GetTestProjects();

            new DotnetCommand(Log, "pack")
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            ValidateIntermediatePaths(testAsset, testProjects, "release");

            foreach (var testProject in testProjects)
            {
                OutputPathCalculator outputPathCalculator = OutputPathCalculator.FromProject(Path.Combine(testAsset.Path, testProject.Name), testProject);
                new FileInfo(Path.Combine(outputPathCalculator.GetOutputDirectory(configuration: "release"), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
                new FileInfo(Path.Combine(outputPathCalculator.GetPackageDirectory(configuration: "release"), testProject.Name + ".1.0.0.nupkg"))
                    .Should()
                    .Exist();
            }
        }

        void ValidateIntermediatePaths(TestAsset testAsset, IEnumerable<TestProject> testProjects, string configuration = "debug")
        {
            foreach (var testProject in testProjects)
            {
                new DirectoryInfo(Path.Combine(testAsset.TestRoot, testProject.Name))
                    .Should()
                    .NotHaveSubDirectories();

                new DirectoryInfo(Path.Combine(testAsset.TestRoot, "artifacts", "obj", testProject.Name, configuration))
                    .Should()
                    .Exist();
            }
        }

        [Fact]
        public void ArtifactsPathCanBeInProjectFolder()
        {
            var (testProjects, testAsset) = GetTestProjects(putArtifactsInProjectFolder: true);

            new DotnetCommand(Log, "build")
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            foreach (var testProject in testProjects)
            {
                var outputPathCalculator = OutputPathCalculator.FromProject(testAsset.Path, testProject);
                outputPathCalculator.IncludeProjectNameInArtifactsPaths = false;
                outputPathCalculator.ArtifactsPath = Path.Combine(testAsset.Path, testProject.Name, "artifacts");

                new DirectoryInfo(outputPathCalculator.GetIntermediateDirectory())
                        .Should()
                        .Exist();

                new FileInfo(Path.Combine(outputPathCalculator.GetOutputDirectory(), testProject.Name + ".dll"))
                    .Should()
                    .Exist();
            }
        }

        [Fact]
        public void ProjectsCanSwitchOutputFormats()
        {
            var testProject = new TestProject()
            {
                IsExe = true,
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            //  Build without artifacts format
            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(OutputPathCalculator.FromProject(testAsset.Path, testProject).GetOutputDirectory())
                .Should()
                .Exist();

            //  Now add a Directory.Build.props file setting UseArtifactsOutput to true
            File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <UseArtifactsOutput>true</UseArtifactsOutput>
                  </PropertyGroup>
                </Project>
                """);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(OutputPathCalculator.FromProject(testAsset.Path, testProject).GetOutputDirectory())
                .Should()
                .Exist();

            //  Now go back to not using artifacts output format
            File.Delete(Path.Combine(testAsset.Path, "Directory.Build.props"));

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void ProjectsCanCustomizeOutputPathBasedOnTargetFramework()
        {
            var testProject = new TestProject("CustomizeArtifactsPath")
            {
                IsExe = true,
                TargetFrameworks = "net7.0;net8.0;netstandard2.0"
            };

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"), """
                <Project>
                  <PropertyGroup>
                    <UseArtifactsOutput>true</UseArtifactsOutput>
                    <AfterTargetFrameworkInferenceTargets>$(MSBuildThisFileDirectory)\Directory.AfterTargetFrameworkInference.targets</AfterTargetFrameworkInferenceTargets>
                  </PropertyGroup>
                </Project>
                """);

            File.WriteAllText(Path.Combine(testAsset.Path, "Directory.AfterTargetFrameworkInference.targets"), """
                <Project>
                  <PropertyGroup Condition="'$(TargetFrameworkIdentifier)' == '.NETCoreApp'">
                    <ArtifactsPivots Condition="'$(_TargetFrameworkVersionWithoutV)' == '8.0'">NET8_$(Configuration)</ArtifactsPivots>
                    <ArtifactsPivots Condition="'$(_TargetFrameworkVersionWithoutV)' == '7.0'">NET7_$(Configuration)</ArtifactsPivots>
                  </PropertyGroup>
                </Project>
                """);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "bin", testProject.Name, "NET8_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "bin", testProject.Name, "NET7_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "bin", testProject.Name, "debug_netstandard2.0")).Should().Exist();

            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "bin", testProject.Name, "debug_net8.0")).Should().NotExist();
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "bin", testProject.Name, "debug_net7.0")).Should().NotExist();

            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "obj", testProject.Name, "NET8_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "obj", testProject.Name, "NET7_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "obj", testProject.Name, "debug_netstandard2.0")).Should().Exist();

            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "obj", testProject.Name, "debug_net8.0")).Should().NotExist();
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "obj", testProject.Name, "debug_net7.0")).Should().NotExist();

            foreach (var targetFramework in testProject.TargetFrameworks.Split(';'))
            {
                new DotnetPublishCommand(Log, "-f", targetFramework)
                    .WithWorkingDirectory(Path.Combine(testAsset.Path, testProject.Name))
                    .Execute()
                    .Should()
                    .Pass();
            }

            //  Note that publish defaults to release configuration for .NET 8 but not prior TargetFrameworks
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "publish", testProject.Name, "NET8_Release")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "publish", testProject.Name, "NET7_Debug")).Should().Exist();
            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "publish", testProject.Name, "debug_netstandard2.0")).Should().Exist();

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.Path, testProject.Name))
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "package", "release")).Should().Exist();
            new FileInfo(Path.Combine(testAsset.Path, "artifacts", "package", "release", testProject.Name + ".1.0.0.nupkg")).Should().Exist();
        }

        TestAsset CreateCustomizedTestProject(string propertyName, string propertyValue, [CallerMemberName] string callingMethod = "")
        {
            var testProject = new TestProject("App")
            {
                IsExe = true
            };

            testProject.UseArtifactsOutput = true;

            var testAsset = _testAssetsManager.CreateTestProjects(new[] { testProject }, callingMethod: callingMethod);

            File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"),
                $"""
                <Project>
                    <PropertyGroup>
                        <UseArtifactsOutput>true</UseArtifactsOutput>
                        <{propertyName}>{propertyValue}</{propertyName}>
                    </PropertyGroup>
                </Project>
                """);

            return testAsset;
        }

        [Fact]
        public void ArtifactsPathCanBeSet()
        {
            var artifactsFolder = _testAssetsManager.CreateTestDirectory(identifier: "ArtifactsPath").Path;

            var testAsset = CreateCustomizedTestProject("ArtifactsPath", artifactsFolder);

            new DotnetBuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            //  If ArtifactsPath is set, even in the project file itself, we still include the project name in the path,
            //  as the path used is likely to be shared between multiple projects
            new FileInfo(Path.Combine(artifactsFolder, "bin", "App", "debug", "App.dll"))
                .Should()
                .Exist();
        }

        [Fact]
        public void BinOutputNameCanBeSet()
        {
            var testAsset = CreateCustomizedTestProject("ArtifactsBinOutputName", "binaries");

            new DotnetBuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new FileInfo(Path.Combine(testAsset.Path, "artifacts", "binaries", "App", "debug", "App.dll"))
                .Should()
                .Exist();
        }

        [Fact]
        public void PublishOutputNameCanBeSet()
        {
            var testAsset = CreateCustomizedTestProject("ArtifactsPublishOutputName", "published_app");

            new DotnetPublishCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            new FileInfo(Path.Combine(testAsset.Path, "artifacts", "published_app", "App", "release", "App.dll"))
                .Should()
                .Exist();
        }

        [Fact]
        public void PackageOutputNameCanBeSet()
        {
            var testAsset = CreateCustomizedTestProject("ArtifactsPackageOutputName", "package_output");

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            new FileInfo(Path.Combine(testAsset.Path, "artifacts", "package_output", "release", "App.1.0.0.nupkg"))
                .Should()
                .Exist();
        }

        [Fact]
        public void ProjectNameCanBeSet()
        {
            var testAsset = CreateCustomizedTestProject("ArtifactsProjectName", "Apps\\MyApp");

            new DotnetBuildCommand(Log)
                .WithWorkingDirectory(testAsset.Path)
                .Execute()
                .Should()
                .Pass();

            new FileInfo(Path.Combine(testAsset.Path, "artifacts", "bin", "Apps", "MyApp", "debug", "App.dll"))
                .Should()
                .Exist();
        }

        [Fact]
        public void PackageValidationSucceeds()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = $"{ToolsetInfo.CurrentTargetFramework};net7.0"
            };

            testProject.AdditionalProperties["EnablePackageValidation"] = "True";

            testProject.UseArtifactsOutput = true;

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            File.WriteAllText(Path.Combine(testAsset.Path, "Directory.Build.props"),
                    $"""
                    <Project>
                        <PropertyGroup>
                            <UseArtifactsOutput>true</UseArtifactsOutput>
                        </PropertyGroup>
                    </Project>
                    """);

            new DotnetPackCommand(Log)
                .WithWorkingDirectory(Path.Combine(testAsset.TestRoot, testProject.Name))
                .Execute()
                .Should()
                .Pass();
        }

        [Fact]
        public void ItErrorsIfArtifactsPathIsSetInProject()
        {
            var testProject = new TestProject();
            testProject.AdditionalProperties["ArtifactsPath"] = "$(MSBuildThisFileDirectory)\\..\\artifacts";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1199");

            new DirectoryInfo(Path.Combine(testAsset.TestRoot, "artifacts"))
                .Should()
                .NotExist();
        }

        [Fact]
        public void ItErrorsIfUseArtifactsOutputIsSetInProject()
        {
            var testProject = new TestProject();
            testProject.AdditionalProperties["UseArtifactsOutput"] = "true";

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .Execute()
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1199");

            new DirectoryInfo(Path.Combine(testAsset.TestRoot, testProject.Name, "artifacts"))
                .Should()
                .NotExist();
        }

        [Fact]
        public void ItErrorsIfUseArtifactsOutputIsSetAndThereIsNoDirectoryBuildProps()
        {
            var testProject = new TestProject();

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            new BuildCommand(testAsset)
                .DisableDirectoryBuildProps()
                .Execute("/p:UseArtifactsOutput=true")
                .Should()
                .Fail()
                .And
                .HaveStdOutContaining("NETSDK1200");
        }

        [Fact]
        public void ItCanBuildWithMicrosoftBuildArtifactsSdk()
        {
            var testAsset = _testAssetsManager.CopyTestAsset("ArtifactsSdkTest")
                .WithSource();

            new DotnetBuildCommand(testAsset)
                .Execute()
                .Should()
                .Pass();

            new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "MSBuildSdk", ToolsetInfo.CurrentTargetFramework))
                .Should()
                .OnlyHaveFiles(new[] { "MSBuildSdk.dll" });

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                //  Microsoft.Build.Artifacts doesn't appear to copy the (extensionless) executable to the artifacts folder on non-Windows platforms
                new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "PackageReference", ToolsetInfo.CurrentTargetFramework))
                    .Should()
                    .OnlyHaveFiles(new[] { "PackageReference.dll", $"PackageReference{EnvironmentInfo.ExecutableExtension}" });
            }
            else
            {
                new DirectoryInfo(Path.Combine(testAsset.Path, "artifacts", "PackageReference", ToolsetInfo.CurrentTargetFramework))
                    .Should()
                    .OnlyHaveFiles(new[] { "PackageReference.dll" });
            }

            //  Verify that default bin and obj folders still exist (which wouldn't be the case if using the .NET SDKs artifacts output functianality
            new FileInfo(Path.Combine(testAsset.Path, "MSBuildSdk", "bin", "Debug", ToolsetInfo.CurrentTargetFramework, "MSBuildSdk.dll")).Should().Exist();
            new FileInfo(Path.Combine(testAsset.Path, "MSBuildSdk", "obj", "Debug", ToolsetInfo.CurrentTargetFramework, "MSBuildSdk.dll")).Should().Exist();

        }
    }

    namespace ArtifactsTestExtensions
    {
        static class Extensions
        {
            public static TestCommand DisableDirectoryBuildProps(this TestCommand command)
            {
                //  There is an empty Directory.Build.props file in the test execution root, to stop other files further up in the repo from
                //  impacting the tests.  So if a project set UseArtifactsOutput to true, the logic would find that file and put the output
                //  in that folder.  To simulate the situation where there is no Directory.Build.props, we turn it off via an environment
                //  variable.
                return command.WithEnvironmentVariable("ImportDirectoryBuildProps", "false");
            }
        }
    }
}
