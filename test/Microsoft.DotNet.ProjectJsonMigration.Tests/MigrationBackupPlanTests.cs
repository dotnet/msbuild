// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Internal.ProjectModel.Utilities;
using Microsoft.DotNet.ProjectJsonMigration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public partial class MigrationBackupPlanTests
    {
        [Fact]
        public void TheBackupDirectoryIsASubfolderOfTheMigratedProject()
        {
            var workspaceDirectory = Path.Combine("src", "root");
            var projectDirectory = Path.Combine("src", "project1");

            WhenMigrating(projectDirectory, workspaceDirectory)
                .RootBackupDirectory
                .FullName
                .Should()
                .Be(new DirectoryInfo(Path.Combine("src", "project1", "backup")).FullName.EnsureTrailingSlash());
        }

        [Fact]
        public void TheBackupDirectoryIsASubfolderOfTheMigratedProjectWhenInitiatedFromProjectFolder()
        {
            var workspaceDirectory = Path.Combine("src", "root");
            var projectDirectory = Path.Combine("src", "root");

            WhenMigrating(projectDirectory, workspaceDirectory)
                .ProjectBackupDirectories.Single()
                .FullName
                .Should()
                .Be(new DirectoryInfo(Path.Combine("src", "root", "backup")).FullName.EnsureTrailingSlash());
        }

        [Fact]
        public void TheBackupDirectoryIsInTheCommonRootOfTwoProjectFoldersWhenInitiatedFromProjectFolder()
        {
            var projectDirectories = new []
            {
                Path.Combine("root", "project1"),
                Path.Combine("root", "project2")
            };

            var workspaceDirectory = Path.Combine("root", "project1");

            WhenMigrating(projectDirectories, workspaceDirectory)
                .RootBackupDirectory
                .FullName
                .Should()
                .Be(new DirectoryInfo(Path.Combine("root", "backup")).FullName.EnsureTrailingSlash());
        }

        [Fact]
        public void TheBackupDirectoryIsInTheCommonRootOfTwoProjectFoldersWhenInitiatedFromCommonRoot()
        {
            var projectDirectories = new []
            {
                Path.Combine("root", "project1"),
                Path.Combine("root", "project2")
            };

            var workspaceDirectory = Path.Combine("root");

            WhenMigrating(projectDirectories, workspaceDirectory)
                .RootBackupDirectory
                .FullName
                .Should()
                .Be(new DirectoryInfo(Path.Combine("root", "backup")).FullName.EnsureTrailingSlash());
        }

        [Fact]
        public void TheBackupDirectoryIsInTheCommonRootOfTwoProjectFoldersAtDifferentLevelsWhenInitiatedFromProjectFolder()
        {
            var projectDirectories = new []
            {
                Path.Combine("root", "tests", "inner", "project1"),
                Path.Combine("root", "src", "project2")
            };

            var workspaceDirectory = Path.Combine("root", "tests", "inner");

            WhenMigrating(projectDirectories, workspaceDirectory)
                .RootBackupDirectory
                .FullName
                .Should()
                .Be(new DirectoryInfo(Path.Combine("root", "backup")).FullName.EnsureTrailingSlash());
        }

        [Fact]
        public void FilesToBackUpAreIdentifiedInTheRootProjectDirectory()
        {
            var workspaceDirectory = Path.Combine("src", "root");
            var projectDirectory = Path.Combine("src", "root");

            var whenMigrating = WhenMigrating(projectDirectory, workspaceDirectory);

            whenMigrating
                .FilesToMove(whenMigrating.ProjectBackupDirectories.Single())
                .Should()
                .Contain(_ => _.FullName == Path.Combine(new DirectoryInfo(workspaceDirectory).FullName, "project.json"));
        }

        [Fact]
        public void FilesToBackUpAreIdentifiedInTheDependencyProjectDirectory()
        {
            var workspaceDirectory = Path.Combine("src", "root");
            var projectDirectory = Path.Combine("src", "root");

            var whenMigrating = WhenMigrating(projectDirectory, workspaceDirectory);

            whenMigrating
                .FilesToMove(whenMigrating.ProjectBackupDirectories.Single())
                .Should()
                .Contain(_ => _.FullName == Path.Combine(new DirectoryInfo(projectDirectory).FullName, "project.json"));
        }

        private MigrationBackupPlan WhenMigrating(string projectDirectory, string workspaceDirectory) =>
            new MigrationBackupPlan(
                new [] { new DirectoryInfo(projectDirectory) },
                new DirectoryInfo(workspaceDirectory),
                dir => new [] { new FileInfo(Path.Combine(dir.FullName, "project.json")) });

        private MigrationBackupPlan WhenMigrating(string[] projectDirectories, string workspaceDirectory) =>
            new MigrationBackupPlan(
                projectDirectories.Select(p => new DirectoryInfo(p)),
                new DirectoryInfo(workspaceDirectory),
                dir => new [] { new FileInfo(Path.Combine(dir.FullName, "project.json")) });
    }
}
