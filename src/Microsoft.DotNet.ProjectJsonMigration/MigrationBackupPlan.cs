// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.DotNet.Internal.ProjectModel.Utilities;

namespace Microsoft.DotNet.ProjectJsonMigration
{
    internal class MigrationBackupPlan
    {
        private const string TempCsprojExtention = ".migration_in_place_backup";

        private readonly FileInfo globalJson;
        private readonly Dictionary<DirectoryInfo, IEnumerable<FileInfo>> mapOfProjectBackupDirectoryToFilesToMove;

        public DirectoryInfo RootBackupDirectory { get; }
        public DirectoryInfo[] ProjectBackupDirectories { get; }

        public IEnumerable<FileInfo> FilesToMove(DirectoryInfo projectBackupDirectory)
            => mapOfProjectBackupDirectoryToFilesToMove[projectBackupDirectory];

        public MigrationBackupPlan(
            IEnumerable<DirectoryInfo> projectDirectories,
            DirectoryInfo workspaceDirectory,
            Func<DirectoryInfo, IEnumerable<FileInfo>> getFiles = null)
        {
            if (projectDirectories == null)
            {
                throw new ArgumentNullException(nameof(projectDirectories));
            }

            if (!projectDirectories.Any())
            {
                throw new ArgumentException("No project directories provided.", nameof(projectDirectories));
            }

            if (workspaceDirectory == null)
            {
                throw new ArgumentNullException(nameof(workspaceDirectory));
            }

            MigrationTrace.Instance.WriteLine("Computing migration backup plan...");

            projectDirectories = projectDirectories.Select(pd => new DirectoryInfo(pd.FullName.EnsureTrailingSlash()));
            workspaceDirectory = new DirectoryInfo(workspaceDirectory.FullName.EnsureTrailingSlash());

            MigrationTrace.Instance.WriteLine($"    Workspace: {workspaceDirectory.FullName}");
            foreach (var projectDirectory in projectDirectories)
            {
                MigrationTrace.Instance.WriteLine($"    Project: {projectDirectory.FullName}");
            }

            var rootDirectory = FindCommonRootPath(projectDirectories.ToArray()) ?? workspaceDirectory;
            rootDirectory = new DirectoryInfo(rootDirectory.FullName.EnsureTrailingSlash());

            MigrationTrace.Instance.WriteLine($"    Root: {rootDirectory.FullName}");

            globalJson = new FileInfo(
                Path.Combine(
                    workspaceDirectory.FullName,
                    "global.json"));

            RootBackupDirectory = new DirectoryInfo(
                GetUniqueDirectoryPath(
                    Path.Combine(
                        rootDirectory.FullName,
                        "backup"))
                    .EnsureTrailingSlash());

            MigrationTrace.Instance.WriteLine($"    Root Backup: {RootBackupDirectory.FullName}");

            var projectBackupDirectories = new List<DirectoryInfo>();
            mapOfProjectBackupDirectoryToFilesToMove = new Dictionary<DirectoryInfo, IEnumerable<FileInfo>>();
            getFiles = getFiles ?? (dir => dir.EnumerateFiles());

            foreach (var projectDirectory in projectDirectories)
            {
                var projectBackupDirectory = ComputeProjectBackupDirectoryPath(rootDirectory, projectDirectory, RootBackupDirectory);
                var filesToMove = getFiles(projectDirectory).Where(NeedsBackup);

                projectBackupDirectories.Add(projectBackupDirectory);
                mapOfProjectBackupDirectoryToFilesToMove.Add(projectBackupDirectory, filesToMove);
            }

            ProjectBackupDirectories = projectBackupDirectories.ToArray();
        }

        public void PerformBackup()
        {
            if (globalJson.Exists)
            {
                PathUtility.EnsureDirectoryExists(RootBackupDirectory.FullName);

                globalJson.MoveTo(
                    Path.Combine(
                        RootBackupDirectory.FullName,
                        globalJson.Name));
            }

            foreach (var kvp in mapOfProjectBackupDirectoryToFilesToMove)
            {
                var projectBackupDirectory = kvp.Key;
                var filesToMove = kvp.Value;

                PathUtility.EnsureDirectoryExists(projectBackupDirectory.FullName);

                foreach (var file in filesToMove)
                {
                    var fileName = file.Name.EndsWith(TempCsprojExtention)
                        ? Path.GetFileNameWithoutExtension(file.Name)
                        : file.Name;

                    file.MoveTo(
                        Path.Combine(
                            projectBackupDirectory.FullName,
                            fileName));
                }
            }
        }

        private static DirectoryInfo ComputeProjectBackupDirectoryPath(
            DirectoryInfo rootDirectory, DirectoryInfo projectDirectory, DirectoryInfo rootBackupDirectory)
        {
            if (PathUtility.IsChildOfDirectory(rootDirectory.FullName, projectDirectory.FullName))
            {
                var relativePath = PathUtility.GetRelativePath(
                    rootDirectory.FullName,
                    projectDirectory.FullName);

                return new DirectoryInfo(
                    Path.Combine(
                            rootBackupDirectory.FullName,
                            relativePath)
                        .EnsureTrailingSlash());
            }

            // Ensure that we use a unique name to avoid collisions as a fallback.
            return new DirectoryInfo(
                GetUniqueDirectoryPath(
                    Path.Combine(
                            rootBackupDirectory.FullName,
                            projectDirectory.Name)
                        .EnsureTrailingSlash()));
        }

        private static bool NeedsBackup(FileInfo file)
            => file.Name == "project.json"
            || file.Extension == ".xproj"
            || file.FullName.EndsWith(".xproj.user")
            || file.FullName.EndsWith(".lock.json")
            || file.FullName.EndsWith(TempCsprojExtention);

        private static string GetUniqueDirectoryPath(string directoryPath)
        {
            var candidatePath = directoryPath;

            var suffix = 1;
            while (Directory.Exists(candidatePath))
            {
                candidatePath = $"{directoryPath}_{suffix++}";
            }

            return candidatePath;
        }

        private static DirectoryInfo FindCommonRootPath(DirectoryInfo[] paths)
        {
            var pathSplits = new string[paths.Length][];
            var shortestLength = int.MaxValue;
            for (int i = 0; i < paths.Length; i++)
            {
                pathSplits[i] = paths[i].FullName.Split(new[] { Path.DirectorySeparatorChar });
                shortestLength = Math.Min(shortestLength, pathSplits[i].Length);
            }

            var builder = new StringBuilder();
            var splitIndex = 0;
            while (splitIndex < shortestLength)
            {
                var split = pathSplits[0][splitIndex];

                var done = false;
                for (int i = 1; i < pathSplits.Length; i++)
                {
                    if (pathSplits[i][splitIndex] != split)
                    {
                        done = true;
                        break;
                    }
                }

                if (done)
                {
                    break;
                }

                builder.Append(split);
                builder.Append(Path.DirectorySeparatorChar);
                splitIndex++;
            }

            return new DirectoryInfo(builder.ToString().EnsureTrailingSlash());
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
