// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenThatWeWantToRunTestsForMultipleTFMsInTheConsole : TestBase
    {
        private readonly string _projectFilePath;
        private readonly string _defaultNetCoreAppOutputPath;
        private readonly string _defaultNet451OutputPath;

        public GivenThatWeWantToRunTestsForMultipleTFMsInTheConsole()
        {
            var testInstance =
                TestAssetsManager.CreateTestInstance(Path.Combine("ProjectsWithTests", "MultipleFrameworkProject"), identifier: "ConsoleTests");

            _projectFilePath = Path.Combine(testInstance.TestRoot, "project.json");
            var contexts = ProjectContext.CreateContextForEachFramework(
                _projectFilePath,
                null,
                RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());

            // Restore the project again in the destination to resolve projects
            // Since the lock file has project relative paths in it, those will be broken
            // unless we re-restore
            new RestoreCommand() { WorkingDirectory = testInstance.TestRoot }.Execute().Should().Pass();

            _defaultNetCoreAppOutputPath = Path.Combine(testInstance.TestRoot, "bin", "Debug", "netcoreapp1.0");
            _defaultNet451OutputPath = Path.Combine(testInstance.TestRoot, "bin", "Debug", "net451", RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers().First());
        }

        [WindowsOnlyFact]
        public void It_builds_and_runs_tests_for_all_frameworks()
        {
            var testCommand = new DotnetTestCommand();
            var result = testCommand
                .ExecuteWithCapturedOutput($"{_projectFilePath}");
            result.Should().Pass();
            result.StdOut.Should().Contain("Skipped for NET451");
            result.StdOut.Should().Contain("Skipped for NETCOREAPP1.0");
        }

        [WindowsOnlyFact]
        public void It_builds_and_runs_tests_for_net451()
        {
            var testCommand = new DotnetTestCommand();
            var result = testCommand
                .ExecuteWithCapturedOutput($"{_projectFilePath} -f net451");
            result.Should().Pass();
            result.StdOut.Should().Contain($"Skipped for NET451");
            result.StdOut.Should().NotContain($"Skipped for NETCOREAPP1.0");
        }

        [Fact]
        public void It_builds_and_runs_tests_for_netcoreapp10()
        {
            var testCommand = new DotnetTestCommand();
            var result = testCommand
                .ExecuteWithCapturedOutput($"{_projectFilePath} -f netcoreapp1.0");
            result.Should().Pass();
            result.StdOut.Should().Contain($"Skipped for NETCOREAPP1.0");
            result.StdOut.Should().NotContain($"Skipped for NET451");
        }

        [Fact]
        public void It_builds_the_project_using_the_output_passed()
        {
            var testCommand = new DotnetTestCommand();
            var result = testCommand.Execute(
                $"{_projectFilePath} -o {Path.Combine(AppContext.BaseDirectory, "output")} -f netcoreapp1.0");
            result.Should().Pass();
        }

        [WindowsOnlyFact]
        public void It_builds_the_project_using_the_build_base_path_passed()
        {
            var buildBasePath = GetNotSoLongBuildBasePath();
            var testCommand = new DotnetTestCommand();
            var result = testCommand.Execute($"{_projectFilePath} -b {buildBasePath}");
            result.Should().Pass();
        }

        [Fact]
        public void It_skips_build_when_the_no_build_flag_is_passed_for_netcoreapp10()
        {
            var buildCommand = new BuildCommand(_projectFilePath);
            var result = buildCommand.Execute($"-f netcoreapp1.0 -o {_defaultNetCoreAppOutputPath}");
            result.Should().Pass();

            var testCommand = new DotnetTestCommand();
            result = testCommand.Execute($"{_projectFilePath} -f netcoreapp10 -o {_defaultNetCoreAppOutputPath} --no-build");
            result.Should().Pass();
        }

        [WindowsOnlyFact]
        public void It_skips_build_when_the_no_build_flag_is_passed_for_net451()
        {
            var rid = RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers().First();
            var buildCommand = new BuildCommand(_projectFilePath);
            var result = buildCommand.Execute($"-f net451 -r {rid} -o {_defaultNet451OutputPath}");
            result.Should().Pass();

            var testCommand = new DotnetTestCommand();
            result = testCommand.Execute($"{_projectFilePath} -f net451 -r {rid} -o {_defaultNet451OutputPath} --no-build");
            result.Should().Pass();
        }

        [Fact]
        public void It_prints_error_when_no_framework_matched()
        {
            var nonExistentFramework = "doesnotexisttfm99.99";
            var testCommand = new DotnetTestCommand();
            var result = testCommand
                .ExecuteWithCapturedOutput($"{_projectFilePath} -f {nonExistentFramework}");

            result.Should().Fail();
            result.StdErr.Should().Contain($"does not support framework");
        }

        [WindowsOnlyFact]
        public void It_runs_tests_for_all_tfms_if_they_fail()
        {
            var testCommand = new DotnetTestCommand
            {
                Environment =
                {
                    { "DOTNET_TEST_SHOULD_FAIL", "1" }
                }
            };

            var result = testCommand
                .ExecuteWithCapturedOutput($"{_projectFilePath}");

            result.Should().Fail();
            result.StdOut.Should().Contain("Failing in NET451");
            result.StdOut.Should().Contain("Failing in NETCOREAPP1.0");
        }

        private string GetNotSoLongBuildBasePath()
        {
            return Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "buildBasePathTest"));
        }
    }
}
