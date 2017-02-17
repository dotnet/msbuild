// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Cli.Sln.Internal;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Test.Utilities;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatIWantToMigrateSolutions : TestBase
    {
        [Theory]
        [InlineData("PJAppWithSlnVersion14", "Visual Studio 15", "15.0.26114.2", "10.0.40219.1")]
        [InlineData("PJAppWithSlnVersion15", "Visual Studio 15 Custom", "15.9.12345.4", "10.9.1234.5")]
        [InlineData("PJAppWithSlnVersionUnknown", "Visual Studio 15", "15.0.26114.2", "10.0.40219.1")]
        public void ItMigratesSlnAndEnsuresAtLeastVS15(
            string projectName,
            string productDescription,
            string visualStudioVersion,
            string minVisualStudioVersion)
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, projectName)
                .CreateInstance(identifier: projectName)
                .WithSourceFiles()
                .WithEmptyGlobalJson()
                .Root;

            var solutionRelPath = "TestApp.sln";

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"migrate \"{solutionRelPath}\"")
                .Should().Pass();

            SlnFile slnFile = SlnFile.Read(Path.Combine(projectDirectory.FullName, solutionRelPath));
            slnFile.ProductDescription.Should().Be(productDescription);
            slnFile.VisualStudioVersion.Should().Be(visualStudioVersion);
            slnFile.MinimumVisualStudioVersion.Should().Be(minVisualStudioVersion);
        }

        [Fact]
        public void ItMigratesAndBuildsSln()
        {
            MigrateAndBuild(
                "NonRestoredTestProjects",
                "PJAppWithSlnAndXprojRefs");
        }

        [Fact]
        public void ItOnlyMigratesProjectsInTheSlnFile()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithSlnAndXprojRefs")
                .CreateInstance()
                .WithSourceFiles()
                .WithEmptyGlobalJson()
                .Root;

            var solutionRelPath = Path.Combine("TestApp", "TestApp.sln");

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"migrate \"{solutionRelPath}\"")
                .Should().Pass();

            projectDirectory
                .Should().HaveFiles(new []
                    {
                        Path.Combine("TestApp", "TestApp.csproj"),
                        Path.Combine("TestLibrary", "TestLibrary.csproj"),
                        Path.Combine("TestApp", "src", "subdir", "subdir.csproj"),
                        Path.Combine("TestApp", "TestAssets", "TestAsset", "project.json")
                    });
 
            projectDirectory
                .Should().NotHaveFile(Path.Combine("TestApp", "TestAssets", "TestAsset", "TestAsset.csproj"));
        }

        [Fact]
        public void WhenDirectoryAlreadyContainsCsprojFileItMigratesAndBuildsSln()
        {
            MigrateAndBuild(
                "NonRestoredTestProjects",
                "PJAppWithSlnAndXprojRefsAndUnrelatedCsproj");
        }

        [Fact]
        public void WhenXprojReferencesCsprojAndSlnDoesNotItMigratesAndBuildsSln()
        {
            MigrateAndBuild(
                "NonRestoredTestProjects",
                "PJAppWithSlnAndXprojRefThatRefsCsprojWhereSlnDoesNotRefCsproj");
        }

        [Fact]
        public void WhenSolutionContainsACsprojFileItGetsMovedToBackup()
        {
            var projectDirectory = TestAssets
                .GetProjectJson("NonRestoredTestProjects", "PJAppWithSlnAndOneAlreadyMigratedCsproj")
                .CreateInstance()
                .WithSourceFiles()
                .WithEmptyGlobalJson()
                .Root;

            var solutionRelPath = Path.Combine("TestApp", "TestApp.sln");

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"migrate \"{solutionRelPath}\"");

            cmd.Should().Pass();

            projectDirectory
                .GetDirectory("TestLibrary")
                .GetFile("TestLibrary.csproj")
                .Should().Exist();

            projectDirectory
                .GetDirectory("TestLibrary")
                .GetFile("TestLibrary.csproj.migration_in_place_backup")
                .Should().NotExist();

            projectDirectory
                .GetDirectory("backup", "TestLibrary")
                .GetFile("TestLibrary.csproj")
                .Should().Exist();
        }

        [Fact]
        public void WhenSolutionContainsACsprojFileItDoesNotTryToAddItAgain()
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithSlnAndOneAlreadyMigratedCsproj")
                .CreateInstance()
                .WithSourceFiles()
                .WithEmptyGlobalJson()
                .Root;

            var solutionRelPath = Path.Combine("TestApp", "TestApp.sln");

            var cmd = new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .ExecuteWithCapturedOutput($"migrate \"{solutionRelPath}\"");

            cmd.Should().Pass();
            cmd.StdOut.Should().NotContain("already contains project");
            cmd.StdErr.Should().BeEmpty();
        }

        [Theory]
        [InlineData("NoSolutionItemsAfterMigration.sln", false)]
        [InlineData("ReadmeSolutionItemAfterMigration.sln", true)]
        public void WhenMigratingAnSlnLinksReferencingItemsMovedToBackupAreRemoved(
            string slnFileName,
            bool solutionItemsContainsReadme)
        {
            var projectDirectory = TestAssets
                .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithSlnAndSolutionItemsToMoveToBackup")
                .CreateInstance(Path.GetFileNameWithoutExtension(slnFileName))
                .WithSourceFiles()
                .Root
                .FullName;

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"migrate \"{slnFileName}\"")
                .Should().Pass();

            var slnFile = SlnFile.Read(Path.Combine(projectDirectory, slnFileName));
            var solutionFolders = slnFile.Projects.Where(p => p.TypeGuid == ProjectTypeGuids.SolutionFolderGuid);
            if (solutionItemsContainsReadme)
            {
                solutionFolders.Count().Should().Be(1);
                var solutionItems = solutionFolders.Single().Sections.GetSection("SolutionItems");
                solutionItems.Should().NotBeNull();
                solutionItems.Properties.Count().Should().Be(1);
                solutionItems.Properties["readme.txt"].Should().Be("readme.txt");
            }
            else
            {
                solutionFolders.Count().Should().Be(0);
            }
        }

        [Fact]
        public void ItMigratesSolutionInTheFolderWhenWeRunMigrationInThatFolder()
        {
            var projectDirectory = TestAssets
                .Get("NonRestoredTestProjects", "PJAppWithSlnAndXprojRefs")
                .CreateInstance()
                .WithSourceFiles()
                .WithEmptyGlobalJson()
                .Root;

            var workingDirectory = new DirectoryInfo(Path.Combine(projectDirectory.FullName, "TestApp"));
            var solutionRelPath = Path.Combine("TestApp", "TestApp.sln");

            new DotnetCommand()
                .WithWorkingDirectory(workingDirectory)
                .Execute($"migrate")
                .Should().Pass();

            SlnFile slnFile = SlnFile.Read(Path.Combine(projectDirectory.FullName, solutionRelPath));

            var nonSolutionFolderProjects = slnFile.Projects
                .Where(p => p.TypeGuid != ProjectTypeGuids.SolutionFolderGuid);

            nonSolutionFolderProjects.Count().Should().Be(4);

            var slnProject = nonSolutionFolderProjects.Where((p) => p.Name == "TestApp").Single();
            slnProject.TypeGuid.Should().Be(ProjectTypeGuids.CSharpProjectTypeGuid);
            slnProject.FilePath.Should().Be("TestApp.csproj");

            slnProject = nonSolutionFolderProjects.Where((p) => p.Name == "TestLibrary").Single();
            slnProject.TypeGuid.Should().Be(ProjectTypeGuids.CSharpProjectTypeGuid);
            slnProject.FilePath.Should().Be(Path.Combine("..", "TestLibrary", "TestLibrary.csproj"));

            slnProject = nonSolutionFolderProjects.Where((p) => p.Name == "subdir").Single();
            slnProject.FilePath.Should().Be(Path.Combine("src", "subdir", "subdir.csproj"));

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"restore \"{solutionRelPath}\"")
                .Should().Pass();

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"build \"{solutionRelPath}\"")
                .Should().Pass();
        }

        private void MigrateAndBuild(string groupName, string projectName, [CallerMemberName] string callingMethod = "", string identifier = "")
        {
            var projectDirectory = TestAssets
                .Get(groupName, projectName)
                .CreateInstance(callingMethod: callingMethod, identifier: identifier)
                .WithSourceFiles()
                .WithEmptyGlobalJson()
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

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"build \"{solutionRelPath}\"")
                .Should().Pass();

            SlnFile slnFile = SlnFile.Read(Path.Combine(projectDirectory.FullName, solutionRelPath));
            
            var nonSolutionFolderProjects = slnFile.Projects
                .Where(p => p.TypeGuid != ProjectTypeGuids.SolutionFolderGuid);

            nonSolutionFolderProjects.Count().Should().Be(3);

            var slnProject = nonSolutionFolderProjects.Where((p) => p.Name == "TestApp").Single();
            slnProject.TypeGuid.Should().Be(ProjectTypeGuids.CSharpProjectTypeGuid);
            slnProject.FilePath.Should().Be("TestApp.csproj");

            slnProject = nonSolutionFolderProjects.Where((p) => p.Name == "TestLibrary").Single();
            slnProject.TypeGuid.Should().Be(ProjectTypeGuids.CSharpProjectTypeGuid);
            slnProject.FilePath.Should().Be(Path.Combine("..", "TestLibrary", "TestLibrary.csproj"));

            slnProject = nonSolutionFolderProjects.Where((p) => p.Name == "subdir").Single();
            //ISSUE: https://github.com/dotnet/sdk/issues/522
            //Once we have that change migrate will always burn in the C# guid
            //slnProject.TypeGuid.Should().Be(ProjectTypeGuids.CSharpProjectTypeGuid);
            slnProject.FilePath.Should().Be(Path.Combine("src", "subdir", "subdir.csproj"));
        }
    }
}
