// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.DotNet.Internal.ProjectModel.Utilities;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class MigrationBackupPlan
    {
        private const string TempCsprojExtention = ".migration_in_place_backup";

        private readonly FileInfo globalJson;

        public MigrationBackupPlan(
            DirectoryInfo projectDirectory,
            DirectoryInfo workspaceDirectory,
            Func<DirectoryInfo, IEnumerable<FileInfo>> getFiles = null)
        {
            if (projectDirectory == null)
            {
                throw new ArgumentNullException(nameof(projectDirectory));
            }
            if (workspaceDirectory == null)
            {
                throw new ArgumentNullException(nameof(workspaceDirectory));
            }

            globalJson = new FileInfo(Path.Combine(
                           workspaceDirectory.FullName,
                           "global.json"));

            projectDirectory = new DirectoryInfo(projectDirectory.FullName.EnsureTrailingSlash());
            workspaceDirectory = new DirectoryInfo(workspaceDirectory.FullName.EnsureTrailingSlash());

            RootBackupDirectory = new DirectoryInfo(
                Path.Combine(
                        workspaceDirectory.Parent.FullName,
                        "backup")
                    .EnsureTrailingSlash());

            ProjectBackupDirectory = new DirectoryInfo(
                Path.Combine(
                        RootBackupDirectory.FullName,
                        projectDirectory.Name)
                    .EnsureTrailingSlash());

            var relativeDirectory = PathUtility.GetRelativePath(
                workspaceDirectory.FullName,
                projectDirectory.FullName);

            getFiles = getFiles ??
                       (dir => dir.EnumerateFiles());

            FilesToMove = getFiles(projectDirectory)
               .Where(f => f.Name == "project.json"
                        || f.Extension == ".xproj"
                        || f.FullName.EndsWith(".xproj.user")
                        || f.FullName.EndsWith(".lock.json")
                        || f.FullName.EndsWith(TempCsprojExtention));
        }

        public DirectoryInfo ProjectBackupDirectory { get; }

        public DirectoryInfo RootBackupDirectory { get; }

        public IEnumerable<FileInfo> FilesToMove { get; }

        public void PerformBackup()
        {
            if (globalJson.Exists)
            {
                PathUtility.EnsureDirectoryExists(RootBackupDirectory.FullName);

                globalJson.MoveTo(Path.Combine(
                    ProjectBackupDirectory.Parent.FullName,
                    globalJson.Name));
            }

            PathUtility.EnsureDirectoryExists(ProjectBackupDirectory.FullName);

            foreach (var file in FilesToMove)
            {
                var fileName = file.Name.EndsWith(TempCsprojExtention)
                    ? Path.GetFileNameWithoutExtension(file.Name)
                    : file.Name;

                file.MoveTo(
                    Path.Combine(ProjectBackupDirectory.FullName, fileName));
            }
        }

        public static void RenameCsprojFromMigrationOutputNameToTempName(string outputProject)
        {
            var backupFileName = $"{outputProject}{TempCsprojExtention}";
            if (File.Exists(backupFileName))
            {
                File.Delete(backupFileName);
            }
            File.Move(outputProject, backupFileName);
        }
    }
}
