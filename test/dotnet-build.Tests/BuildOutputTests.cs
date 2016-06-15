// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildOutputTests : TestBase
    {
        private string _testProjectsRoot;
        private string _runtime;
        private DirectoryInfo _rootDirInfo;
        private DirectoryInfo _testAppDirDirInfo;
        private DirectoryInfo _testLibDirInfo;

        private readonly string[] _runtimeFiles =
        {
            "TestApp" + FileNameSuffixes.DotNet.DynamicLib,
            "TestApp" + FileNameSuffixes.DotNet.ProgramDatabase,
            "TestApp" + FileNameSuffixes.CurrentPlatform.Exe,
            "TestApp" + FileNameSuffixes.DepsJson,
            "TestApp" + FileNameSuffixes.RuntimeConfigJson,
            "TestLibrary" + FileNameSuffixes.DotNet.DynamicLib,
            "TestLibrary" + FileNameSuffixes.DotNet.ProgramDatabase
        };

        private readonly string[] _runtimeExcludeFiles =
        {
            "TestLibrary" + FileNameSuffixes.RuntimeConfigJson,
            "TestLibrary" + FileNameSuffixes.RuntimeConfigDevJson
        };

        private readonly string[] _appCompileFiles =
        {
            "TestApp" + FileNameSuffixes.DotNet.DynamicLib,
            "TestApp" + FileNameSuffixes.DotNet.ProgramDatabase
        };

        private readonly string[] _libCompileFiles =
        {
            "TestLibrary" + FileNameSuffixes.DotNet.DynamicLib,
            "TestLibrary" + FileNameSuffixes.DotNet.ProgramDatabase
        };

        private readonly string[] _libCompileExcludeFiles =
        {
            "TestLibrary" + FileNameSuffixes.RuntimeConfigJson,
            "TestLibrary" + FileNameSuffixes.RuntimeConfigDevJson
        };


        private void GetProjectInfo(string testRoot)
        {
            _testProjectsRoot = testRoot;
            _rootDirInfo = new DirectoryInfo(_testProjectsRoot);
            _testAppDirDirInfo = new DirectoryInfo(Path.Combine(_testProjectsRoot, "TestApp"));
            _testLibDirInfo = new DirectoryInfo(Path.Combine(_testProjectsRoot, "TestLibrary"));

            var contexts = ProjectContext.CreateContextForEachFramework(
                _testAppDirDirInfo.FullName,
                null,
                RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());
            _runtime = contexts.FirstOrDefault(c => !string.IsNullOrEmpty(c.RuntimeIdentifier))?.RuntimeIdentifier;
        }

        private string FormatPath(string input, string framework, string runtime)
        {
            return input.Replace("{fw}", framework).Replace("{rid}", runtime);
        }

        [Theory]
        // global.json exists
        [InlineData("1", true, null, null, "TestLibrary/bin/Debug/{fw}", "TestApp/bin/Debug/{fw}", "TestApp/bin/Debug/{fw}/{rid}")]
        [InlineData("2", true, "out", null, "TestLibrary/bin/Debug/{fw}", "TestApp/bin/Debug/{fw}", "out")]
        [InlineData("3", true, null, "build", "build/TestLibrary/bin/Debug/{fw}", "build/TestApp/bin/Debug/{fw}", "build/TestApp/bin/Debug/{fw}/{rid}")]
        [InlineData("4", true, "out", "build", "build/TestLibrary/bin/Debug/{fw}", "build/TestApp/bin/Debug/{fw}", "out")]
        //no global.json
        //[InlineData(false, null, null, "TestLibrary/bin/debug/{fw}", "TestApp/bin/debug/{fw}", "TestApp/bin/debug/{fw}/{rid}")]
        //[InlineData(false, "out", null, "TestLibrary/bin/debug/{fw}", "TestApp/bin/debug/{fw}", "out")]
        //[InlineData(false, null, "build", "build/TestLibrary/bin/debug/{fw}", "build/TestApp/bin/debug/{fw}", "build/TestApp/bin/debug/{fw}/{rid}")]
        //[InlineData(false, "out", "build", "build/TestLibrary/bin/debug/{fw}", "build/TestApp/bin/debug/{fw}", "out")]
        public void AppDefaultPaths(string testIdentifer, bool global, string outputValue, string baseValue, string expectedLibCompile, string expectedAppCompile, string expectedAppRuntime)
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary", identifier: testIdentifer)
                                                .WithLockFiles();
            GetProjectInfo(testInstance.TestRoot);

            new BuildCommand(GetProjectPath(_testAppDirDirInfo),
                output: outputValue != null ? Path.Combine(_testProjectsRoot, outputValue) : string.Empty,
                buildBasePath: baseValue != null ? Path.Combine(_testProjectsRoot, baseValue) : string.Empty,
                framework: DefaultFramework)
                .ExecuteWithCapturedOutput().Should().Pass();

            var libdebug = _rootDirInfo.Sub(FormatPath(expectedLibCompile, DefaultLibraryFramework, _runtime));
            var appdebug = _rootDirInfo.Sub(FormatPath(expectedAppCompile, DefaultFramework, _runtime));
            var appruntime = _rootDirInfo.Sub(FormatPath(expectedAppRuntime, DefaultFramework, _runtime));

            libdebug.Should().Exist()
                .And.HaveFiles(_libCompileFiles)
                .And.NotHaveFiles(_libCompileExcludeFiles);
            appdebug.Should().Exist().And.HaveFiles(_appCompileFiles);
            appruntime.Should().Exist()
                .And.HaveFiles(_runtimeFiles)
                .And.NotHaveFiles(_runtimeExcludeFiles);
        }

        [Theory]
        [InlineData("1", true, null, null, "TestLibrary/bin/Debug/{fw}", "TestLibrary/bin/Debug/{fw}/{rid}")]
        [InlineData("2", true, "out", null, "TestLibrary/bin/Debug/{fw}", "out")]
        [InlineData("3", true, null, "build", "build/TestLibrary/bin/Debug/{fw}", "build/TestLibrary/bin/Debug/{fw}/{rid}")]
        [InlineData("4", true, "out", "build", "build/TestLibrary/bin/Debug/{fw}", "out")]
        public void LibDefaultPaths(string testIdentifer, bool global, string outputValue, string baseValue, string expectedLibCompile, string expectedLibOutput)
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary", identifier: testIdentifer)
                                                .WithLockFiles();
            GetProjectInfo(testInstance.TestRoot);

            new BuildCommand(GetProjectPath(_testLibDirInfo),
                output: outputValue != null ? Path.Combine(_testProjectsRoot, outputValue) : string.Empty,
                buildBasePath: baseValue != null ? Path.Combine(_testProjectsRoot, baseValue) : string.Empty,
                framework: DefaultLibraryFramework)
                .ExecuteWithCapturedOutput().Should().Pass();

            var libdebug = _rootDirInfo.Sub(FormatPath(expectedLibCompile, DefaultLibraryFramework, _runtime));

            libdebug.Should().Exist()
                .And.HaveFiles(_libCompileFiles)
                .And.NotHaveFiles(_libCompileExcludeFiles);
        }

        [Fact]
        public void SettingVersionInEnvironment_ShouldStampAssemblyInfoInOutputAssembly()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestLibraryWithConfiguration")
                                                .WithLockFiles();

            var cmd = new BuildCommand(Path.Combine(testInstance.TestRoot, Project.FileName), framework: DefaultLibraryFramework);
            cmd.Environment["DOTNET_BUILD_VERSION"] = "85";
            cmd.Environment["DOTNET_ASSEMBLY_FILE_VERSION"] = "345";
            cmd.ExecuteWithCapturedOutput().Should().Pass();

            var output = Path.Combine(testInstance.TestRoot, "bin", "Debug", DefaultLibraryFramework, "TestLibraryWithConfiguration.dll");
            var informationalVersion = PeReaderUtils.GetAssemblyAttributeValue(output, "AssemblyInformationalVersionAttribute");
            var fileVersion = PeReaderUtils.GetAssemblyAttributeValue(output, "AssemblyFileVersionAttribute");

            informationalVersion.Should().NotBeNull();
            informationalVersion.Should().BeEquivalentTo("1.0.0-85");

            fileVersion.Should().NotBeNull();
            fileVersion.Should().BeEquivalentTo("1.0.0.345");
        }

        [Fact]
        public void SettingVersionSuffixFlag_ShouldStampAssemblyInfoInOutputAssembly()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestLibraryWithConfiguration")
                                                .WithLockFiles();

            var cmd = new BuildCommand(Path.Combine(testInstance.TestRoot, Project.FileName), framework: DefaultLibraryFramework, versionSuffix: "85");
            cmd.ExecuteWithCapturedOutput().Should().Pass();

            var output = Path.Combine(testInstance.TestRoot, "bin", "Debug", DefaultLibraryFramework, "TestLibraryWithConfiguration.dll");
            var informationalVersion = PeReaderUtils.GetAssemblyAttributeValue(output, "AssemblyInformationalVersionAttribute");

            informationalVersion.Should().NotBeNull();
            informationalVersion.Should().BeEquivalentTo("1.0.0-85");
        }

        [Fact]
        public void BuildGlobbingMakesAllRunnable()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("AppWithAppDependency")
                                                .WithLockFiles();

            var cmd = new BuildCommand(string.Format("*{0}project.json", Path.DirectorySeparatorChar), skipLoadProject: true)
                .WithWorkingDirectory(testInstance.TestRoot)
                .Execute()
                .Should()
                .Pass();

            foreach (var project in new [] { "TestApp1", "TestApp2" })
            {
                new DirectoryInfo(Path.Combine(testInstance.TestRoot, project, "bin", "Debug", DefaultFramework))
                    .Should().HaveFile($"{project}.deps.json");
            }
        }

        [Theory]
        //        [InlineData("net20", false, true)]
        //        [InlineData("net40", true, true)]
        //        [InlineData("net461", true, true)]
        [InlineData("netstandard1.5", true, false)]
        public void MultipleFrameworks_ShouldHaveValidTargetFrameworkAttribute(string frameworkName, bool shouldHaveTargetFrameworkAttribute, bool windowsOnly)
        {
            var framework = NuGetFramework.Parse(frameworkName);

            var testInstance = TestAssetsManager.CreateTestInstance("TestLibraryWithMultipleFrameworks")
                                                .WithLockFiles();

            var cmd = new BuildCommand(Path.Combine(testInstance.TestRoot, Project.FileName), framework: framework.GetShortFolderName());

            if (windowsOnly && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // on non-windows platforms, desktop frameworks will not build
                cmd.ExecuteWithCapturedOutput().Should().Fail();
            }
            else
            {
                cmd.ExecuteWithCapturedOutput().Should().Pass();

                var output = Path.Combine(testInstance.TestRoot, "bin", "Debug", framework.GetShortFolderName(), "TestLibraryWithMultipleFrameworks.dll");
                var targetFramework = PeReaderUtils.GetAssemblyAttributeValue(output, "TargetFrameworkAttribute");

                if (shouldHaveTargetFrameworkAttribute)
                {
                    targetFramework.Should().NotBeNull();
                    targetFramework.Should().BeEquivalentTo(framework.DotNetFrameworkName);
                }
                else
                {
                    targetFramework.Should().BeNull();
                }
            }
        }

        [Fact]
        public void UnresolvedReferenceCausesBuildToFailAndNotProduceOutput()
        {
            var testAssetsManager = GetTestGroupTestAssetsManager("NonRestoredTestProjects");
            var testInstance = testAssetsManager.CreateTestInstance("TestProjectWithUnresolvedDependency")
                                                .WithLockFiles();

            var restoreResult = new RestoreCommand() { WorkingDirectory = testInstance.TestRoot }.Execute();
            restoreResult.Should().Fail();
            new DirectoryInfo(testInstance.TestRoot).Should().HaveFile("project.lock.json");

            var buildCmd = new BuildCommand(testInstance.TestRoot);
            var buildResult = buildCmd.ExecuteWithCapturedOutput();
            buildResult.Should().Fail();

            buildResult.StdErr.Should().Contain("The dependency ThisIsNotARealDependencyAndIfSomeoneGoesAndAddsAProjectWithThisNameIWillFindThemAndPunishThem could not be resolved.");

            var outputDir = new DirectoryInfo(Path.Combine(testInstance.TestRoot, "bin", "Debug", "netcoreapp1.0"));
            outputDir.GetFiles().Length.Should().Be(0);
        }

        [Fact]
        public void PackageReferenceWithResourcesTest()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("ResourcesTests")
                                                .WithLockFiles();

            var projectRoot = Path.Combine(testInstance.TestRoot, "TestApp");

            var cmd = new BuildCommand(projectRoot);
            var result = cmd.Execute();
            result.Should().Pass();

            var outputDir = new DirectoryInfo(Path.Combine(projectRoot, "bin", "Debug", "netcoreapp1.0"));

            outputDir.Should().HaveFile("TestLibraryWithResources.dll");
            outputDir.Sub("fr").Should().HaveFile("TestLibraryWithResources.resources.dll");

            var depsJson = JObject.Parse(File.ReadAllText(Path.Combine(outputDir.FullName, $"{Path.GetFileNameWithoutExtension(cmd.GetOutputExecutableName())}.deps.json")));

            foreach (var library in new[] { Tuple.Create("Microsoft.Data.OData", "5.6.4"), Tuple.Create("TestLibraryWithResources", "1.0.0") })
            {
                var resources = depsJson["targets"][".NETCoreApp,Version=v1.0"][library.Item1 + "/" + library.Item2]["resources"];

                resources.Should().NotBeNull();

                foreach (var item in resources.Children<JProperty>())
                {
                    var locale = item.Value["locale"];
                    locale.Should().NotBeNull();

                    item.Name.Should().EndWith($"{locale}/{library.Item1}.resources.dll");
                }
            }
        }

        [Fact]
        public void ResourceTest()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary")
                                                .WithLockFiles();
            GetProjectInfo(testInstance.TestRoot);

            var names = new[]
            {
                "uk-UA",
                "en",
                "en-US"
            };
            foreach (var folder in new[] { _testAppDirDirInfo, _testLibDirInfo })
            {
                foreach (var name in names)
                {
                    var resourceFile = Path.Combine(folder.FullName, $"Resource.{name}.resx");
                    File.WriteAllText(resourceFile, "<root></root>");
                }
            }

            new BuildCommand(GetProjectPath(_testAppDirDirInfo), framework: DefaultFramework)
                .ExecuteWithCapturedOutput().Should().Pass();

            var libdebug = _testLibDirInfo.Sub("bin/Debug").Sub(DefaultLibraryFramework);
            var appdebug = _testAppDirDirInfo.Sub("bin/Debug").Sub(DefaultFramework);
            var appruntime = appdebug.Sub(_runtime);

            foreach (var name in names)
            {
                libdebug.Sub(name).Should().Exist().And.HaveFile("TestLibrary.resources.dll");
                appdebug.Sub(name).Should().Exist().And.HaveFile("TestApp.resources.dll");
                appruntime.Sub(name).Should().Exist().And.HaveFiles(new[] { "TestLibrary.resources.dll", "TestApp.resources.dll" });
            }

        }

        [Fact]
        private void StandaloneApp_WithoutCoreClrDll_Fails()
        {
            // Convert a Portable App to Standalone to simulate the customer scenario
            var testInstance = TestAssetsManager.CreateTestInstance("DependencyChangeTest")
                                .WithLockFiles();

            // Convert the portable test project to standalone by removing "type": "platform" and adding rids
            var originalTestProject = Path.Combine(testInstance.TestRoot, "PortableApp_Standalone", "project.json");
            var modifiedTestProject = Path.Combine(testInstance.TestRoot, "PortableApp_Standalone", "project.json.modified");

            // Simulate a user editting the project.json
            File.Delete(originalTestProject);
            File.Copy(modifiedTestProject, originalTestProject);

            var buildResult = new BuildCommand(originalTestProject, framework: DefaultFramework)
                .ExecuteWithCapturedOutput();

            buildResult.Should().Fail();

            buildResult.StdErr.Should().Contain("Can not find runtime target for framework '.NETCoreApp,Version=v1.0' compatible with one of the target runtimes");
            buildResult.StdErr.Should().Contain("The project has not been restored or restore failed - run `dotnet restore`");
        }

        [Fact]
        private void App_WithSelfReferencingDependency_FailsBuild()
        {
            var testAssetsManager = GetTestGroupTestAssetsManager("NonRestoredTestProjects");
            var testInstance = testAssetsManager.CreateTestInstance("TestProjectWithSelfReferencingDependency")
                                                .WithLockFiles();

            var restoreResult = new RestoreCommand() { WorkingDirectory = testInstance.TestRoot }.ExecuteWithCapturedOutput();
            restoreResult.Should().Fail();
            restoreResult.StdOut.Should().Contain("error: Cycle detected");
        }

        private void CopyProjectToTempDir(string projectDir, TempDirectory tempDir)
        {
            // copy all the files to temp dir
            foreach (var file in Directory.EnumerateFiles(projectDir))
            {
                tempDir.CopyFile(file);
            }
        }

        private string GetProjectPath(DirectoryInfo projectDir)
        {
            return Path.Combine(projectDir.FullName, "project.json");
        }
    }
}
