// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Common;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test.Utilities;
using Microsoft.Extensions.PlatformAbstractions;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.Tools.Builder.Tests
{
    public class BuildOutputTests : TestBase
    {
        private readonly string _testProjectsRoot;
        private readonly string[] _runtimeFiles =
        {
            "TestApp" + FileNameSuffixes.DotNet.DynamicLib,
            "TestApp" + FileNameSuffixes.DotNet.ProgramDatabase,
            "TestApp" + FileNameSuffixes.CurrentPlatform.Exe,
            "TestApp" + FileNameSuffixes.Deps,
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
            "TestLibrary" + FileNameSuffixes.DotNet.ProgramDatabase,
        };

        public BuildOutputTests()
        {
            _testProjectsRoot = Path.Combine(AppContext.BaseDirectory, "TestAssets", "TestProjects");
        }

        private void PrepareProject(out TempDirectory root, out TempDirectory testAppDir, out TempDirectory testLibDir, out string runtime)
        {
            root = Temp.CreateDirectory();
            var src = root.CreateDirectory("src");

            testAppDir = src.CreateDirectory("TestApp");
            testLibDir = src.CreateDirectory("TestLibrary");

            // copy projects to the temp dir
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestApp"), testAppDir);
            CopyProjectToTempDir(Path.Combine(_testProjectsRoot, "TestLibrary"), testLibDir);

            var contexts = ProjectContext.CreateContextForEachFramework(
                testLibDir.Path,
                null,
                PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());
            runtime = contexts.FirstOrDefault(c => !string.IsNullOrEmpty(c.RuntimeIdentifier))?.RuntimeIdentifier;
        }

        private string FormatPath(string input, string framework, string runtime)
        {
            return input.Replace("{fw}", framework).Replace("{rid}", runtime);
        }

        [Theory]
        // global.json exists
        [InlineData(true, null, null, "src/TestLibrary/bin/Debug/{fw}", "src/TestApp/bin/Debug/{fw}", "src/TestApp/bin/Debug/{fw}/{rid}")]
        [InlineData(true, "out", null, "src/TestLibrary/bin/Debug/{fw}", "src/TestApp/bin/Debug/{fw}", "out")]
        [InlineData(true, null, "build", "build/src/TestLibrary/bin/Debug/{fw}", "build/src/TestApp/bin/Debug/{fw}", "build/src/TestApp/bin/Debug/{fw}/{rid}")]
        [InlineData(true, "out", "build", "build/src/TestLibrary/bin/Debug/{fw}", "build/src/TestApp/bin/Debug/{fw}", "out")]
        //no global.json
        //[InlineData(false, null, null, "src/TestLibrary/bin/debug/{fw}", "src/TestApp/bin/debug/{fw}", "src/TestApp/bin/debug/{fw}/{rid}")]
        //[InlineData(false, "out", null, "src/TestLibrary/bin/debug/{fw}", "src/TestApp/bin/debug/{fw}", "out")]
        //[InlineData(false, null, "build", "build/TestLibrary/bin/debug/{fw}", "build/TestApp/bin/debug/{fw}", "build/TestApp/bin/debug/{fw}/{rid}")]
        //[InlineData(false, "out", "build", "build/TestLibrary/bin/debug/{fw}", "build/TestApp/bin/debug/{fw}", "out")]
        public void DefaultPaths(bool global, string outputValue, string baseValue, string expectedLibCompile, string expectedAppCompile, string expectedAppRuntime)
        {
            TempDirectory root;
            TempDirectory testAppDir;
            TempDirectory testLibDir;
            string runtime;

            PrepareProject(out root, out testAppDir, out testLibDir, out runtime);
            if (global)
            {
                root.CopyFile(Path.Combine(_testProjectsRoot, "global.json"));
            }

            new BuildCommand(GetProjectPath(testAppDir),
                output: outputValue != null ?  Path.Combine(root.Path, outputValue) : string.Empty,
                buidBasePath: baseValue != null ? Path.Combine(root.Path, baseValue) : string.Empty,
                framework: DefaultFramework)
                .ExecuteWithCapturedOutput().Should().Pass();

            var libdebug = root.DirectoryInfo.Sub(FormatPath(expectedLibCompile, DefaultFramework, runtime));
            var appdebug = root.DirectoryInfo.Sub(FormatPath(expectedAppCompile, DefaultFramework, runtime));
            var appruntime = root.DirectoryInfo.Sub(FormatPath(expectedAppRuntime, DefaultFramework, runtime));

            libdebug.Should().Exist().And.HaveFiles(_libCompileFiles);
            appdebug.Should().Exist().And.HaveFiles(_appCompileFiles);
            appruntime.Should().Exist().And.HaveFiles(_runtimeFiles);
        }

        [Fact]
        public void ResourceTest()
        {
            TempDirectory root;
            TempDirectory testAppDir;
            TempDirectory testLibDir;
            string runtime;

            PrepareProject(out root, out testAppDir, out testLibDir, out runtime);
            var names = new[]
            {
                "uk-UA",
                "en",
                "en-US"
            };
            foreach (var folder in new [] { testAppDir, testLibDir })
            {
                foreach (var name in names)
                {
                    folder.CreateFile($"Resource.{name}.resx").WriteAllText("<root></root>");
                }
            }

            new BuildCommand(GetProjectPath(testAppDir), framework: DefaultFramework)
                .ExecuteWithCapturedOutput().Should().Pass();

            var libdebug = testLibDir.DirectoryInfo.Sub("bin/Debug").Sub(DefaultFramework);
            var appdebug = testAppDir.DirectoryInfo.Sub("bin/Debug").Sub(DefaultFramework);
            var appruntime = appdebug.Sub(runtime);

            foreach (var name in names)
            {
                libdebug.Sub(name).Should().Exist().And.HaveFile("TestLibrary.resources.dll");
                appdebug.Sub(name).Should().Exist().And.HaveFile("TestApp.resources.dll");
                appruntime.Sub(name).Should().Exist().And.HaveFiles(new [] { "TestLibrary.resources.dll", "TestApp.resources.dll" });
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

        private string GetProjectPath(TempDirectory projectDir)
        {
            return Path.Combine(projectDir.Path, "project.json");
        }
    }
}
