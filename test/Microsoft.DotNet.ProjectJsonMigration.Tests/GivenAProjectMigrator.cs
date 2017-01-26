using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.Build.Construction;
using Microsoft.DotNet.ProjectJsonMigration.Rules;
using Microsoft.DotNet.Internal.ProjectModel;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test.Utilities;
using NuGet.Frameworks;
using Xunit;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public class GivenAProjectMigrator : TestBase
    {
        [Fact]
        public void ItCopiesProjectDirectoryContentsToOutputDirectoryWhenTheDirectoriesAreDifferent()
        {
            var testProjectDirectory = TestAssetsManager
                .CreateTestInstance("PJTestAppSimple", callingMethod: "z")
                .Path;

            var outputDirectory = Temp.CreateDirectory().Path;

            var projectDirectoryRelativeFilePaths = EnumerateFilesWithRelativePath(testProjectDirectory);

            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(testProjectDirectory, outputDirectory, mockProj);

            var projectMigrator = new ProjectMigrator(new FakeEmptyMigrationRule());
            projectMigrator.Migrate(testSettings);

            foreach (var projectDirectoryRelativeFilePath in projectDirectoryRelativeFilePaths)
            {
                File.Exists(Path.Combine(outputDirectory, projectDirectoryRelativeFilePath)).Should().BeTrue();
            }
        }

        [Fact]
        public void ItHasErrorWhenMigratingADeprecatedNamedResourceOptionProjectJson()
        {
            var testProjectDirectory = TestAssets
               .GetProjectJson(TestAssetKinds.NonRestoredTestProjects, "PJAppWithDeprecatedNamedResourceOption")
               .CreateInstance()
               .WithSourceFiles()
               .Root
               .FullName;

            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(
                testProjectDirectory,
                testProjectDirectory,
                mockProj);

            var projectMigrator = new ProjectMigrator(new FakeEmptyMigrationRule());
            var report = projectMigrator.Migrate(testSettings);

            var projectReport = report.ProjectMigrationReports.First();
            var errorMessage = projectReport.Errors.First().GetFormattedErrorMessage();
            errorMessage.Should().Contain("MIGRATE1011::Deprecated Project:");
            errorMessage.Should().Contain("The 'namedResource' option is deprecated. Use 'embed' in 'buildOptions' instead. (line: 3, file:");
        }

        [Fact]
        public void ItHasErrorWhenMigratingANonCsharpApp()
        {
            var testProjectDirectory =
                TestAssetsManager.CreateTestInstance("FSharpTestProjects/TestApp", callingMethod: "z")
                    .Path;

            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(testProjectDirectory, testProjectDirectory, mockProj);

            var projectMigrator = new ProjectMigrator(new FakeEmptyMigrationRule());
            var report = projectMigrator.Migrate(testSettings);
            var projectReport = report.ProjectMigrationReports.First();

            var errorMessage = projectReport.Errors.First().GetFormattedErrorMessage();
            errorMessage.Should().Contain("MIGRATE20013::Non-Csharp App: Cannot migrate project");
        }

        [Fact]
        public void ItHasErrorWhenMigratingAProjectJsonWithoutAFrameworks()
        {
            var testInstance = TestAssets.Get(
                    "NonRestoredTestProjects", 
                    "TestLibraryWithProjectFileWithoutFrameworks")
                .CreateInstance()
                .WithSourceFiles();

            var testProjectDirectory = testInstance.Root.FullName;

            var mockProj = ProjectRootElement.Create();
            var testSettings = MigrationSettings.CreateMigrationSettingsTestHook(
                testProjectDirectory,
                testProjectDirectory,
                mockProj);

            var projectMigrator = new ProjectMigrator(new FakeEmptyMigrationRule());
            var report = projectMigrator.Migrate(testSettings);

            var projectReport = report.ProjectMigrationReports.First();

            projectReport.Errors.First().GetFormattedErrorMessage()
                .Should().Contain("MIGRATE1013::No Project:")
                .And.Contain($"The project.json specifies no target frameworks in {testProjectDirectory}");
        }

        private IEnumerable<string> EnumerateFilesWithRelativePath(string testProjectDirectory)
        {
            return
                Directory.EnumerateFiles(testProjectDirectory, "*", SearchOption.AllDirectories)
                    .Select(file => PathUtility.GetRelativePath(testProjectDirectory, file));
        }

        private class FakeEmptyMigrationRule : IMigrationRule
        {
            public void Apply(MigrationSettings migrationSettings, MigrationRuleInputs migrationRuleInputs)
            {
                return;
            }
        }
    }
}