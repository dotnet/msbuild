using FluentAssertions;
using Microsoft.DotNet.TestFramework;
using Microsoft.DotNet.Tools.Common;
using Microsoft.DotNet.Tools.Test.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace Microsoft.DotNet.Migration.Tests
{
    public class GivenThatAnAppWasMigrated : TestBase
    {
        [Theory]
        [InlineData("PJTestAppSimple")]
        [InlineData("TestAppWithLibrary")]
        public void When_migration_succeeds_Then_project_json_artifacts_get_moved_to_backup(string testProjectName)
        {
            var testRoot = TestAssetsManager
                .CreateTestInstance(testProjectName, identifier: testProjectName)
                .Path;

            var backupRoot = Path.Combine(testRoot, "backup");

            var migratableArtifacts = GetProjectJsonArtifacts(testRoot);
            
            new MigrateCommand()
                .WithWorkingDirectory(testRoot)
                .Execute()
                .Should().Pass();

            var backupArtifacts = GetProjectJsonArtifacts(backupRoot);

            backupArtifacts.Should().Equal(migratableArtifacts, "Because all of and only these artifacts should have been moved");

            new DirectoryInfo(testRoot).Should().NotHaveFiles(backupArtifacts.Keys);

            new DirectoryInfo(backupRoot).Should().HaveTextFiles(backupArtifacts);
        }

        [Theory]
        [InlineData("TestAppWithLibraryAndMissingP2P")]
        public void When_migration_fails_Then_project_json_artifacts_do_not_get_moved_to_backup(string testProjectName)
        {
            var testRoot = new TestAssetsManager(Path.Combine(RepoRoot, "TestAssets", "NonRestoredTestProjects"))
                .CreateTestInstance(testProjectName, identifier: testProjectName)
                .Path;

            var backupRoot = Path.Combine(testRoot, "backup");

            var migratableArtifacts = GetProjectJsonArtifacts(testRoot);

            new MigrateCommand()
                .WithWorkingDirectory(testRoot)
                .Execute()
                .Should().Fail();
            
            new DirectoryInfo(backupRoot).Should().NotExist("Because migration failed and therefore no backup is needed.");

            new DirectoryInfo(testRoot).Should().HaveTextFiles(migratableArtifacts, "Because migration failed so nothing was moved to backup.");
        }

        [Theory]
        [InlineData("PJTestAppSimple")]
        public void When_skipbackup_specified_Then_project_json_artifacts_do_not_get_moved_to_backup(string testProjectName)
        {
            var testRoot = TestAssetsManager.CreateTestInstance(testProjectName, identifier: testProjectName).Path;

            var backupRoot = Path.Combine(testRoot, "backup");

            var migratableArtifacts = GetProjectJsonArtifacts(testRoot);

            new MigrateCommand()
                .WithWorkingDirectory(testRoot)
                .Execute("--skip-backup")
                .Should().Pass();
            
            new DirectoryInfo(backupRoot).Should().NotExist("Because --skip-backup was specified.");

            new DirectoryInfo(testRoot).Should().HaveTextFiles(migratableArtifacts, "Because --skip-backup was specified.");
        }

        private Dictionary<string, string> GetProjectJsonArtifacts(string rootPath)
        {
            var catalog = new Dictionary<string, string>();
            
            var patterns = new [] { "global.json", "project.json", "*.xproj", "*.xproj.user" };

            foreach (var pattern in patterns)
            {
                AddArtifactsToCatalog(catalog, rootPath, pattern);
            }
            
            return catalog;
        }

        private void AddArtifactsToCatalog(Dictionary<string, string> catalog, string basePath, string pattern)
        {
            basePath = PathUtility.EnsureTrailingSlash(basePath);

            var baseDirectory = new DirectoryInfo(basePath);

            var files = baseDirectory.GetFiles(pattern, SearchOption.AllDirectories);

            foreach (var file in files)
            {
                catalog.Add(PathUtility.GetRelativePath(basePath, file.FullName), File.ReadAllText(file.FullName));
            }
        }
    }
}
