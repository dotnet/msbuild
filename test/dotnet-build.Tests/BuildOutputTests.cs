// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using FluentAssertions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
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
            "TestApp" + FileNameSuffixes.Deps,
            "TestApp" + FileNameSuffixes.DepsJson,
            "TestLibrary" + FileNameSuffixes.DotNet.DynamicLib,
            "TestLibrary" + FileNameSuffixes.DotNet.ProgramDatabase
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

        private void GetProjectInfo(string testRoot)
        {
            _testProjectsRoot = testRoot;
            _rootDirInfo = new DirectoryInfo(_testProjectsRoot);
            _testAppDirDirInfo = new DirectoryInfo(Path.Combine(_testProjectsRoot, "TestApp"));
            _testLibDirInfo = new DirectoryInfo(Path.Combine(_testProjectsRoot, "TestLibrary"));

            var contexts = ProjectContext.CreateContextForEachFramework(
                _testAppDirDirInfo.FullName,
                null,
                PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());
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
        public void DefaultPaths(string testIdentifer, bool global, string outputValue, string baseValue, string expectedLibCompile, string expectedAppCompile, string expectedAppRuntime)
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestAppWithLibrary", identifier: testIdentifer)
                                                .WithLockFiles();
            GetProjectInfo(testInstance.TestRoot);

            new BuildCommand(GetProjectPath(_testAppDirDirInfo),
                output: outputValue != null ? Path.Combine(_testProjectsRoot, outputValue) : string.Empty,
                buidBasePath: baseValue != null ? Path.Combine(_testProjectsRoot, baseValue) : string.Empty,
                framework: DefaultFramework)
                .ExecuteWithCapturedOutput().Should().Pass();

            var libdebug = _rootDirInfo.Sub(FormatPath(expectedLibCompile, DefaultFramework, _runtime));
            var appdebug = _rootDirInfo.Sub(FormatPath(expectedAppCompile, DefaultFramework, _runtime));
            var appruntime = _rootDirInfo.Sub(FormatPath(expectedAppRuntime, DefaultFramework, _runtime));

            libdebug.Should().Exist().And.HaveFiles(_libCompileFiles);
            appdebug.Should().Exist().And.HaveFiles(_appCompileFiles);
            appruntime.Should().Exist().And.HaveFiles(_runtimeFiles);
        }

        [Fact]
        public void SettingVersionInEnvironment_ShouldStampAssemblyInfoInOutputAssembly()
        {
            var testInstance = TestAssetsManager.CreateTestInstance("TestLibraryWithConfiguration")
                                                .WithLockFiles();

            var cmd = new BuildCommand(Path.Combine(testInstance.TestRoot, Project.FileName), framework: DefaultFramework);
            cmd.Environment["DOTNET_BUILD_VERSION"] = "85";
            cmd.Environment["DOTNET_ASSEMBLY_FILE_VERSION"] = "345";
            cmd.ExecuteWithCapturedOutput().Should().Pass();

            var output = Path.Combine(testInstance.TestRoot, "bin", "Debug", DefaultFramework, "TestLibraryWithConfiguration.dll");
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

            var cmd = new BuildCommand(Path.Combine(testInstance.TestRoot, Project.FileName), framework: DefaultFramework, versionSuffix: "85");
            cmd.ExecuteWithCapturedOutput().Should().Pass();

            var output = Path.Combine(testInstance.TestRoot, "bin", "Debug", DefaultFramework, "TestLibraryWithConfiguration.dll");
            var informationalVersion = PeReaderUtils.GetAssemblyAttributeValue(output, "AssemblyInformationalVersionAttribute");

            informationalVersion.Should().NotBeNull();
            informationalVersion.Should().BeEquivalentTo("1.0.0-85");
        }

        [Theory]
//        [InlineData("net20", false, true)]
//        [InlineData("net40", true, true)]
//        [InlineData("net461", true, true)]
        [InlineData("netstandardapp1.5", true, false)]
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

            var libdebug = _testLibDirInfo.Sub("bin/Debug").Sub(DefaultFramework);
            var appdebug = _testAppDirDirInfo.Sub("bin/Debug").Sub(DefaultFramework);
            var appruntime = appdebug.Sub(_runtime);

            foreach (var name in names)
            {
                libdebug.Sub(name).Should().Exist().And.HaveFile("TestLibrary.resources.dll");
                appdebug.Sub(name).Should().Exist().And.HaveFile("TestApp.resources.dll");
                appruntime.Sub(name).Should().Exist().And.HaveFiles(new[] { "TestLibrary.resources.dll", "TestApp.resources.dll" });
            }

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
