// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;
using Microsoft.DotNet.Internal.ProjectModel.Utilities;
using Microsoft.DotNet.ProjectJsonMigration;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace Microsoft.DotNet.ProjectJsonMigration.Tests
{
    public partial class MigrationBackupPlanTests
    {
        [Fact]
        public void TheRootBackupDirectoryIsASiblingOfTheRootProject()
        {
            var dir = new DirectoryInfo(Path.Combine("src", "some-proj"));

            System.Console.WriteLine(dir.FullName);

            WhenMigrating(
                projectDirectory: dir.FullName,
                workspaceDirectory: Path.Combine("src", "RootProject"))
            .RootBackupDirectory
            .FullName
            .Should()
            .Be(new DirectoryInfo(Path.Combine("src", "backup")).FullName.EnsureTrailingSlash());
        }

        [Fact]
        public void TheRootProjectsBackupDirectoryIsASubfolderOfTheRootBackupDirectory()
        {
            WhenMigrating(
                projectDirectory: Path.Combine("src", "RootProject"),
                workspaceDirectory: Path.Combine("src", "RootProject"))
            .ProjectBackupDirectory
            .FullName
            .Should()
            .Be(new DirectoryInfo(Path.Combine("src", "backup", "RootProject")).FullName.EnsureTrailingSlash());
        }

        [Fact]
        public void ADependentProjectsMigrationBackupDirectoryIsASubfolderOfTheRootBackupDirectory()
        {
            WhenMigrating(
                 projectDirectory: Path.Combine("src", "Dependency"),
                 workspaceDirectory: Path.Combine("src", "RootProject"))
             .ProjectBackupDirectory
             .FullName
             .Should()
             .Be(new DirectoryInfo(Path.Combine("src", "backup", "Dependency")).FullName.EnsureTrailingSlash());
        }

        [Fact]
        public void FilesToBackUpAreIdentifiedInTheTheRootProjectDirectory()
        {
            var root = new DirectoryInfo(Path.Combine("src", "RootProject"));

            WhenMigrating(
                projectDirectory: root.FullName,
                workspaceDirectory: root.FullName)
            .FilesToMove
            .Should()
            .Contain(_ => _.FullName == Path.Combine(root.FullName, "project.json"));

        }

        [Fact]
        public void FilesToBackUpAreIdentifiedInTheTheDependencyProjectDirectory()
        {
            var root = new DirectoryInfo(Path.Combine("src", "RootProject"));
            var dependency = new DirectoryInfo(Path.Combine("src", "RootProject"));

            WhenMigrating(
                projectDirectory: dependency.FullName,
                workspaceDirectory: root.FullName)
            .FilesToMove
            .Should()
            .Contain(_ => _.FullName == Path.Combine(dependency.FullName, "project.json"));

        }

        private MigrationBackupPlan WhenMigrating(
            string projectDirectory,
            string workspaceDirectory) =>
            new MigrationBackupPlan(
                new DirectoryInfo(projectDirectory),
                new DirectoryInfo(workspaceDirectory),
                dir => new[] { new FileInfo(Path.Combine(dir.FullName, "project.json")) });

    }
}
