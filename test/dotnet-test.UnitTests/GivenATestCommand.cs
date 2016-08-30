// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using FluentAssertions;
using Microsoft.DotNet.Tools.Test;
using Moq;
using Xunit;
using Microsoft.DotNet.ProjectModel;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenATestCommand
    {
        private static readonly string ProjectJsonPath = Path.Combine(
                AppContext.BaseDirectory,
                "TestAssets",
                "TestProjects",
                "ProjectsWithTests",
                "NetCoreAppOnlyProject",
                "project.json");

        private readonly TestCommand _testCommand;
        private readonly Mock<IDotnetTestRunnerFactory> _dotnetTestRunnerFactoryMock;
        private readonly Mock<IDotnetTestRunner> _dotnetTestRunnerMock;

        public GivenATestCommand()
        {
            _dotnetTestRunnerMock = new Mock<IDotnetTestRunner>();
            _dotnetTestRunnerMock
                .Setup(d => d.RunTests(It.IsAny<DotnetTestParams>()))
                .Returns(0);

            _dotnetTestRunnerFactoryMock = new Mock<IDotnetTestRunnerFactory>();
            _dotnetTestRunnerFactoryMock
                .Setup(d => d.Create(It.IsAny<DotnetTestParams>()))
                .Returns(_dotnetTestRunnerMock.Object);

            _testCommand = new TestCommand(_dotnetTestRunnerFactoryMock.Object);
        }

        [Fact]
        public void It_does_not_create_a_runner_if_the_args_include_help()
        {
            var result = _testCommand.DoRun(new[] {"--help"});

            result.Should().Be(0);
            _dotnetTestRunnerFactoryMock
                .Verify(d => d.Create(It.IsAny<DotnetTestParams>()), Times.Never);
        }

        [Fact]
        public void It_creates_a_runner_if_the_args_do_no_include_help()
        {
            var result = _testCommand.DoRun(new[] { ProjectJsonPath, "-f", "netcoreapp1.0" });

            result.Should().Be(0);
            _dotnetTestRunnerFactoryMock
                .Verify(d => d.Create(It.IsAny<DotnetTestParams>()), Times.Once);
        }

        [Fact]
        public void It_runs_the_tests_through_the_DotnetTestRunner()
        {
            var result = _testCommand.DoRun(new[] { ProjectJsonPath, "-f", "netcoreapp1.0" });

            _dotnetTestRunnerMock.Verify(
                d => d.RunTests(It.IsAny<DotnetTestParams>()),
                Times.Once);
        }
    }
}
