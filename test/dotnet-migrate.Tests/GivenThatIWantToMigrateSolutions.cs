// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateSolutions : TestBase
    {
        [Fact]
        public void ItMigratesAndBuildsSln()
        {
            MigrateAndBuild(
                "NonRestoredTestProjects",
                "PJAppWithSlnAndXprojRefs",
                ProjectTypeGuids.CSharpProjectTypeGuid);
        }

        [Fact]
        public void WhenDirectoryAlreadyContainsCsprojFileItMigratesAndBuildsSln()
        {
            MigrateAndBuild(
                "NonRestoredTestProjects",
                "PJAppWithSlnAndXprojRefsAndUnrelatedCsproj",
                ProjectTypeGuids.CSharpProjectTypeGuid);
        }

        [Fact]
        public void WhenXprojReferencesCsprojAndSlnDoesNotItMigratesAndBuildsSln()
        {
            MigrateAndBuild(
                "NonRestoredTestProjects",
                "PJAppWithSlnAndXprojRefThatRefsCsprojWhereSlnDoesNotRefCsproj",
                ProjectTypeGuids.CPSProjectTypeGuid);
        }

        private void MigrateAndBuild(string groupName, string projectName, string subdirProjectTypeGuid)
        {
            var projectDirectory = TestAssets
                .Get(groupName, projectName)
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var solutionRelPath = Path.Combine("TestApp", "TestApp.sln");

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"migrate \"{solutionRelPath}\"")
                .Should().Pass();

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"restore \"{solutionRelPath}\"")
                .Should().Pass();

            //ISSUE: https://github.com/dotnet/sdk/issues/545
            //new DotnetCommand()
            //    .WithWorkingDirectory(projectDirectory)
            //    .Execute($"build \"{solutionRelPath}\"")
            //    .Should().Pass();

            SlnFile slnFile = SlnFile.Read(Path.Combine(projectDirectory.FullName, solutionRelPath));
            slnFile.Projects.Count.Should().Be(3);

            var slnProject = slnFile.Projects.Where((p) => p.Name == "TestApp").Single();
            slnProject.TypeGuid.Should().Be(ProjectTypeGuids.CSharpProjectTypeGuid);
            slnProject.FilePath.Should().Be("TestApp.csproj");

            slnProject = slnFile.Projects.Where((p) => p.Name == "TestLibrary").Single();
            slnProject.TypeGuid.Should().Be(ProjectTypeGuids.CSharpProjectTypeGuid);
            slnProject.FilePath.Should().Be(Path.Combine("..", "TestLibrary", "TestLibrary.csproj"));

            slnProject = slnFile.Projects.Where((p) => p.Name == "subdir").Single();
            slnProject.TypeGuid.Should().Be(subdirProjectTypeGuid);
            slnProject.FilePath.Should().Be(Path.Combine("src", "subdir", "subdir.csproj"));
        }
    }
}
