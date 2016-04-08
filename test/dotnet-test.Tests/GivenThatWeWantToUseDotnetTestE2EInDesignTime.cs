// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.IO;
using FluentAssertions;
using Xunit;
using Microsoft.Extensions.PlatformAbstractions;
using System.Linq;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenThatWeWantToUseDotnetTestE2EInDesignTime : TestBase
    {
        private string _projectFilePath;
        private string _outputPath;

        public GivenThatWeWantToUseDotnetTestE2EInDesignTime()
        {
            var testAssetManager = new TestAssetsManager(Path.Combine(RepoRoot, "TestAssets"));
            var testInstance = testAssetManager.CreateTestInstance("ProjectWithTests").WithLockFiles();

            _projectFilePath = Path.Combine(testInstance.TestRoot, "project.json");
            var contexts = ProjectContext.CreateContextForEachFramework(
                _projectFilePath,
                null,
                PlatformServices.Default.Runtime.GetAllCandidateRuntimeIdentifiers());

            _outputPath = Path.Combine(testInstance.TestRoot, "bin", "Debug", "netcoreapp1.0");
            var buildCommand = new BuildCommand(_projectFilePath);
            var result = buildCommand.Execute();

            result.Should().Pass();
        }

        [WindowsOnlyFact]
        public void It_discovers_two_tests_for_the_ProjectWithTests()
        {
            using (var adapter = new Adapter("TestDiscovery.Start"))
            {
                adapter.Listen();

                var testCommand = new DotnetTestCommand();
                var result = testCommand.Execute($"{_projectFilePath} -o {_outputPath} --port {adapter.Port} --no-build");
                result.Should().Pass();

                adapter.Messages["TestSession.Connected"].Count.Should().Be(1);
                adapter.Messages["TestDiscovery.TestFound"].Count.Should().Be(2);
                adapter.Messages["TestDiscovery.Completed"].Count.Should().Be(1);
            }
        }

        [Fact]
        public void It_runs_two_tests_for_the_ProjectWithTests()
        {
            using (var adapter = new Adapter("TestExecution.GetTestRunnerProcessStartInfo"))
            {
                adapter.Listen();

                var testCommand = new DotnetTestCommand();
                var result = testCommand.Execute($"{_projectFilePath} -o {_outputPath} --port {adapter.Port} --no-build");
                result.Should().Pass();

                adapter.Messages["TestSession.Connected"].Count.Should().Be(1);
                adapter.Messages["TestExecution.TestRunnerProcessStartInfo"].Count.Should().Be(1);
                adapter.Messages["TestExecution.TestStarted"].Count.Should().Be(2);
                adapter.Messages["TestExecution.TestResult"].Count.Should().Be(2);
                adapter.Messages["TestExecution.Completed"].Count.Should().Be(1);
            }
        }
    }
}
