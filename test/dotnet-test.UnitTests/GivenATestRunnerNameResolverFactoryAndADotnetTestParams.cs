// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using FluentAssertions;
using Microsoft.DotNet.ProjectModel;
using Microsoft.DotNet.Tools.Test;
using Moq;
using Xunit;

namespace Microsoft.Dotnet.Tools.Test.Tests
{
    public class GivenATestRunnerNameResolverFactoryAndADotnetTestParams
    {
        private const string PathToAFolder = "c:/some/path";
        private const string PathToAnAssembly = "c:/some/path/to/assembly.dll";
        private const string SomeTestRunner = "some test runner";

        private readonly string _pathToAProjectJson = Path.Combine(PathToAFolder, "project.json");

        [Fact]
        public void It_returns_a_ProjectJsonTestRunnerResolver_when_the_path_parameter_points_to_a_project_json()
        {
            var dotnetTestParams = new DotnetTestParams
            {
                ProjectOrAssemblyPath = _pathToAProjectJson
            };

            var projectReaderMock = new Mock<IProjectReader>();
            projectReaderMock
                .Setup(p => p.ReadProject(dotnetTestParams.ProjectOrAssemblyPath, null))
                .Returns(new Project());

            var dotnetTestRunnerResolverFactory = new DotnetTestRunnerResolverFactory(projectReaderMock.Object);

            var testRunnerResolver = dotnetTestRunnerResolverFactory.Create(dotnetTestParams);

            testRunnerResolver.Should().BeOfType<ProjectJsonTestRunnerNameResolver>();
        }

        [Fact]
        public void It_returns_a_ProjectJsonTestRunnerResolver_when_the_path_parameter_points_to_a_folder()
        {
            var dotnetTestParams = new DotnetTestParams
            {
                ProjectOrAssemblyPath = PathToAFolder
            };

            var projectReaderMock = new Mock<IProjectReader>();
            projectReaderMock
                .Setup(p => p.ReadProject(dotnetTestParams.ProjectOrAssemblyPath, null))
                .Returns(new Project());

            var dotnetTestRunnerResolverFactory = new DotnetTestRunnerResolverFactory(projectReaderMock.Object);

            var testRunnerResolver = dotnetTestRunnerResolverFactory.Create(dotnetTestParams);

            testRunnerResolver.Should().BeOfType<ProjectJsonTestRunnerNameResolver>();
        }

        [Fact]
        public void It_returns_a_ParameterTestRunnerResolver_when_an_assembly_and_a_test_runner_are_passed()
        {
            var dotnetTestParams = new DotnetTestParams
            {
                ProjectOrAssemblyPath = PathToAnAssembly,
                TestRunner = SomeTestRunner
            };

            var projectReaderMock = new Mock<IProjectReader>();

            var dotnetTestRunnerResolverFactory = new DotnetTestRunnerResolverFactory(projectReaderMock.Object);

            var testRunnerResolver = dotnetTestRunnerResolverFactory.Create(dotnetTestParams);

            testRunnerResolver.Should().BeOfType<ParameterTestRunnerNameResolver>();
        }
    }
}
