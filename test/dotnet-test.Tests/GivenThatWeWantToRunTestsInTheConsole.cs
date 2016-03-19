// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using Microsoft.DotNet.ProjectModel;
using Microsoft.Extensions.PlatformAbstractions;
using Xunit;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.Linq;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenThatWeWantToRunTestsInTheConsole : TestBase
    {
        private string _projectFilePath;
        private string _defaultOutputPath;

        public GivenThatWeWantToRunTestsInTheConsole()
        {
            var testInstance =
                TestAssetsManager.CreateTestInstance("ProjectWithTests", identifier: "ConsoleTests").WithLockFiles();

            _projectFilePath = Path.Combine(testInstance.TestRoot, "project.json");
            var contexts = ProjectContext.CreateContextForEachFramework(
                _projectFilePath,
                null,
                PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());

            var runtime = contexts.FirstOrDefault(c => !string.IsNullOrEmpty(c.RuntimeIdentifier))?.RuntimeIdentifier;
            _defaultOutputPath = Path.Combine(testInstance.TestRoot, "bin", "Debug", DefaultFramework, runtime);
        }

        //ISSUE https://github.com/dotnet/cli/issues/1935
        [WindowsOnlyFact]
        public void It_returns_a_failure_when_it_fails_to_run_the_tests()
        {
            var testCommand = new DotnetTestCommand();
            var result = testCommand.Execute(
                $"{_projectFilePath} -o {Path.Combine(AppContext.BaseDirectory, "nonExistingFolder")} --no-build");
            result.Should().Fail();
        }

        [Fact]
        public void It_builds_the_project_before_running()
        {
            var testCommand = new DotnetTestCommand();
            var result = testCommand.Execute($"{_projectFilePath}");
            result.Should().Pass();
        }

        [Fact]
        public void It_builds_the_project_using_the_output_passed()
        {
            var testCommand = new DotnetTestCommand();
            var result = testCommand.Execute($"{_projectFilePath} -o {AppContext.BaseDirectory} -f netstandardapp1.5");
            result.Should().Pass();
        }

        [Fact]
        public void It_builds_the_project_using_the_build_base_path_passed()
        {
            var buildBasePath = GetNotSoLongBuildBasePath();
            var testCommand = new DotnetTestCommand();
            var result = testCommand.Execute($"{_projectFilePath} -b {buildBasePath}");
            result.Should().Pass();
        }

        [Fact]
        public void It_skips_build_when_the_no_build_flag_is_passed()
        {
            var buildCommand = new BuildCommand(_projectFilePath);
            var result = buildCommand.Execute();
            result.Should().Pass();

            var testCommand = new DotnetTestCommand();
            result = testCommand.Execute($"{_projectFilePath} -o {_defaultOutputPath} --no-build");
            result.Should().Pass();
        }

        private string GetNotSoLongBuildBasePath()
        {
            return Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "buildBasePathTest"));
        }
    }
}
