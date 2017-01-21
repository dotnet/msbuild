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
        private const string ExpectedSlnFileAfterRemovingAllSolutionItems = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""TestApp"", ""TestApp\TestApp.csproj"", ""{D65E5A1F-719F-4F95-8835-88BDD67AD457}""
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|x64.ActiveCfg = Debug|x64
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|x64.Build.0 = Debug|x64
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|x86.ActiveCfg = Debug|x86
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|x86.Build.0 = Debug|x86
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|Any CPU.Build.0 = Release|Any CPU
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|x64.ActiveCfg = Release|x64
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|x64.Build.0 = Release|x64
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|x86.ActiveCfg = Release|x86
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

        private const string ExpectedSlnFileAfterRemovingAllSolutionItemsExceptReadme = @"
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio 15
VisualStudioVersion = 15.0.26006.2
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}"") = ""TestApp"", ""TestApp\TestApp.csproj"", ""{D65E5A1F-719F-4F95-8835-88BDD67AD457}""
EndProject
Project(""{2150E333-8FDC-42A3-9474-1A3956D46DE8}"") = ""Solution Items"", ""Solution Items"", ""{FAACC4BE-31AE-4EB7-A4C8-5BB4617EB4AF}""
	ProjectSection(SolutionItems) = preProject
		readme.txt = readme.txt
	EndProjectSection
EndProject
Global
	GlobalSection(SolutionConfigurationPlatforms) = preSolution
		Debug|Any CPU = Debug|Any CPU
		Debug|x64 = Debug|x64
		Debug|x86 = Debug|x86
		Release|Any CPU = Release|Any CPU
		Release|x64 = Release|x64
		Release|x86 = Release|x86
	EndGlobalSection
	GlobalSection(ProjectConfigurationPlatforms) = postSolution
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|x64.ActiveCfg = Debug|x64
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|x64.Build.0 = Debug|x64
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|x86.ActiveCfg = Debug|x86
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Debug|x86.Build.0 = Debug|x86
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|Any CPU.Build.0 = Release|Any CPU
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|x64.ActiveCfg = Release|x64
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|x64.Build.0 = Release|x64
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|x86.ActiveCfg = Release|x86
		{D65E5A1F-719F-4F95-8835-88BDD67AD457}.Release|x86.Build.0 = Release|x86
	EndGlobalSection
	GlobalSection(SolutionProperties) = preSolution
		HideSolutionNode = FALSE
	EndGlobalSection
EndGlobal
";

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
                .Get("NonRestoredTestProjects", projectName)
                .CreateInstance()
                .WithSourceFiles()
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
                .Get("NonRestoredTestProjects", "PJAppWithSlnAndXprojRefs")
                .CreateInstance()
                .WithSourceFiles()
                .Root;

            var solutionRelPath = Path.Combine("TestApp", "TestApp.sln");

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"migrate \"{solutionRelPath}\"")
                .Should().Pass();

            new DirectoryInfo(projectDirectory.FullName)
                .Should().HaveFiles(new []
                    {
                        Path.Combine("TestApp", "TestApp.csproj"),
                        Path.Combine("TestLibrary", "TestLibrary.csproj"),
                        Path.Combine("TestApp", "src", "subdir", "subdir.csproj"),
                        Path.Combine("TestApp", "TestAssets", "TestAsset", "project.json")
                    });
 
            new DirectoryInfo(projectDirectory.FullName)
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

        [Theory]
        [InlineData("NoSolutionItemsAfterMigration.sln", ExpectedSlnFileAfterRemovingAllSolutionItems)]
        [InlineData("ReadmeSolutionItemAfterMigration.sln", ExpectedSlnFileAfterRemovingAllSolutionItemsExceptReadme)]
        public void WhenMigratingAnSlnLinksReferencingItemsMovedToBackupAreRemoved(
            string slnFileName,
            string expectedSlnContents)
        {
            var projectDirectory = TestAssets
                .Get("NonRestoredTestProjects", "PJAppWithSlnAndSolutionItemsToMoveToBackup")
                .CreateInstance(Path.GetFileNameWithoutExtension(slnFileName))
                .WithSourceFiles()
                .Root
                .FullName;

            new DotnetCommand()
                .WithWorkingDirectory(projectDirectory)
                .Execute($"migrate \"{slnFileName}\"")
                .Should().Pass();

            File.ReadAllText(Path.Combine(projectDirectory, slnFileName))
                .Should().BeVisuallyEquivalentTo(expectedSlnContents);
        }

        private void MigrateAndBuild(string groupName, string projectName, [CallerMemberName] string callingMethod = "", string identifier = "")
        {
            var projectDirectory = TestAssets
                .Get(groupName, projectName)
                .CreateInstance(callingMethod: callingMethod, identifier: identifier)
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

            //ISSUE: https://github.com/dotnet/cli/issues/5205
            //new DotnetCommand()
            //    .WithWorkingDirectory(projectDirectory)
            //    .Execute($"build \"{solutionRelPath}\"")
            //    .Should().Pass();

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
