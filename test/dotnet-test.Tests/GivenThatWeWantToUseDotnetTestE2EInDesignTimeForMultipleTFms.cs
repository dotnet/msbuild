// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using FluentAssertions;
using Microsoft.DotNet.InternalAbstractions;
using Microsoft.DotNet.PlatformAbstractions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenThatWeWantToUseDotnetTestE2EInDesignTimeForMultipleTFms : TestBase
    {
        private string _projectFilePath;
        private string _netCoreAppOutputPath;
        private string _net46OutputPath;

        private void Setup([CallerMemberName] string callingMethod = "")
        {
            var testInstance = TestAssetsManager.CreateTestInstance(Path.Combine("ProjectsWithTests", "MultipleFrameworkProject"), callingMethod);

            _projectFilePath = Path.Combine(testInstance.TestRoot, "project.json");
            var contexts = ProjectContext.CreateContextForEachFramework(
                _projectFilePath,
                null,
                RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers());

            // Restore the project again in the destination to resolve projects
            // Since the lock file has project relative paths in it, those will be broken
            // unless we re-restore
            new RestoreCommand() { WorkingDirectory = testInstance.TestRoot }.Execute().Should().Pass();

            _netCoreAppOutputPath = Path.Combine(testInstance.TestRoot, "bin", "Debug", "netcoreapp1.0");
            var buildCommand = new BuildCommand(_projectFilePath);
            var result = buildCommand.Execute($"-f netcoreapp1.0 -o {_netCoreAppOutputPath}");

            result.Should().Pass();

            if (RuntimeEnvironment.OperatingSystemPlatform == Platform.Windows)
            {
                var rid = RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers().First();
                _net46OutputPath = Path.Combine(testInstance.TestRoot, "bin", "Debug", "net46", rid);
                result = buildCommand.Execute($"-f net46 -r {rid} -o {_net46OutputPath}");
                result.Should().Pass();
            }
        }

        [WindowsOnlyFact]
        public void It_discovers_tests_for_the_ProjectWithTestsWithNetCoreApp()
        {
            Setup();

            using (var adapter = new Adapter("TestDiscovery.Start"))
            {
                adapter.Listen();

                var testCommand = new DotnetTestCommand();
                var result = testCommand.Execute($"{_projectFilePath} -f netcoreapp1.0 -o {_netCoreAppOutputPath} --port {adapter.Port} --no-build");
                result.Should().Pass();

                adapter.Messages["TestSession.Connected"].Count.Should().Be(1);
                adapter.Messages["TestDiscovery.TestFound"].Count.Should().Be(4);
                adapter.Messages["TestDiscovery.Completed"].Count.Should().Be(1);
            }
        }

        [WindowsOnlyFact(Skip="https://github.com/xunit/xunit/issues/934")]
        public void It_discovers_tests_for_the_ProjectWithTestsWithNet46()
        {
            Setup();

            using (var adapter = new Adapter("TestDiscovery.Start"))
            {
                adapter.Listen();
                var rid = RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers().First();

                var testCommand = new DotnetTestCommand();
                var result = testCommand.Execute($"{_projectFilePath} -f net46 -r {rid} -o {_net46OutputPath} --port {adapter.Port} --no-build");
                result.Should().Pass();

                adapter.Messages["TestSession.Connected"].Count.Should().Be(1);
                adapter.Messages["TestDiscovery.TestFound"].Count.Should().Be(4);
                adapter.Messages["TestDiscovery.Completed"].Count.Should().Be(1);
            }
        }

        [Fact]
        public void It_runs_tests_for_netcoreapp10()
        {
            Setup();

            using (var adapter = new Adapter("TestExecution.GetTestRunnerProcessStartInfo"))
            {
                adapter.Listen();

                var testCommand = new DotnetTestCommand();
                var result = testCommand.Execute($"{_projectFilePath} -f netcoreapp1.0 -o {_netCoreAppOutputPath} --port {adapter.Port} --no-build");
                result.Should().Pass();

                adapter.Messages["TestSession.Connected"].Count.Should().Be(1);
                adapter.Messages["TestExecution.TestRunnerProcessStartInfo"].Count.Should().Be(1);
                adapter.Messages["TestExecution.TestStarted"].Count.Should().Be(4);
                adapter.Messages["TestExecution.TestResult"].Count.Should().Be(4);
                adapter.Messages["TestExecution.Completed"].Count.Should().Be(1);
            }
        }

        [WindowsOnlyFact(Skip="https://github.com/xunit/xunit/issues/934")]
        public void It_runs_tests_for_net46()
        {
            Setup();

            using (var adapter = new Adapter("TestExecution.GetTestRunnerProcessStartInfo"))
            {
                adapter.Listen();

                var testCommand = new DotnetTestCommand();
                var rid = RuntimeEnvironmentRidExtensions.GetAllCandidateRuntimeIdentifiers().First();
                var result = testCommand.Execute($"{_projectFilePath} -f net46 -r {rid} -o {_net46OutputPath} --port {adapter.Port} --no-build");
                result.Should().Pass();

                adapter.Messages["TestSession.Connected"].Count.Should().Be(1);
                adapter.Messages["TestExecution.TestRunnerProcessStartInfo"].Count.Should().Be(1);
                adapter.Messages["TestExecution.TestStarted"].Count.Should().Be(4);
                adapter.Messages["TestExecution.TestResult"].Count.Should().Be(4);
                adapter.Messages["TestExecution.Completed"].Count.Should().Be(1);
            }
        }
    }
}
